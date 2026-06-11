using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using R3.Models;

namespace R3.Services;

public class TokenService
{
    private readonly JwtOptions _opts;
    private readonly SymmetricSecurityKey _key;

    public TokenService(IOptions<JwtOptions> opts)
    {
        _opts = opts.Value;
        if (Encoding.UTF8.GetByteCount(_opts.SignKey) < 32)
            throw new InvalidOperationException("Jwt:SignKey must be at least 32 bytes (256 bits) for HS256.");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SignKey));
    }

    public string CreateAccessToken(long userId, string displayName)
    {
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("name", displayName),
        };
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opts.AccessTokenMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Used by tests; runtime validation is done by the JwtBearer middleware.
    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = _opts.Issuer,
                ValidAudience = _opts.Audience,
                IssuerSigningKey = _key,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(5),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            }, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException) { return null; }
    }

    public static string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string HashRefreshToken(string raw)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
