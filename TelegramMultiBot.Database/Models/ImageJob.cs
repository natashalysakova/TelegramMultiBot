using System.ComponentModel.DataAnnotations.Schema;

public class ImageJob
{
    public ImageJob()
    {
        Created = DateTime.Now;
        BotMessageId = -1;
    }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public DateTime Created { get; set; }
    public DateTime Started { get; set; }
    public DateTime Finised { get; set; }
    public ImageJobStatus Status { get; set; }

    public long UserId { get; set; }
    public virtual ICollection<JobResult> Results { get; set; } = new List<JobResult>();
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public int? MessageThreadId { get; set; }
    public string? Text { get; set; }
    public int BotMessageId { get; set; }
    public bool PostInfo { get; set; }
    public  JobType Type { get; set; }

    public Guid? PreviousJobResultId { get; set; }
    public double? UpscaleModifyer { get; set; }
    public double Progress { get; internal set; }
    public string TextStatus { get; internal set; }
}

