using System.Text.Json;

namespace R3.Providers;

// LINE Login (OAuth 2.0) — a SEPARATE channel from the messaging bot.
// Config: LineLogin:ChannelId, LineLogin:ChannelSecret, LineLogin:CallbackUrl
public class LineLoginClient
{
    private readonly HttpClient _http;
    private readonly string _channelId;
    private readonly string _channelSecret;
    private readonly string _callbackUrl;

    public LineLoginClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _channelId = config["LineLogin:ChannelId"] ?? "";
        _channelSecret = config["LineLogin:ChannelSecret"] ?? "";
        _callbackUrl = config["LineLogin:CallbackUrl"] ?? "";
    }

    public string BuildAuthorizeUrl(string state) =>
        "https://access.line.me/oauth2/v2.1/authorize?response_type=code"
        + $"&client_id={Uri.EscapeDataString(_channelId)}"
        + $"&redirect_uri={Uri.EscapeDataString(_callbackUrl)}"
        + $"&state={Uri.EscapeDataString(state)}"
        + "&scope=profile%20openid";

    public record LineProfile(string UserId, string DisplayName);

    public async Task<LineProfile> ExchangeAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_channelId)) throw new InvalidOperationException("LineLogin:ChannelId missing");

        using var tokenResp = await _http.PostAsync("https://api.line.me/oauth2/v2.1/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _callbackUrl,
                ["client_id"] = _channelId,
                ["client_secret"] = _channelSecret,
            }), ct);
        var tokenBody = await tokenResp.Content.ReadAsStringAsync(ct);
        if (!tokenResp.IsSuccessStatusCode)
            throw new HttpRequestException($"LINE token exchange failed: {tokenBody}");

        using var tokenDoc = JsonDocument.Parse(tokenBody);
        var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

        using var profileReq = new HttpRequestMessage(HttpMethod.Get, "https://api.line.me/v2/profile");
        profileReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var profileResp = await _http.SendAsync(profileReq, ct);
        var profileBody = await profileResp.Content.ReadAsStringAsync(ct);
        if (!profileResp.IsSuccessStatusCode)
            throw new HttpRequestException($"LINE profile fetch failed: {profileBody}");

        using var profileDoc = JsonDocument.Parse(profileBody);
        var root = profileDoc.RootElement;
        return new LineProfile(
            root.GetProperty("userId").GetString() ?? "",
            root.GetProperty("displayName").GetString() ?? "LINE 使用者");
    }
}
