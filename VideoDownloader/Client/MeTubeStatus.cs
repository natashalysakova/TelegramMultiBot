using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace VideoDownloader.Client;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MeTubeStatus
{
    [EnumMember(Value = "ok")]
    Ok,
    [EnumMember(Value = "error")]
    Error
}
