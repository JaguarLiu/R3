using BudPay.Models;

namespace BudPay.Services;

// Mirrors the web UI's "direct" settlement mode: greedy pairwise match between
// people owed money (creditors, paid > spent) and people who owe (debtors).
public static class SettlementCalculator
{
    public record Transfer(string From, string To, long Amount);

    public static List<Transfer> Compute(IReadOnlyList<string> participants, IReadOnlyList<SplitExpense> expenses)
    {
        if (participants.Count == 0) return new();

        var balances = participants
            .Select(name => new
            {
                Name = name,
                Amount = expenses.Sum(e => e.Payers.GetValueOrDefault(name, 0m))
                       - expenses.Sum(e => e.Splits.GetValueOrDefault(name, 0m))
            })
            .ToList();

        var creditors = balances.Where(b => b.Amount > 0)
            .Select(b => new MutableBalance(b.Name, b.Amount))
            .OrderByDescending(b => b.Amount).ToList();
        var debtors = balances.Where(b => b.Amount < 0)
            .Select(b => new MutableBalance(b.Name, Math.Abs(b.Amount)))
            .OrderByDescending(b => b.Amount).ToList();

        var transfers = new List<Transfer>();
        int debtorIdx = 0, creditorIdx = 0;
        while (debtorIdx < debtors.Count && creditorIdx < creditors.Count)
        {
            var debtor = debtors[debtorIdx];
            var creditor = creditors[creditorIdx];
            var pay = Math.Min(debtor.Amount, creditor.Amount);

            if (pay > 0.5m)
                transfers.Add(new Transfer(debtor.Name, creditor.Name, (long)Math.Round(pay)));

            debtor.Amount -= pay;
            creditor.Amount -= pay;
            if (debtor.Amount <= 0.1m) debtorIdx++;
            if (creditor.Amount <= 0.1m) creditorIdx++;
        }
        return transfers;
    }

    private sealed class MutableBalance(string name, decimal amount)
    {
        public string Name { get; } = name;
        public decimal Amount { get; set; } = amount;
    }
}
