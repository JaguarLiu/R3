using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using R3.Data;
using R3.Models;
using R3.Endpoints;
using R3.Services;
using R3.Providers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Npgsql 8+ requires opting into dynamic JSON to serialize arbitrary CLR types into jsonb.
// SplitExpense.Payers/Splits are Dictionary<string,decimal> mapped to jsonb; without this,
// SaveChanges throws at write time (the silent 500 on POST /api/trips/{id}/expenses).
var npgsqlDataSource = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("Postgres"))
    .EnableDynamicJson()
    .Build();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(npgsqlDataSource));

builder.Services.AddHttpClient<LineClient>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<LineLoginClient>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<RefreshTokenService>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

const string SpaCorsPolicy = "SpaDev";
builder.Services.AddCors(o => o.AddPolicy(SpaCorsPolicy, p =>
    p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

// Trust X-Forwarded-* from upstream proxy so Connection.RemoteIpAddress is the real client IP.
// The middleware only honors X-Forwarded-For when the connection peer is a KnownProxy (single IP)
// or inside a KnownNetwork (CIDR). PaaS gateways (e.g. Zeabur) connect from a *dynamic* private IP,
// so pin the proxy's network via ForwardedHeaders:KnownNetworks rather than a single IP.
// For dev we clear the defaults so any loopback/sidecar works; in prod populate via config.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Number of proxy hops to trust. Default 1 (single PaaS gateway in front of the container);
    // raising it past the real number lets clients spoof X-Forwarded-For to dodge per-IP limits.
    options.ForwardLimit = builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit") ?? 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var ip in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (System.Net.IPAddress.TryParse(ip, out var addr)) options.KnownProxies.Add(addr);
    }
    foreach (var cidr in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    {
        if (System.Net.IPNetwork.TryParse(cidr, out var net)) options.KnownIPNetworks.Add(net);
    }
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
        await ctx.HttpContext.Response.WriteAsync(
            "{\"error\":\"rate_limited\",\"message\":\"AI 呼叫太頻繁了，請稍後再試\"}", ct);
    };

    // Per-IP fixed window: 5 requests / minute, no queueing.
    options.AddPolicy("ai", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    });

    // Per-IP fixed window: 10 requests / minute for auth endpoints.
    options.AddPolicy("auth", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    });
});

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SignKey)),
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ClockSkew = TimeSpan.FromSeconds(5),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Global catch-all: log EVERY unhandled exception (from any endpoint or middleware) with full
// detail + the request method/path, then return a 500 ProblemDetails. Registered first so it
// wraps the entire pipeline. Without this, unhandled errors returned an empty 500 and left the
// log silent (e.g. POST /api/trips/{id}/expenses failures).
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("R3.UnhandledException");
        logger.LogError(ex, "Unhandled exception on {Method} {Path}",
            context.Request.Method, context.Request.Path);

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            // Never leak ex.Message to clients in prod (may contain connection strings, paths, PII).
            // The full exception is already in the server log above; clients get a traceId to report.
            await context.Response.WriteAsJsonAsync(new
            {
                title = "An unexpected error occurred.",
                status = 500,
                detail = app.Environment.IsDevelopment() ? ex.Message : "An internal error occurred.",
                traceId = context.TraceIdentifier,
            });
        }
    }
});

// Must run BEFORE UseRateLimiter so the limiter partitions on the real client IP.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
    app.UseCors(SpaCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
// .webmanifest isn't in ASP.NET Core's default MIME map; register it so the
// PWA manifest is served as application/manifest+json (browsers reject otherwise).
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypeProvider });

// Auto-migrate on startup (fine for dev; switch to explicit migrations for prod)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapTripEndpoints();
app.MapWebhookEndpoints();
app.MapAuthEndpoints();

app.MapGet("/api/health", () => "R3 API is alive.");

app.Run();
