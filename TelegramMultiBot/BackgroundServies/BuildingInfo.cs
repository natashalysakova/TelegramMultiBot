using System.Text.Json.Serialization;

namespace TelegramMultiBot.BackgroundServies;

public class BuildingInfo
{
    [JsonPropertyName("sub_type")]
    public string SubType { get; set; } = string.Empty;

    [JsonPropertyName("start_date")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("end_date")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("sub_type_reason")]
    public List<string> SubTypeReason { get; set; } = new();

    [JsonPropertyName("voluntarily")]
    public object? Voluntarily { get; set; }
}


