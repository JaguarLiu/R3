using R3.Auth;
using R3.Common;
using R3.Data;
using R3.Models;
using R3.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace R3.Endpoints;

public static class TripEndpoints
{
    public static void MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var trips = app.MapGroup("/api/trips").RequireAuthorization();

        trips.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var memberTripIds = db.TripMembers.Where(m => m.UserId == userId).Select(m => m.TripId);
            return Results.Ok(await db.Trips.AsNoTracking()
                .Where(t => t.OwnerUserId == userId || memberTripIds.Contains(t.Id))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.Title, t.Days, t.CreatedAt, isOwner = t.OwnerUserId == userId })
                .ToListAsync(ct));
        });

        trips.MapGet("/{id:long}", async (long id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var trip = await TripAccess.FindAccessibleAsync(db, id, userId.Value, ownerOnly: false, ct);
            if (trip is null) return Results.NotFound();
            var members = await db.TripMembers.Where(m => m.TripId == id)
                .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { u.Id, u.DisplayName, u.Email })
                .ToListAsync(ct);
            return Results.Ok(new { trip.Id, trip.Title, trip.Days, trip.CreatedAt,
                isOwner = trip.OwnerUserId == userId, trip.Participants, trip.Expenses, members });
        });

        trips.MapPost("/", async ([FromBody] TripUpsertDto dto, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var tripValidationError = InputSanitizer.ValidateTrip(dto);
            if (tripValidationError != null) return Results.BadRequest(tripValidationError);

            var trip = new Trip { Title = dto.Title.Trim(), Days = dto.Days, OwnerUserId = userId };
            var names = dto.Participants.Select(n => n.Trim()).Distinct().ToList();
            for (var i = 0; i < names.Count; i++)
                trip.Participants.Add(new Participant { Name = names[i], Order = i, TripId = trip.Id });
            db.Trips.Add(trip);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/trips/{trip.Id}", trip);
        });

        trips.MapPut("/{id:long}", async (long id, [FromBody] TripUpsertDto dto, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var tripValidationError = InputSanitizer.ValidateTrip(dto);
            if (tripValidationError != null) return Results.BadRequest(tripValidationError);

            var trip = await db.Trips.Include(t => t.Participants).FirstOrDefaultAsync(t => t.Id == id, ct);
            if (trip is null || trip.OwnerUserId != userId) return Results.NotFound();
            trip.Title = dto.Title.Trim();
            trip.Days = dto.Days;

            // Sync participants by name (preserve ids for existing names so expenses keep working)
            var existingByName = trip.Participants.ToDictionary(p => p.Name);
            var newSet = dto.Participants.Select(n => n.Trim()).Distinct().ToList();
            var toRemove = trip.Participants.Where(p => !newSet.Contains(p.Name)).ToList();
            db.Participants.RemoveRange(toRemove);
            for (var i = 0; i < newSet.Count; i++)
            {
                var name = newSet[i];
                if (existingByName.TryGetValue(name, out var p)) p.Order = i;
                else trip.Participants.Add(new Participant { Name = name, Order = i, TripId = trip.Id });
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(trip);
        });

        trips.MapDelete("/{id:long}", async (long id, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var trip = await db.Trips.FirstOrDefaultAsync(t => t.Id == id && t.OwnerUserId == userId, ct);
            if (trip is null) return Results.NotFound();
            db.Trips.Remove(trip);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Expenses
        var expenses = app.MapGroup("/api/trips/{tripId:long}/expenses").RequireAuthorization();

        expenses.MapPost("/", async (long tripId, [FromBody] SplitExpenseDto dto,
            ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var expenseValidationError = InputSanitizer.ValidateExpense(dto);
            if (expenseValidationError != null) return Results.BadRequest(expenseValidationError);
            var trip = await TripAccess.FindAccessibleAsync(db, tripId, userId.Value, ownerOnly: false, ct);
            if (trip is null) return Results.NotFound();
            var creator = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            var entry = new SplitExpense
            {
                TripId = tripId, Day = dto.Day.Trim(), Item = dto.Item.Trim(),
                Total = dto.Total, Payers = dto.Payers, Splits = dto.Splits,
                CreatedByUserId = userId, CreatedByName = creator?.DisplayName, SourceChannel = "web",
            };
            db.SplitExpenses.Add(entry);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/trips/{tripId}/expenses/{entry.Id}", entry);
        });

        expenses.MapPut("/{id:long}", async (long tripId, long id, [FromBody] SplitExpenseDto dto,
            ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var expenseValidationError = InputSanitizer.ValidateExpense(dto);
            if (expenseValidationError != null) return Results.BadRequest(expenseValidationError);
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            if (await TripAccess.FindAccessibleAsync(db, tripId, userId.Value, ownerOnly: false, ct) is null)
                return Results.NotFound();
            var entry = await db.SplitExpenses.FirstOrDefaultAsync(e => e.Id == id && e.TripId == tripId, ct);
            if (entry is null) return Results.NotFound();
            entry.Day = dto.Day.Trim();
            entry.Item = dto.Item.Trim();
            entry.Total = dto.Total;
            entry.Payers = dto.Payers;
            entry.Splits = dto.Splits;
            await db.SaveChangesAsync(ct);
            return Results.Ok(entry);
        });

        expenses.MapDelete("/{id:long}", async (long tripId, long id,
            ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            if (await TripAccess.FindAccessibleAsync(db, tripId, userId.Value, ownerOnly: false, ct) is null)
                return Results.NotFound();
            var entry = await db.SplitExpenses.FirstOrDefaultAsync(e => e.Id == id && e.TripId == tripId, ct);
            if (entry is null) return Results.NotFound();
            db.SplitExpenses.Remove(entry);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // AI endpoints (Gemini extracted to backend). Rate-limited per-IP via "ai" policy.
        var ai = app.MapGroup("/api/ai").RequireRateLimiting("ai").RequireAuthorization();

        ai.MapPost("/analyze/{tripId:long}", async (long tripId, ClaimsPrincipal principal, GeminiService gemini, AppDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("R3.Endpoints.AI");
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var trip = await TripAccess.FindAccessibleAsync(db, tripId, userId.Value, ownerOnly: false, ct);
            if (trip is null) return Results.NotFound();

            var summary = trip.Participants.ToDictionary(p => p.Name, p => new
            {
                paid = trip.Expenses.Sum(e => e.Payers.GetValueOrDefault(p.Name, 0m)),
                spent = trip.Expenses.Sum(e => e.Splits.GetValueOrDefault(p.Name, 0m))
            });
            var simple = trip.Expenses.Select(e => new { e.Item, e.Total });

            try
            {
                var text = await gemini.AnalyzeAsync(simple, summary, ct);
                return Results.Ok(new { text });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI analyze failed for trip {TripId}", tripId);
                return Results.Problem(ex.Message);
            }
        });

        // Frontend just sends { text } — backend looks up participants from the trip,
        // calls Gemini, converts items into SplitExpense rows, persists, and returns them.
        ai.MapPost("/parse/{tripId:long}", async (
            long tripId,
            [FromBody] TextMessageDto body,
            ClaimsPrincipal principal,
            GeminiService gemini,
            AppDbContext db,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("R3.Endpoints.AI");
            if (string.IsNullOrWhiteSpace(body?.Text)) return Results.BadRequest(new { error = "text required" });

            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var trip = await TripAccess.FindAccessibleAsync(db, tripId, userId.Value, ownerOnly: false, ct);
            if (trip is null) return Results.NotFound();

            var participants = trip.Participants.OrderBy(p => p.Order).Select(p => p.Name).ToList();

            BatchParseResult parsed;
            try { parsed = await gemini.BatchParseAsync(body.Text, participants, ct); }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI parse failed for trip {TripId}, text length {Len}", tripId, body.Text.Length);
                return Results.Problem(ex.Message);
            }

            if (parsed.UnknownNames.Count > 0)
                return Results.BadRequest(new { error = "unknown_names", names = parsed.UnknownNames });

            var creator = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            var entries = parsed.Items
                .Where(i => i.Total > 0 && !string.IsNullOrWhiteSpace(i.Item))
                .Select(i =>
                {
                    var e = ExpenseBuilder.FromParsedItem(tripId, i, participants);
                    e.CreatedByUserId = userId; e.CreatedByName = creator?.DisplayName; e.SourceChannel = "web";
                    return e;
                })
                .ToList();

            if (entries.Count == 0) return Results.BadRequest(new { error = "no_expenses_parsed" });

            db.SplitExpenses.AddRange(entries);
            await db.SaveChangesAsync(ct);
            return Results.Ok(entries);
        });

        var members = app.MapGroup("/api/trips/{tripId:long}/members").RequireAuthorization();

        members.MapPost("/", async (long tripId, [FromBody] AddMemberDto dto,
            ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            if (await TripAccess.FindAccessibleAsync(db, tripId, userId.Value, ownerOnly: true, ct) is null)
                return Results.NotFound();
            var email = (dto?.Email ?? "").Trim().ToLowerInvariant();
            if (email.Length == 0) return Results.BadRequest(new { error = "email_required" });
            var target = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (target is null) return Results.NotFound(new { error = "user_not_found" });
            if (target.Id == userId) return Results.BadRequest(new { error = "owner_already_member" });
            if (!await db.TripMembers.AnyAsync(m => m.TripId == tripId && m.UserId == target.Id, ct))
            {
                db.TripMembers.Add(new TripMember { TripId = tripId, UserId = target.Id });
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { target.Id, target.DisplayName, target.Email });
        });

        members.MapDelete("/{memberUserId:long}", async (long tripId, long memberUserId,
            ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            if (await TripAccess.FindAccessibleAsync(db, tripId, userId.Value, ownerOnly: true, ct) is null)
                return Results.NotFound();
            var row = await db.TripMembers.FirstOrDefaultAsync(m => m.TripId == tripId && m.UserId == memberUserId, ct);
            if (row is null) return Results.NotFound();
            db.TripMembers.Remove(row);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}

public record TripUpsertDto(string Title, int Days, List<string> Participants);
public record SplitExpenseDto(string Day, string Item, decimal Total, Dictionary<string, decimal> Payers, Dictionary<string, decimal> Splits);
public record TextMessageDto(string Text);
public record AddMemberDto(string Email);

// Validates user-supplied fields that flow into Gemini system prompts.
// Rejects control chars (line breaks, tabs, etc.) so attackers can't break out of
// the prompt's data fences with a newline injection, and caps length to bound abuse.
internal static class InputSanitizer
{
    const int MaxTitle = 100;
    const int MaxName = 30;
    const int MaxItem = 100;
    const int MaxDay = 20;
    const int MaxParticipants = 50;

    public static object? ValidateTrip(TripUpsertDto dto)
    {
        if (dto is null) return new { error = "invalid_body" };

        var titleError = CheckText(dto.Title, MaxTitle, "title");
        if (titleError != null) return titleError;

        if (dto.Days < 1 || dto.Days > 365) return new { error = "invalid_days" };
        if (dto.Participants is null || dto.Participants.Count == 0) return new { error = "participants_required" };
        if (dto.Participants.Count > MaxParticipants) return new { error = "too_many_participants", limit = MaxParticipants };

        foreach (var name in dto.Participants)
        {
            var nameError = CheckText(name, MaxName, "participant");
            if (nameError != null) return nameError;
        }
        return null;
    }

    public static object? ValidateExpense(SplitExpenseDto dto)
    {
        if (dto is null) return new { error = "invalid_body" };

        var itemError = CheckText(dto.Item, MaxItem, "item");
        if (itemError != null) return itemError;

        var dayError = CheckText(dto.Day, MaxDay, "day");
        if (dayError != null) return dayError;

        if (dto.Total <= 0 || dto.Total > 100_000_000m) return new { error = "invalid_total" };

        foreach (var payerName in dto.Payers.Keys)
        {
            var payerError = CheckText(payerName, MaxName, "payer_name");
            if (payerError != null) return payerError;
        }
        foreach (var splitName in dto.Splits.Keys)
        {
            var splitError = CheckText(splitName, MaxName, "split_name");
            if (splitError != null) return splitError;
        }
        return null;
    }

    private static object? CheckText(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return new { error = "empty_field", field };
        var trimmed = value.Trim();
        if (trimmed.Length > max) return new { error = "too_long", field, limit = max };
        if (HasControlChar(trimmed)) return new { error = "invalid_chars", field };
        return null;
    }

    private static bool HasControlChar(string s)
    {
        foreach (var c in s)
        {
            // Block any character that could insert a new line into a Gemini prompt.
            if (char.IsControl(c)) return true;                       // 0x00-0x1F, 0x7F, C1 controls
            if (c == '\u2028' || c == '\u2029') return true;    // Unicode line / paragraph separators
        }
        return false;
    }
}
