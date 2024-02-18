using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.Database.DTO;



public class JobInfo
{
    public long ChatId { get; set; }
    public int? MessageThreadId { get; set; }
    public int BotMessageId { get; set; }
    public int MessageId { get; set; }

    public ICollection<JobResultInfo> Results { get; set; }
    public bool PostInfo { get; set; }
    public JobType Type { get; set; }
    public double? UpscaleModifyer { get; set; }
    public string Id { get; set; }
    public string PreviousJobResultId { get; set; }
    public string Text { get; set; }
    public ExceptionInfo Exception { get; set; }
    public double Progress { get; set; }
    public string TextStatus { get; set; }
    public ImageJobStatus Status { get; set; }
}

public record ExceptionInfo(string type, string errorMessage);
