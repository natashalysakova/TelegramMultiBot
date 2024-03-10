using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Database.DTO;

public class CallbackData : IInputData
{
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public int MessageId { get; set; }
    public int? MessageThreadId { get; set; }
    public double? Upscale { get; set; }
    public JobType JobType { get; set; }
    public Guid PreviousJobResultId { get; set; }
    public int BotMessageId { get; set ; }
}
