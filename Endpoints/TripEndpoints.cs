using BudPay.Data;
using BudPay.Models;
using BudPay.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudPay.Endpoints;

public static class TripEndpoints
{
    public static void MapTripEndpoints(this IEndpointRouteBuilder app)
    {
        var trips = app.MapGroup("/api/trips");

        trips.MapGet("/", async (AppDbContext db) =>
            await db.Trips.AsNoTracking()
                .Select(t => new { t.Id, t.Title, t.Days, t.CreatedAt })
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync());

        trips.MapGet("/{id:long}", async (long id, AppDbContext db) =>
        {
            var trip = await db.Trips.AsNoTracking()
                .Include(t => t.Participants.OrderBy(p => p.Order))
                .Include(t => t.Expenses.OrderBy(e => e.CreatedAt))
                .FirstOrDefaultAsync(t => t.Id == id);
            return trip is null ? Results.NotFound() : Results.Ok(trip);
        });

        trips.MapPost("/", async ([FromBody] TripUpsertDto dto, AppDbContext db) =>
        {
            var tripValidationError = InputSanitizer.ValidateTrip(dto);
            if (tripValidationError != null) return Results.BadRequest(tripValidationError);

            var trip = new Trip { Title = dto.Title.Trim(), Days = dto.Days };
            var names = dto.Participants.Select(n => n.Trim()).Distinct().ToList();
            for (var i = 0; i < names.Count; i++)
                trip.Participants.Add(new Participant { Name = names[i], Order = i, TripId = trip.Id });
            db.Trips.Add(trip);
            await db.SaveChangesAsync();
            return Results.Created($"/api/trips/{trip.Id}", trip);
        });

        trips.MapPut("/{id:long}", async (long id, [FromBody] TripUpsertDto dto, AppDbContext db) =>
        {
            var tripValidationError = InputSanitizer.ValidateTrip(dto);
            if (tripValidationError != null) return Results.BadRequest(tripValidationError);

            var trip = await db.Trips.Include(t => t.Participants).FirstOrDefaultAsync(t => t.Id == id);
            if (trip is null) return Results.NotFound();
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
            await db.SaveChangesAsync();
            return Results.Ok(trip);
        });

        trips.MapDelete("/{id:long}", async (long id, AppDbContext db) =>
        {
            var trip = await db.Trips.FindAsync(id);
            if (trip is null) return Results.NotFound();
            db.Trips.Remove(trip);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Expenses
        var expenses = app.MapGroup("/api/trips/{tripId:long}/expenses");

        expenses.MapPost("/", async (long tripId, [FromBody] SplitExpenseDto dto, AppDbContext db) =>
        {
            var expenseValidationError = InputSanitizer.ValidateExpense(dto);
            if (expenseValidationError != null) return Results.BadRequest(expenseValidationError);
            if (!await db.Trips.AnyAsync(t => t.Id == tripId)) return Results.NotFound();
            var entry = new SplitExpense
            {
                TripId = tripId,
                Day = dto.Day.Trim(),
                Item = dto.Item.Trim(),
                Total = dto.Total,
                Payers = dto.Payers,
                Splits = dto.Splits
            };
            db.SplitExpenses.Add(entry);
            await db.SaveChangesAsync();
            return Results.Created($"/api/trips/{tripId}/expenses/{entry.Id}", entry);
        });

        expenses.MapPut("/{id:long}", async (long tripId, long id, [FromBody] SplitExpenseDto dto, AppDbContext db) =>
        {
            var expenseValidationError = InputSanitizer.ValidateExpense(dto);
            if (expenseValidationError != null) return Results.BadRequest(expenseValidationError);
            var entry = await db.SplitExpenses.FirstOrDefaultAsync(e => e.Id == id && e.TripId == tripId);
            if (entry is null) return Results.NotFound();
            entry.Day = dto.Day.Trim();
            entry.Item = dto.Item.Trim();
            entry.Total = dto.Total;
            entry.Payers = dto.Payers;
            entry.Splits = dto.Splits;
            await db.SaveChangesAsync();
            return Results.Ok(entry);
        });

        expenses.MapDelete("/{id:long}", async (long tripId, long id, AppDbContext db) =>
        {
            var entry = await db.SplitExpenses.FirstOrDefaultAsync(e => e.Id == id && e.TripId == tripId);
            if (entry is null) return Results.NotFound();
            db.SplitExpenses.Remove(entry);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // AI endpoints (Gemini extracted to backend). Rate-limited per-IP via "ai" policy.
        var ai = app.MapGroup("/api/ai").RequireRateLimiting("ai");

        ai.MapPost("/analyze/{tripId:long}", async (long tripId, GeminiService gemini, AppDbContext db, CancellationToken ct) =>
        {
            var trip = await db.Trips.AsNoTracking()
                .Include(t => t.Participants)
                .Include(t => t.Expenses)
                .FirstOrDefaultAsync(t => t.Id == tripId, ct);
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
                return Results.Problem(ex.Message);
            }
        });

        // Frontend just sends { text } — backend looks up participants from the trip,
        // calls Gemini, converts items into SplitExpense rows, persists, and returns them.
        ai.MapPost("/parse/{tripId:long}", async (
            long tripId,
            [FromBody] TextMessageDto body,
            GeminiService gemini,
            AppDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body?.Text)) return Results.BadRequest(new { error = "text required" });

            var trip = await db.Trips
                .Include(t => t.Participants.OrderBy(p => p.Order))
                .FirstOrDefaultAsync(t => t.Id == tripId, ct);
            if (trip is null) return Results.NotFound();

            var participants = trip.Participants.Select(p => p.Name).ToList();

            BatchParseResult parsed;
            try { parsed = await gemini.BatchParseAsync(body.Text, participants, ct); }
            catch (Exception ex) { return Results.Problem(ex.Message); }

            if (parsed.UnknownNames.Count > 0)
                return Results.BadRequest(new { error = "unknown_names", names = parsed.UnknownNames });

            var entries = parsed.Items
                .Where(i => i.Total > 0 && !string.IsNullOrWhiteSpace(i.Item))
                .Select(i => ExpenseBuilder.FromParsedItem(tripId, i, participants))
                .ToList();

            if (entries.Count == 0) return Results.BadRequest(new { error = "no_expenses_parsed" });

            db.SplitExpenses.AddRange(entries);
            await db.SaveChangesAsync(ct);
            return Results.Ok(entries);
        });
    }
}

public record TripUpsertDto(string Title, int Days, List<string> Participants);
public record SplitExpenseDto(string Day, string Item, decimal Total, Dictionary<string, decimal> Payers, Dictionary<string, decimal> Splits);
public record TextMessageDto(string Text);

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
