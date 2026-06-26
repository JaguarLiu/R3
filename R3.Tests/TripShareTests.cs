using R3.Auth;
using R3.Data;
using R3.Models;
using Microsoft.EntityFrameworkCore;

namespace R3.Tests;

public class TripShareTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void GenerateToken_IsUrlSafeAndUnique()
    {
        var a = TripShare.GenerateToken();
        var b = TripShare.GenerateToken();
        Assert.NotEqual(a, b);
        Assert.Equal(64, a.Length);
        Assert.Matches("^[0-9A-F]+$", a);
    }

    [Fact]
    public async Task ResetShareAsync_OwnerGetsToken_NonOwnerNull()
    {
        using var db = NewDb();
        db.Trips.Add(new Trip { Id = 1, Title = "T", OwnerUserId = 5 });
        await db.SaveChangesAsync();

        var ok = await TripShare.ResetShareAsync(db, 1, ownerUserId: 5, linkDays: 7, default);
        Assert.NotNull(ok);
        Assert.True(ok!.Value.expiresAt > DateTime.UtcNow);

        var trip = await db.Trips.FindAsync(1L);
        Assert.Equal(ok.Value.token, trip!.ShareToken);

        var bad = await TripShare.ResetShareAsync(db, 1, ownerUserId: 99, linkDays: 7, default);
        Assert.Null(bad);
    }

    [Fact]
    public async Task ResetShareAsync_RotatesToken()
    {
        using var db = NewDb();
        db.Trips.Add(new Trip { Id = 1, Title = "T", OwnerUserId = 5 });
        await db.SaveChangesAsync();

        var first = await TripShare.ResetShareAsync(db, 1, 5, 7, default);
        var second = await TripShare.ResetShareAsync(db, 1, 5, 7, default);
        Assert.NotEqual(first!.Value.token, second!.Value.token);
    }

    private static async Task<(AppDbContext db, string token)> SeededTrip()
    {
        var db = NewDb();
        db.Trips.Add(new Trip
        {
            Id = 1, Title = "東京", OwnerUserId = 5,
            Participants = { new Participant { Id = 10, Name = "小王", Order = 0, TripId = 1 },
                             new Participant { Id = 11, Name = "小花", Order = 1, TripId = 1 } }
        });
        await db.SaveChangesAsync();
        var t = await TripShare.ResetShareAsync(db, 1, 5, 7, default);
        return (db, t!.Value.token);
    }

    [Fact]
    public async Task GetJoinInfo_ListsUnclaimedParticipants()
    {
        var (db, token) = await SeededTrip();
        var info = await TripShare.GetJoinInfoAsync(db, token, userId: 8, default);
        Assert.NotNull(info);
        Assert.Equal("東京", info!.Title);
        Assert.False(info.AlreadyMember);
        Assert.Equal(new[] { "小王", "小花" }, info.Claimable.Select(c => c.Name).ToArray());
        db.Dispose();
    }

    [Fact]
    public async Task GetJoinInfo_InvalidOrExpiredToken_Null()
    {
        var (db, token) = await SeededTrip();
        Assert.Null(await TripShare.GetJoinInfoAsync(db, "deadbeef", 8, default));

        var trip = await db.Trips.FindAsync(1L);
        trip!.ShareTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        Assert.Null(await TripShare.GetJoinInfoAsync(db, token, 8, default));
        db.Dispose();
    }

    [Fact]
    public async Task GetJoinInfo_OwnerIsAlreadyMember()
    {
        var (db, token) = await SeededTrip();
        var info = await TripShare.GetJoinInfoAsync(db, token, userId: 5, default);
        Assert.True(info!.AlreadyMember);
        db.Dispose();
    }

    [Fact]
    public async Task Claim_Success_CreatesBoundMember()
    {
        var (db, token) = await SeededTrip();
        var r = await TripShare.ClaimAsync(db, token, userId: 8, participantId: 10, default);
        Assert.Equal(ClaimOutcome.Success, r.Outcome);
        Assert.Equal(1, r.TripId);

        var member = await db.TripMembers.SingleAsync(m => m.UserId == 8);
        Assert.Equal(10, member.ParticipantId);
        db.Dispose();
    }

    [Fact]
    public async Task Claim_SameParticipantTwice_SecondFails()
    {
        var (db, token) = await SeededTrip();
        await TripShare.ClaimAsync(db, token, userId: 8, participantId: 10, default);
        var r = await TripShare.ClaimAsync(db, token, userId: 9, participantId: 10, default);
        Assert.Equal(ClaimOutcome.AlreadyClaimed, r.Outcome);
        db.Dispose();
    }

    [Fact]
    public async Task Claim_ParticipantNotInTrip_Fails()
    {
        var (db, token) = await SeededTrip();
        var r = await TripShare.ClaimAsync(db, token, userId: 8, participantId: 999, default);
        Assert.Equal(ClaimOutcome.ParticipantNotFound, r.Outcome);
        db.Dispose();
    }

    [Fact]
    public async Task Claim_ExistingMember_Idempotent()
    {
        var (db, token) = await SeededTrip();
        db.TripMembers.Add(new TripMember { TripId = 1, UserId = 8, ParticipantId = 11 });
        await db.SaveChangesAsync();
        var r = await TripShare.ClaimAsync(db, token, userId: 8, participantId: 10, default);
        Assert.Equal(ClaimOutcome.AlreadyMember, r.Outcome);
        Assert.Equal(1, await db.TripMembers.CountAsync(m => m.UserId == 8));
        db.Dispose();
    }

    [Fact]
    public async Task Claim_InvalidToken_Fails()
    {
        var (db, _) = await SeededTrip();
        var r = await TripShare.ClaimAsync(db, "nope", userId: 8, participantId: 10, default);
        Assert.Equal(ClaimOutcome.InvalidToken, r.Outcome);
        db.Dispose();
    }
}
