using System.ComponentModel.DataAnnotations.Schema;
using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.Database.Models;

public class ImageJob
{
    public ImageJob()
    {
        Created = DateTime.Now;
        BotMessageId = -1;
        TextStatus = "empty";
    }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public DateTime Created { get; set; }
    public DateTime Started { get; set; }
    public DateTime Finised { get; set; }
    public ImageJobStatus Status { get; set; }

    public long UserId { get; set; }
    public virtual ICollection<JobResult> Results { get; set; } = [];
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public int? MessageThreadId { get; set; }
    public string? Text { get; set; }
    public int BotMessageId { get; set; }
    public bool PostInfo { get; set; }
    public JobType Type { get; set; }

    public Guid? PreviousJobResultId { get; set; }
    public double? UpscaleModifyer { get; set; }
    public double Progress { get; internal set; }
    public string TextStatus { get; internal set; }
    public string? Diffusor { get; set; }
    public DateTime NextTry { get; internal set; }
}