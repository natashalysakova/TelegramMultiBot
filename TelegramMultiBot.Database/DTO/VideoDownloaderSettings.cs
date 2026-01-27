namespace TelegramMultiBot.Database.DTO;

public class VideoDownloaderSettings : BaseSetting
{
    public static string Name => "VideoDownloader";

    public string MeTubeUrl { get; set; }
}