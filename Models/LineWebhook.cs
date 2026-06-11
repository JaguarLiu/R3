using System.Text.Json.Serialization;

namespace R3.Models;

public class LineWebhookPayload
{
    [JsonPropertyName("destination")] public string? Destination { get; set; }
    [JsonPropertyName("events")] public List<LineEvent> Events { get; set; } = new();
}

public class LineEvent
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("replyToken")] public string? ReplyToken { get; set; }
    [JsonPropertyName("source")] public LineSource? Source { get; set; }
    [JsonPropertyName("message")] public LineMessage? Message { get; set; }
}

public class LineSource
{
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    [JsonPropertyName("groupId")] public string? GroupId { get; set; }
    [JsonPropertyName("roomId")] public string? RoomId { get; set; }
}

public class LineMessage
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("mention")] public LineMention? Mention { get; set; }
}

public class LineMention
{
    [JsonPropertyName("mentionees")] public List<LineMentionee> Mentionees { get; set; } = new();
}

public class LineMentionee
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("length")] public int Length { get; set; }
    [JsonPropertyName("userId")] public string? UserId { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("isSelf")] public bool? IsSelf { get; set; }
}
