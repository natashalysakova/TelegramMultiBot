namespace TelegramMultiBot.Database;

public class VideoDownload
{
    public Guid Id { get; set; }
    public required string VideoUrl { get; set; }
    public string? DownloadedFilePath { get; set; }
    public VideoDownloadStatus Status { get; set; } = VideoDownloadStatus.Pending;

    public long ChatId { get; set; }
    public int? MessageThreadId { get; set; }

    public int BotMessage { get; set; }
    public int? MessageToDelete { get; set; }
    public required string RequestedBy { get; set; }
    public string? UserComment { get; set; }
}

public enum VideoDownloadStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}