using System.Text.Json;
using System.Threading.RateLimiting;
using BudPay.Data;
using BudPay.Endpoints;
using BudPay.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddHttpClient<LineClient>();
builder.Services.AddHttpClient<GeminiService>();

builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

const string SpaCorsPolicy = "SpaDev";
builder.Services.AddCors(o => o.AddPolicy(SpaCorsPolicy, p =>
    p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

// Trust X-Forwarded-* from upstream proxy so Connection.RemoteIpAddress is the real client IP.
// KnownProxies/KnownNetworks should list the trusted proxy hop(s). For dev we clear them so
// any loopback/sidecar works; in prod populate via config (see ForwardedHeaders:KnownProxies).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var ip in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (System.Net.IPAddress.TryParse(ip, out var addr)) options.KnownProxies.Add(addr);
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
});

var app = builder.Build();

// Must run BEFORE UseRateLimiter so the limiter partitions on the real client IP.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
    app.UseCors(SpaCorsPolicy);

app.UseRateLimiter();

app.UseDefaultFiles();
app.UseStaticFiles();

// Auto-migrate on startup (fine for dev; switch to explicit migrations for prod)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapTripEndpoints();
app.MapWebhookEndpoints();

app.MapGet("/api/health", () => "BudPay API is alive.");

app.Run();
