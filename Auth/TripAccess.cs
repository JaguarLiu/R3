using System.Security.Claims;
using R3.Data;
using R3.Models;
using Microsoft.EntityFrameworkCore;

namespace R3.Auth;

public static class TripAccess
{
    public static long? CurrentUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(sub, out var id) ? id : null;
    }

    // Returns the trip (with Participants + Expenses loaded) if the user may access it, else null.
    // ownerOnly restricts to the trip owner.
    public static async Task<Trip?> FindAccessibleAsync(
        AppDbContext db, long tripId, long userId, bool ownerOnly, CancellationToken ct)
    {
        var trip = await db.Trips
            .Include(t => t.Participants.OrderBy(p => p.Order))
            .Include(t => t.Expenses.OrderBy(e => e.CreatedAt))
            .FirstOrDefaultAsync(t => t.Id == tripId, ct);
        if (trip is null) return null;
        if (trip.OwnerUserId == userId) return trip;
        if (ownerOnly) return null;
        var isMember = await db.TripMembers.AnyAsync(m => m.TripId == tripId && m.UserId == userId, ct);
        return isMember ? trip : null;
    }
}
