using BudPay.Models;

namespace BudPay.Services;

// Pure helper that turns a Gemini-parsed item into a SplitExpense row,
// applying the same payer/split rules used by the web UI: even split with
// remainder pushed to the first selected participants, sender as fallback payer.
public static class ExpenseBuilder
{
    public static SplitExpense FromParsedItem(long tripId, BatchParseItem item, List<string> participants, string? fallbackPayer = null)
    {
        var defaultPayer = fallbackPayer ?? participants.FirstOrDefault() ?? "";

        var payers = item.IsMultiPayer && item.MultiPayers.Count > 0
            ? item.MultiPayers
            : new Dictionary<string, decimal> { [item.SinglePayer ?? defaultPayer] = item.Total };

        Dictionary<string, decimal> splits;
        if (item.IsCustomSplit && item.CustomSplits.Count > 0)
        {
            splits = participants.ToDictionary(p => p, p => item.CustomSplits.GetValueOrDefault(p, 0m));
        }
        else
        {
            var selected = (item.SelectedForSplit.Count > 0 ? item.SelectedForSplit : participants)
                .Where(participants.Contains).ToList();
            if (selected.Count == 0) selected = participants;

            var totalInt = (long)item.Total;
            var n = selected.Count;
            var avg = totalInt / n;
            var rem = (int)(totalInt % n);
            splits = participants.ToDictionary(p => p, p =>
            {
                var idx = selected.IndexOf(p);
                if (idx < 0) return 0m;
                return (decimal)(avg + (idx < rem ? 1 : 0));
            });
        }

        return new SplitExpense
        {
            TripId = tripId,
            Day = string.IsNullOrWhiteSpace(item.Day) ? "第 1 天" : item.Day,
            Item = item.Item.Trim(),
            Total = item.Total,
            Payers = payers,
            Splits = splits,
        };
    }
}
