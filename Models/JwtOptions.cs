namespace R3.Models;

public class JwtOptions
{
    public string SignKey { get; set; } = "";
    public string Issuer { get; set; } = "r3";
    public string Audience { get; set; } = "r3-web";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}
