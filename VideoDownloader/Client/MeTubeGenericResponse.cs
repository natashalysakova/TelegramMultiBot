using System.Text.Json.Serialization;

namespace VideoDownloader.Client;

public record MeTubeGenericResponse
{
    [JsonPropertyName("status")]
    public MeTubeStatus Status { get; set; }

    [JsonPropertyName("msg")]
    public string ErrorMessage { get; set; }
}
