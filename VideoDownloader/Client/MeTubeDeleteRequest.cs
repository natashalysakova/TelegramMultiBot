using System.Text.Json.Serialization;

namespace VideoDownloader.Client;

public record MeTubeDeleteRequest
{
    [JsonPropertyName("where")]
    public required string Where { get; set; }

    [JsonPropertyName("ids")]
    public required string[] Ids { get; set; }
}
