using R3.Services;
using R3.Models;
using Microsoft.Extensions.Options;

namespace R3.Tests;

public class TokenServiceTests
{
    private static TokenService Make(string key = "this-is-a-test-signing-key-32bytes!!", string iss = "r3", string aud = "r3-web")
        => new(Options.Create(new JwtOptions { SignKey = key, Issuer = iss, Audience = aud, AccessTokenMinutes = 15 }));

    [Fact]
    public void AccessToken_RoundTrips_UserId()
    {
        var svc = Make();
        var token = svc.CreateAccessToken(42, "Alice");
        var principal = svc.ValidateAccessToken(token);
        Assert.NotNull(principal);
        Assert.Equal("42", principal!.FindFirst("sub")!.Value);
    }

    [Fact]
    public void ValidateAccessToken_RejectsWrongKey()
    {
        var token = Make(key: "this-is-a-test-signing-key-32bytes!!").CreateAccessToken(1, "A");
        var other = Make(key: "a-completely-different-signing-keyyyy");
        Assert.Null(other.ValidateAccessToken(token));
    }

    [Fact]
    public void ValidateAccessToken_RejectsExpiredToken()
    {
        var svc = new TokenService(Options.Create(new JwtOptions
        {
            SignKey = "this-is-a-test-signing-key-32bytes!!",
            Issuer = "r3", Audience = "r3-web", AccessTokenMinutes = -10,
        }));
        var token = svc.CreateAccessToken(1, "A");
        Assert.Null(svc.ValidateAccessToken(token));
    }

    [Fact]
    public void ValidateAccessToken_RejectsWrongIssuer()
    {
        var token = Make(iss: "evil-issuer").CreateAccessToken(1, "A");
        Assert.Null(Make().ValidateAccessToken(token)); // default issuer "r3"
    }

    [Fact]
    public void RefreshToken_HashIsDeterministic_AndDiffersFromRaw()
    {
        var raw = TokenService.GenerateRefreshToken();
        Assert.Equal(TokenService.HashRefreshToken(raw), TokenService.HashRefreshToken(raw));
        Assert.NotEqual(raw, TokenService.HashRefreshToken(raw));
    }
}
