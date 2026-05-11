namespace BudPay.Models;

public class Expense
{
    public long Id { get; set; }
    public string LineUserId { get; set; } = "";
    public string? LineGroupId { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public string RawText { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
