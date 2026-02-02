using System.Text.Json.Serialization;

namespace VideoDownloader.Client;

public record MeTubeHistoryResponse
{
    [JsonPropertyName("done")]
    public MeTubeHistoryItem[] Done { get; set; } = [];

    [JsonPropertyName("queue")]
    public MeTubeHistoryItem[] Queue { get; set; } = [];

    [JsonPropertyName("pending")]
    public MeTubeHistoryItem[] Pending { get; set; } = [];
}
