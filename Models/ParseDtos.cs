using System.Text.Json.Serialization;

namespace R3.Models;

// DTOs produced by the parsers (Gemini NL parsing + the regex fallback).
// Kept in Models because they are pure data contracts with no behaviour.

// Result of Gemini batch NL parsing: parsed expense items + any names not in the trip whitelist.
public class BatchParseResult
{
    [JsonPropertyName("items")]
    public List<BatchParseItem> Items { get; set; } = new();

    [JsonPropertyName("unknown_names")]
    public List<string> UnknownNames { get; set; } = new();
}

public class BatchParseItem
{
    [JsonPropertyName("day")] public string Day { get; set; } = "第 1 天";
    [JsonPropertyName("item")] public string Item { get; set; } = "";
    [JsonPropertyName("total")] public decimal Total { get; set; }
    [JsonPropertyName("singlePayer")] public string? SinglePayer { get; set; }
    [JsonPropertyName("isMultiPayer")] public bool IsMultiPayer { get; set; }
    [JsonPropertyName("multiPayers")] public Dictionary<string, decimal> MultiPayers { get; set; } = new();
    [JsonPropertyName("isCustomSplit")] public bool IsCustomSplit { get; set; }
    [JsonPropertyName("customSplits")] public Dictionary<string, decimal> CustomSplits { get; set; } = new();
    [JsonPropertyName("selectedForSplit")] public List<string> SelectedForSplit { get; set; } = new();
}

// Result of the regex fallback parser (MessageParser): a single amount + optional note.
public record ParseResult(decimal Amount, string? Note);
