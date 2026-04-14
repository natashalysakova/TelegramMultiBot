using System.ComponentModel;
using System.Reflection;

namespace TelegramMultiBot.Database.DTO;

public class VideoDownloaderSettings : BaseSetting
{
    public static string Name => "VideoDownloader";

    public string MeTubeUrl { get; set; } = "http://metube:8081";

    public int PollingIntervalSeconds { get; set; } = 15;

    public VideoQuality VideoQuality { get; set; } = VideoQuality.best;
    public VideoFormat VideoFormat { get; set; } = VideoFormat.iosCompatible;
    public VideoCodec VideoCodec { get; set; } = VideoCodec.h265;
}

public enum VideoQuality
{
    [Description("best")]
    best,
    [Description("worst")]
    worst,
    [Description("2160p")]
    p2160,
    [Description("1440p")]
    p1440,
    [Description("1080p")]
    p1080,
    [Description("720p")]
    p720,
    [Description("480p")]
    p480,
    [Description("360p")]
    p360,
    [Description("240p")]
    p240,
}

public enum VideoFormat
{
    [Description("mp4")]
    mp4,
    [Description("any")]
    auto,
    [Description("ios")]
    iosCompatible,
}

public enum VideoCodec
{
    [Description("auto")]
    auto,
    [Description("h264")]
    h264,
    [Description("h265")]
    h265,
    [Description("av1")]
    av1,
    [Description("vp9")]
    vp9,
}

public static class VideoDownloaderEnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        return value.GetType()
            .GetField(value.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description ?? value.ToString();
    }
}