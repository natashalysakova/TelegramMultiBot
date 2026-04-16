using System.Text.Json.Serialization;

namespace VideoDownloader.Client;

public record MeTubeHistoryItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("ext")]
    public string? Ext { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = 0;

    [JsonPropertyName("msg")]
    public string? Message { get; set; }
}
