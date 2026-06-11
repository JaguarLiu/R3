using R3.Data;
using R3.Services;
using R3.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace R3.Tests;

public class RefreshTokenServiceTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static RefreshTokenService Make(AppDbContext db) =>
        new(db, Options.Create(new JwtOptions { RefreshTokenDays = 14 }));

    [Fact]
    public async Task Issue_ThenValidate_ReturnsActiveRow()
    {
        using var db = NewDb();
        var svc = Make(db);
        var (raw, _) = await svc.IssueAsync(7, default);
        var row = await svc.ValidateAsync(raw, default);
        Assert.NotNull(row);
        Assert.Equal(7, row!.UserId);
    }

    [Fact]
    public async Task Rotate_RevokesOld_AndOldTokenNoLongerValidates()
    {
        using var db = NewDb();
        var svc = Make(db);
        var (raw, _) = await svc.IssueAsync(7, default);
        var rotated = await svc.RotateAsync(raw, default);
        Assert.NotNull(rotated);
        Assert.Null(await svc.ValidateAsync(raw, default));              // old dead
        Assert.NotNull(await svc.ValidateAsync(rotated!.Value.newRaw, default)); // new alive
    }

    [Fact]
    public async Task Validate_ReturnsNull_ForUnknownToken()
    {
        using var db = NewDb();
        Assert.Null(await Make(db).ValidateAsync("never-issued", default));
    }

    [Fact]
    public async Task Validate_ReturnsNull_ForExpiredToken()
    {
        using var db = NewDb();
        var svc = new RefreshTokenService(db, Options.Create(new JwtOptions { RefreshTokenDays = -1 }));
        var (raw, _) = await svc.IssueAsync(7, default);
        Assert.Null(await svc.ValidateAsync(raw, default));
    }

    [Fact]
    public async Task Revoke_MakesTokenNoLongerValidate()
    {
        using var db = NewDb();
        var svc = Make(db);
        var (raw, _) = await svc.IssueAsync(7, default);
        await svc.RevokeAsync(raw, default);
        Assert.Null(await svc.ValidateAsync(raw, default));
    }
}
