namespace TelegramMultiBot.Database;

public class VideoDownload
{
    public Guid Id { get; set; }
    public string VideoUrl { get; set; }
    public string? DownloadedFilePath { get; set; }
    public string Status { get; set; }

    public long ChatId { get; set; }
    public int MessageThreadId { get; set; }

    public int BotMessage { get; set; }
    public int MessageToDelete { get; set; }
}