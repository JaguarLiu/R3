using System.Security.Claims;
using R3.Auth;
using R3.Data;
using R3.Models;
using Microsoft.EntityFrameworkCore;

namespace R3.Tests;

public class TripAccessTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ClaimsPrincipal UserPrincipal(long id) =>
        new(new ClaimsIdentity(new[] { new Claim("sub", id.ToString()) }, "test"));

    [Fact]
    public void CurrentUserId_ParsesSubClaim()
        => Assert.Equal(99, UserPrincipal(99).CurrentUserId());

    [Fact]
    public async Task Owner_HasAccess()
    {
        using var db = NewDb();
        db.Trips.Add(new Trip { Id = 1, Title = "T", OwnerUserId = 5 });
        await db.SaveChangesAsync();
        Assert.NotNull(await TripAccess.FindAccessibleAsync(db, 1, userId: 5, ownerOnly: false, default));
    }

    [Fact]
    public async Task Member_HasAccess_ButNotWhenOwnerOnly()
    {
        using var db = NewDb();
        db.Trips.Add(new Trip { Id = 1, Title = "T", OwnerUserId = 5 });
        db.TripMembers.Add(new TripMember { TripId = 1, UserId = 8 });
        await db.SaveChangesAsync();
        Assert.NotNull(await TripAccess.FindAccessibleAsync(db, 1, userId: 8, ownerOnly: false, default));
        Assert.Null(await TripAccess.FindAccessibleAsync(db, 1, userId: 8, ownerOnly: true, default));
    }

    [Fact]
    public async Task Stranger_HasNoAccess()
    {
        using var db = NewDb();
        db.Trips.Add(new Trip { Id = 1, Title = "T", OwnerUserId = 5 });
        await db.SaveChangesAsync();
        Assert.Null(await TripAccess.FindAccessibleAsync(db, 1, userId: 999, ownerOnly: false, default));
    }
}
