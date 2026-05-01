using System.Collections;
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

    internal IEnumerable<string> GetAllIds()
    {
        return Done.Select(x => x.Id)
            .Concat(Queue.Select(x => x.Id))
            .Concat(Pending.Select(x => x.Id));
    }
}
