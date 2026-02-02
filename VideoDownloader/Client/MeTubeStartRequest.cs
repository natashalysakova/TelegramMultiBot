using System.Text.Json.Serialization;

namespace VideoDownloader.Client;

public record MeTubeStartRequest
{
    [JsonPropertyName("ids")]
    public required string[] Ids { get; set; }
}
