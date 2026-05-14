using System.Text.Json;
using BudPay.Data;
using BudPay.Models;
using BudPay.Services;
using Microsoft.EntityFrameworkCore;

namespace BudPay.Endpoints;

public static class WebhookEndpoints
{
    // Command prefixes. The legacy /記帳 keyword is still read from config (Line:TriggerKeyword).
    private const string TripCommand = "/旅程";
    private const string SwitchTripCommand = "/切換";
    private const string SettleCommand = "/結算";
    private const string ListSubcommand = "列表";

    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook", async (
            HttpRequest request,
            AppDbContext db,
            LineClient line,
            GeminiService gemini,
            IConfiguration config,
            ILogger<WebhookLog> log,
            CancellationToken ct) =>
        {
            // 1. Read raw body for signature verification (must hash exact bytes LINE sent).
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, ct);
            var body = ms.ToArray();

            var secret = config["Line:ChannelSecret"] ?? throw new InvalidOperationException("Line:ChannelSecret missing");
            var sigHeader = request.Headers["x-line-signature"].ToString();

            log.LogInformation("Webhook hit. sig={Sig} bodyLen={Len}", sigHeader, body.Length);

            if (!LineSignature.Verify(secret, body, sigHeader))
            {
                log.LogWarning("Invalid LINE signature");
                return Results.Unauthorized();
            }

            var payload = JsonSerializer.Deserialize<LineWebhookPayload>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload is null) return Results.Ok();

            var recordKeyword = config["Line:TriggerKeyword"] ?? "/記帳";

            foreach (var ev in payload.Events)
            {
                if (ev.Type != "message" || ev.Message?.Type != "text") continue;
                var text = (ev.Message.Text ?? "").TrimStart();
                var scopeId = ResolveScopeId(ev.Source);
                if (scopeId is null) continue;  // can't route a trip without a stable scope

                try
                {
                    if (TryStripPrefix(text, TripCommand, out var tripArg))
                        await HandleTripCommand(tripArg, scopeId, ev, db, line, log, ct);
                    else if (TryStripPrefix(text, SwitchTripCommand, out var switchArg))
                        await HandleSwitchTrip(switchArg, scopeId, ev, db, line, ct);
                    else if (TryStripPrefix(text, SettleCommand, out _))
                        await HandleSettle(scopeId, ev, db, line, ct);
                    else if (TryStripPrefix(text, recordKeyword, out var recordArg))
                        await HandleRecord(recordArg, scopeId, ev, db, line, gemini, log, ct);
                    else
                        log.LogInformation("Skip: srcType={Src} text={Text}", ev.Source?.Type, text);
                }
                catch (Exception perEventEx)
                {
                    // LINE retries on non-2xx — never bubble per-event failures out of the loop.
                    log.LogError(perEventEx, "Webhook event handler crashed");
                }
            }
            return Results.Ok();
        });
    }

    // -- /旅程 <name> | /旅程 列表 -------------------------------------------
    private static async Task HandleTripCommand(string arg, string scopeId, LineEvent ev,
        AppDbContext db, LineClient line, ILogger log, CancellationToken ct)
    {
        var trimmed = arg.Trim();

        // Subcommand: /旅程 列表
        if (trimmed.Equals(ListSubcommand, StringComparison.OrdinalIgnoreCase))
        {
            await HandleListTrips(scopeId, ev, db, line, ct);
            return;
        }

        // Otherwise: /旅程 <name>
        var title = trimmed;
        if (string.IsNullOrWhiteSpace(title))
        {
            await Reply(line, ev, "用法：\n・/旅程 <名稱>  建立新旅程\n・/旅程 列表       列出本群所有旅程", ct);
            return;
        }
        if (title.Length > 100 || title.Any(char.IsControl))
        {
            await Reply(line, ev, "旅程名稱不合法", ct);
            return;
        }

        // Deactivate any currently active trip in this scope, then create + activate the new one.
        await db.Trips.Where(t => t.LineGroupId == scopeId && t.IsActive)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.IsActive, false), ct);

        var trip = new Trip
        {
            Title = title,
            Days = 1,
            LineGroupId = scopeId,
            IsActive = true,
        };
        db.Trips.Add(trip);
        await db.SaveChangesAsync(ct);

        log.LogInformation("Created trip {Id} '{Title}' for scope {Scope}", trip.Id, trip.Title, scopeId);
        await Reply(line, ev, $"已建立新旅程：{trip.Title} (id {trip.Id})", ct);
    }

    // -- /切換 <name> ---------------------------------------------------------
    private static async Task HandleSwitchTrip(string arg, string scopeId, LineEvent ev,
        AppDbContext db, LineClient line, CancellationToken ct)
    {
        var query = arg.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            await Reply(line, ev, "用法：/切換 <旅程名稱或 id>", ct);
            return;
        }

        Trip? target = null;
        if (long.TryParse(query, out var id))
            target = await db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.LineGroupId == scopeId, ct);
        target ??= await db.Trips.FirstOrDefaultAsync(t => t.LineGroupId == scopeId && t.Title == query, ct);

        if (target is null)
        {
            await Reply(line, ev, $"找不到旅程「{query}」", ct);
            return;
        }

        await db.Trips.Where(t => t.LineGroupId == scopeId && t.IsActive && t.Id != target.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.IsActive, false), ct);
        target.IsActive = true;
        await db.SaveChangesAsync(ct);
        await Reply(line, ev, $"已切換至旅程：{target.Title} (id {target.Id})", ct);
    }

    // -- /旅程 列表 ----------------------------------------------------------
    private static async Task HandleListTrips(string scopeId, LineEvent ev,
        AppDbContext db, LineClient line, CancellationToken ct)
    {
        var trips = await db.Trips.AsNoTracking()
            .Where(t => t.LineGroupId == scopeId)
            .OrderByDescending(t => t.IsActive)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new { t.Id, t.Title, t.IsActive })
            .ToListAsync(ct);

        if (trips.Count == 0)
        {
            await Reply(line, ev, "本群還沒有旅程，輸入「/旅程 <名稱>」建立第一個吧！", ct);
            return;
        }

        var lines = trips.Select(t => $"{(t.IsActive ? "▶" : "・")} {t.Title}  (id {t.Id})");
        await Reply(line, ev, $"本群旅程：\n{string.Join("\n", lines)}", ct);
    }

    // -- /結算 ---------------------------------------------------------------
    private static async Task HandleSettle(string scopeId, LineEvent ev,
        AppDbContext db, LineClient line, CancellationToken ct)
    {
        var trip = await db.Trips.AsNoTracking()
            .Include(t => t.Participants.OrderBy(p => p.Order))
            .Include(t => t.Expenses)
            .FirstOrDefaultAsync(t => t.LineGroupId == scopeId && t.IsActive, ct);

        if (trip is null)
        {
            await Reply(line, ev, "還沒有作用中的旅程，沒東西可以結算。", ct);
            return;
        }
        if (trip.Expenses.Count == 0)
        {
            await Reply(line, ev, $"《{trip.Title}》目前還沒有任何花費。", ct);
            return;
        }

        var participants = trip.Participants.Select(p => p.Name).ToList();
        var transfers = SettlementCalculator.Compute(participants, trip.Expenses);

        if (transfers.Count == 0)
        {
            await Reply(line, ev, $"《{trip.Title}》：扯平啦！沒人欠錢～", ct);
            return;
        }

        var lines = transfers.Select(t => $"・{t.From} → {t.To}  ${t.Amount:N0}");
        await Reply(line, ev, $"《{trip.Title}》結算：\n{string.Join("\n", lines)}", ct);
    }

    // -- /記帳 <text> ---------------------------------------------------------
    private static async Task HandleRecord(string arg, string scopeId, LineEvent ev,
        AppDbContext db, LineClient line, GeminiService gemini, ILogger log, CancellationToken ct)
    {
        var trip = await db.Trips
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.LineGroupId == scopeId && t.IsActive, ct);
        if (trip is null)
        {
            await Reply(line, ev, "還沒有作用中的旅程，請先輸入「/旅程 <名稱>」建立。", ct);
            return;
        }

        // Determine participant list for this expense.
        //  - Prefer @-mentions in the message.
        //  - Otherwise fall back to the sender's display name.
        // Any name not yet in the trip is auto-added.
        var fromMessage = ExtractMentionNames(ev.Message!);
        if (fromMessage.Count == 0)
        {
            var senderName = await line.GetDisplayNameAsync(ev.Source?.Type, scopeId, ev.Source?.UserId, ct);
            if (!string.IsNullOrWhiteSpace(senderName)) fromMessage.Add(senderName.Trim());
        }

        if (fromMessage.Count == 0)
        {
            await Reply(line, ev, "抓不到任何成員（請 @-mention 或讓我可以讀到你的名字）", ct);
            return;
        }

        await EnsureParticipants(db, trip, fromMessage, ct);
        var participants = trip.Participants.Select(p => p.Name).ToList();
        var senderDisplay = fromMessage.FirstOrDefault();

        var text = arg.TrimStart();

        // Gemini parse → SplitExpense rows. Regex fallback only writes a single self-paid record.
        List<SplitExpense> entries;
        try
        {
            var parsed = await gemini.BatchParseAsync(text, participants, ct);
            entries = parsed.Items
                .Where(i => i.Total > 0 && !string.IsNullOrWhiteSpace(i.Item))
                .Select(i => ExpenseBuilder.FromParsedItem(trip.Id, i, participants, senderDisplay))
                .ToList();

            if (entries.Count == 0)
            {
                var fallback = MessageParser.TryParse(text);
                if (fallback is null)
                {
                    await Reply(line, ev, "沒抓到任何花費，試試「/記帳 午餐 120」", ct);
                    return;
                }
                entries.Add(BuildFromRegex(trip.Id, fallback.Amount, fallback.Note, senderDisplay!, participants));
            }
        }
        catch (Exception aiEx)
        {
            log.LogWarning(aiEx, "Gemini parse failed, falling back to regex");
            var fallback = MessageParser.TryParse(text);
            if (fallback is null)
            {
                await Reply(line, ev, "沒抓到金額哦，試試「/記帳 午餐 120」", ct);
                return;
            }
            entries = new() { BuildFromRegex(trip.Id, fallback.Amount, fallback.Note, senderDisplay!, participants) };
        }

        db.SplitExpenses.AddRange(entries);
        await db.SaveChangesAsync(ct);

        var summary = string.Join("\n", entries.Select(e =>
            $"・{e.Item} {e.Total} 元（{string.Join("+", e.Payers.Keys)} 付）"));
        await Reply(line, ev, $"已記到《{trip.Title}》：\n{summary}", ct);
    }

    // -- helpers --------------------------------------------------------------

    private static SplitExpense BuildFromRegex(long tripId, decimal amount, string? note, string payer, List<string> participants)
    {
        var splits = participants.ToDictionary(p => p, p => p == payer ? amount : 0m);
        return new SplitExpense
        {
            TripId = tripId,
            Day = "第 1 天",
            Item = string.IsNullOrWhiteSpace(note) ? "未命名" : note!.Trim(),
            Total = amount,
            Payers = new Dictionary<string, decimal> { [payer] = amount },
            Splits = splits,
        };
    }

    private static async Task EnsureParticipants(AppDbContext db, Trip trip, IEnumerable<string> names, CancellationToken ct)
    {
        var existing = trip.Participants.Select(p => p.Name).ToHashSet();
        var order = trip.Participants.Count;
        var added = false;
        foreach (var name in names)
        {
            if (existing.Contains(name)) continue;
            trip.Participants.Add(new Participant { Name = name, Order = order++, TripId = trip.Id });
            existing.Add(name);
            added = true;
        }
        if (added) await db.SaveChangesAsync(ct);
    }

    // group/room get their own scope (one trip per group); 1:1 chats use the user id.
    private static string? ResolveScopeId(LineSource? source) =>
        source?.GroupId ?? source?.RoomId ?? source?.UserId;

    private static bool TryStripPrefix(string text, string prefix, out string rest)
    {
        if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            rest = text[prefix.Length..].TrimStart();
            return true;
        }
        rest = "";
        return false;
    }

    private static Task Reply(LineClient line, LineEvent ev, string text, CancellationToken ct) =>
        ev.ReplyToken is null ? Task.CompletedTask : line.ReplyAsync(ev.ReplyToken, text, ct);

    // Pulls each @-mention's display name out of the LINE message text using LINE's index/length
    // offsets. Skips self / @all, validates length + control chars before letting names flow into
    // a Gemini system prompt as a participant whitelist.
    private static List<string> ExtractMentionNames(LineMessage message)
    {
        var text = message.Text ?? "";
        var mentionees = message.Mention?.Mentionees;
        if (mentionees is null || mentionees.Count == 0) return new List<string>();

        var names = new List<string>();
        foreach (var mentionee in mentionees)
        {
            if (mentionee.IsSelf == true) continue;
            if (mentionee.Type == "all") continue;
            if (mentionee.Index < 0 || mentionee.Length <= 0) continue;
            if (mentionee.Index + mentionee.Length > text.Length) continue;

            var span = text.Substring(mentionee.Index, mentionee.Length);
            var name = (span.StartsWith('@') ? span[1..] : span).Trim();

            if (name.Length == 0 || name.Length > 30) continue;
            if (name.Any(char.IsControl)) continue;
            if (name.Any(c => c == '\u2028' || c == '\u2029')) continue;

            names.Add(name);
        }
        return names.Distinct().ToList();
    }
}

// Marker type used as the ILogger<T> category for webhook log lines.
public sealed class WebhookLog { }
