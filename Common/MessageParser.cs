using System.Globalization;
using System.Text.RegularExpressions;
using R3.Models;

namespace R3.Common;


public static partial class MessageParser
{
    // Match: 100 / 100.5 / 1,000 with optional currency hints (元/塊/塊錢/NT$/$)
    [GeneratedRegex(@"(?<num>\d{1,3}(?:,\d{3})+|\d+(?:\.\d+)?)\s*(?:元|塊錢|塊|圓|NT\$?|\$)?", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    // Strip common filler so the remainder reads as a note
    private static readonly Regex FillerRegex = new(
        @"幫我|請|麻煩|把這|這個|這筆|這|那筆|那|花了|花費|付了|付|買了|買|記錄一下|記錄下來|記錄|記一下|記帳|記|下來|一下|金額",
        RegexOptions.Compiled);

    public static ParseResult? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = AmountRegex().Match(text);
        if (!match.Success) return null;

        var raw = match.Groups["num"].Value.Replace(",", "");
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return null;
        if (amount <= 0) return null;

        // Note = original text minus the matched amount span, minus filler words
        var withoutAmount = text.Remove(match.Index, match.Length);
        var note = FillerRegex.Replace(withoutAmount, " ");
        note = Regex.Replace(note, @"\s+", " ").Trim(' ', '，', ',', '。', '!', '?', '、');

        return new ParseResult(amount, string.IsNullOrWhiteSpace(note) ? null : note);
    }
}
