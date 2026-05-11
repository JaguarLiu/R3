using System.Text;
using System.Text.Json;
using BudPay.Data;
using BudPay.Models;
using BudPay.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddHttpClient<LineClient>();

var app = builder.Build();

// Auto-migrate on startup (fine for dev; switch to explicit migrations for prod)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapGet("/", () => "BudPay LINE bot is alive.");

app.MapPost("/webhook", async (
    HttpRequest request,
    AppDbContext db,
    LineClient line,
    IConfiguration config,
    ILogger<Program> log,
    CancellationToken ct) =>
{
    // 1. Read raw body for signature verification
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms, ct);
    var body = ms.ToArray();

    var secret = config["Line:ChannelSecret"]
        ?? throw new InvalidOperationException("Line:ChannelSecret missing");
    var sigHeader = request.Headers["x-line-signature"].ToString();

    log.LogInformation("Webhook hit. sig={Sig} bodyLen={Len} body={Body}",
        sigHeader, body.Length, System.Text.Encoding.UTF8.GetString(body));

    if (!LineSignature.Verify(secret, body, sigHeader))
    {
        log.LogWarning("Invalid LINE signature");
        return Results.Unauthorized();
    }

    // 2. Deserialize
    var payload = JsonSerializer.Deserialize<LineWebhookPayload>(body,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (payload is null) return Results.Ok();

    var keyword = config["Line:TriggerKeyword"] ?? "/記帳";

    foreach (var ev in payload.Events)
    {
        if (ev.Type != "message" || ev.Message?.Type != "text") continue;
        var text = ev.Message.Text ?? "";

        // 3. Trigger check: keyword prefix only
        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
        {
            log.LogInformation("Skip: srcType={Src} text={Text}", ev.Source?.Type, text);
            continue;
        }

        // Strip the keyword so the parser only sees the payload
        text = trimmed[keyword.Length..].TrimStart();

        // 4. Parse amount + note
        var parsed = MessageParser.TryParse(text);
        if (parsed is null)
        {
            if (ev.ReplyToken is not null)
                await line.ReplyAsync(ev.ReplyToken, "沒抓到金額哦，試試「@bot 午餐 120」", ct);
            continue;
        }

        // 5. Persist
        var expense = new Expense
        {
            LineUserId = ev.Source?.UserId ?? "",
            LineGroupId = ev.Source?.GroupId ?? ev.Source?.RoomId,
            Amount = parsed.Amount,
            Note = parsed.Note,
            RawText = text,
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);

        // 6. Reply
        if (ev.ReplyToken is not null)
        {
            var reply = parsed.Note is null
                ? $"已記錄：{parsed.Amount} 元"
                : $"已記錄：{parsed.Note} {parsed.Amount} 元";
            await line.ReplyAsync(ev.ReplyToken, reply, ct);
        }
    }

    return Results.Ok();
});

app.Run();
