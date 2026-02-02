using System.Text.Json.Serialization;

namespace VideoDownloader.Client;

public record MeTubeAddRequest
{
    // "url": "https://www.youtube.com/shorts/bxOLkxR6_6cbn",
    // "quality": "1080",
    // "format": "mp4",
    // "folder": "qwe",
    // "custom_name_prefix": "bober",
    // "playlist_item_limit": 1,
    // "auto_start": true,
    // "split_by_chapters": false,
    // "chapter_template": "%(title)s - %(section_number)02d - %(section_title)s.%(ext)s"

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// best, best_ios, worst, 2160, 1440,1080, 720, 480, 360, 240, audio
    /// </summary>
    [JsonPropertyName("quality")]
    public required string Quality { get; set; } = "best";

    /// <summary>
    /// any, mp4, mp3, m4a, wav, flac, opus, thumbnail
    /// </summary>
    [JsonPropertyName("format")]
    public required string Format { get; set; }

    [JsonPropertyName("auto_start")]
    public bool AutoStart { get; set; } = true;

    [JsonPropertyName("split_by_chapters")]
    public bool SplitByChapters { get; set; } = false;
    [JsonPropertyName("chapter_template")]
    public string? ChapterTemplate { get; set; }

    [JsonPropertyName("folder")]
    public string? Folder { get; set; }

    [JsonPropertyName("custom_name_prefix")]
    public string? CustomNamePrefix { get; set; }

    [JsonPropertyName("playlist_item_limit")]
    public int? PlaylistItemLimit { get; set; }
}
