using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;

namespace R3.Providers;

public class LineClient(HttpClient http, IConfiguration config, ILogger<LineClient> logger)
{
    private readonly string _token = config["Line:ChannelAccessToken"]
        ?? throw new InvalidOperationException("Line:ChannelAccessToken missing");

    public async Task ReplyAsync(string replyToken, string text, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/reply")
        {
            Content = JsonContent.Create(new
            {
                replyToken,
                messages = new[] { new { type = "text", text } }
            })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            logger.LogWarning("LINE reply failed: {Status} {Body}", res.StatusCode, body);
        else
            logger.LogInformation("LINE reply ok: {Status}", res.StatusCode);
    }

    // Resolves the display name for a sender. sourceType is the LINE event source type
    // ("user" | "group" | "room"). For group/room we must use the membership-scoped endpoint
    // because the global /v2/bot/profile only works if the user has added the bot as a friend.
    public async Task<string?> GetDisplayNameAsync(string? sourceType, string? scopeId, string? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var url = sourceType switch
        {
            "group" when !string.IsNullOrEmpty(scopeId) => $"https://api.line.me/v2/bot/group/{scopeId}/member/{userId}",
            "room"  when !string.IsNullOrEmpty(scopeId) => $"https://api.line.me/v2/bot/room/{scopeId}/member/{userId}",
            _ => $"https://api.line.me/v2/bot/profile/{userId}",
        };

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            logger.LogWarning("LINE profile fetch failed: {Status} {Body}", res.StatusCode, body);
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("displayName", out var name) ? name.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
