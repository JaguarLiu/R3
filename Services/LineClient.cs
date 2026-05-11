using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace BudPay.Services;

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
}
