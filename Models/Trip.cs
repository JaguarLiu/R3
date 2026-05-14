namespace BudPay.Models;

public class Trip
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public int Days { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // LINE binding: when this trip was created via the bot, holds the group/room/user
    // scope it belongs to. Null for trips created in the web UI only.
    public string? LineGroupId { get; set; }
    // Per-LineGroupId, at most one trip is active at a time — that's where new
    // /記帳 messages from that group get appended.
    public bool IsActive { get; set; }

    public List<Participant> Participants { get; set; } = new();
    public List<SplitExpense> Expenses { get; set; } = new();
}

public class Participant
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public string Name { get; set; } = "";
    public int Order { get; set; }
}

public class SplitExpense
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public string Day { get; set; } = "";
    public string Item { get; set; } = "";
    public decimal Total { get; set; }

    // Stored as jsonb: { "name": amount }
    public Dictionary<string, decimal> Payers { get; set; } = new();
    public Dictionary<string, decimal> Splits { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
