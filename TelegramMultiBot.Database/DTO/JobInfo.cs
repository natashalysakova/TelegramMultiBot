using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.Database.DTO;

public class JobInfo
{
    public long ChatId { get; set; }
    public int? MessageThreadId { get; set; }
    public int BotMessageId { get; set; }
    public int MessageId { get; set; }

    public ICollection<JobResultInfoView> Results { get; set; } = new List<JobResultInfoView>();
    public bool PostInfo { get; set; }
    public JobType Type { get; set; }
    public double? UpscaleModifyer { get; set; }
    public required string Id { get; set; }
    public string? PreviousJobResultId { get; set; }
    public string? Text { get; set; }
    public ExceptionInfo? Exception { get; set; }
    public double Progress { get; set; }
    public required string TextStatus { get; set; }
    public ImageJobStatus Status { get; set; }
    public string? Diffusor { get; set; }
}

public record ExceptionInfo(string Type, string ErrorMessage);
