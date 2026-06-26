using System.Security.Cryptography;
using R3.Data;
using R3.Models;
using Microsoft.EntityFrameworkCore;

namespace R3.Auth;

// 分享連結 / 加入行程的核心邏輯。與 TripAccess 同模式：純邏輯、吃 AppDbContext、可用 InMemory 單測。
public static class TripShare
{
    public static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    // Owner-only：建立或重置分享連結（覆蓋舊 token + 重設過期）。非 owner / 行程不存在回 null。
    public static async Task<(string token, DateTime expiresAt)?> ResetShareAsync(
        AppDbContext db, long tripId, long ownerUserId, int linkDays, CancellationToken ct)
    {
        var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == tripId && t.OwnerUserId == ownerUserId, ct);
        if (trip is null) return null;
        var token = GenerateToken();
        var expires = DateTime.UtcNow.AddDays(linkDays);
        trip.ShareToken = token;
        trip.ShareTokenExpiresAt = expires;
        await db.SaveChangesAsync(ct);
        return (token, expires);
    }

    // 依 token 找有效（未過期）行程，含 Participants。無效/過期回 null。
    public static async Task<Trip?> FindByValidTokenAsync(AppDbContext db, string token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var trip = await db.Trips.Include(t => t.Participants.OrderBy(p => p.Order))
            .FirstOrDefaultAsync(t => t.ShareToken == token, ct);
        if (trip is null || trip.ShareTokenExpiresAt is null || trip.ShareTokenExpiresAt <= DateTime.UtcNow)
            return null;
        return trip;
    }

    // 回傳行程標題 + 尚未被認領的 participants + 呼叫者是否已是成員。token 無效回 null。
    public static async Task<JoinInfo?> GetJoinInfoAsync(AppDbContext db, string token, long userId, CancellationToken ct)
    {
        var trip = await FindByValidTokenAsync(db, token, ct);
        if (trip is null) return null;

        var alreadyMember = trip.OwnerUserId == userId
            || await db.TripMembers.AnyAsync(m => m.TripId == trip.Id && m.UserId == userId, ct);

        var claimedIds = await db.TripMembers
            .Where(m => m.TripId == trip.Id && m.ParticipantId != null)
            .Select(m => m.ParticipantId!.Value)
            .ToListAsync(ct);

        var claimable = trip.Participants
            .Where(p => !claimedIds.Contains(p.Id))
            .Select(p => new ClaimableParticipant(p.Id, p.Name))
            .ToList();

        return new JoinInfo(trip.Id, trip.Title, claimable, alreadyMember);
    }

    // 認領一個未被佔用的 participant 成為成員。程式內查重為主，DB unique index 為併發最後防線。
    public static async Task<ClaimResult> ClaimAsync(
        AppDbContext db, string token, long userId, long participantId, CancellationToken ct)
    {
        var trip = await FindByValidTokenAsync(db, token, ct);
        if (trip is null) return new ClaimResult(ClaimOutcome.InvalidToken, 0);

        if (trip.OwnerUserId == userId
            || await db.TripMembers.AnyAsync(m => m.TripId == trip.Id && m.UserId == userId, ct))
            return new ClaimResult(ClaimOutcome.AlreadyMember, trip.Id);

        if (trip.Participants.All(p => p.Id != participantId))
            return new ClaimResult(ClaimOutcome.ParticipantNotFound, trip.Id);

        if (await db.TripMembers.AnyAsync(m => m.TripId == trip.Id && m.ParticipantId == participantId, ct))
            return new ClaimResult(ClaimOutcome.AlreadyClaimed, trip.Id);

        db.TripMembers.Add(new TripMember { TripId = trip.Id, UserId = userId, ParticipantId = participantId });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return new ClaimResult(ClaimOutcome.AlreadyClaimed, trip.Id); } // race: unique index
        return new ClaimResult(ClaimOutcome.Success, trip.Id);
    }
}
