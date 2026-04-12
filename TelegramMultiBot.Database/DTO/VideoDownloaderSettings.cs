namespace TelegramMultiBot.Database.DTO;

public class VideoDownloaderSettings : BaseSetting
{
    public static string Name => "VideoDownloader";

    public string MeTubeUrl { get; set; } = "http://metube:8081";

    public int PollingIntervalSeconds { get; set; } = 15;
}