using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface IInputData
    {
        public long UserId { get; set; }
        public long ChatId { get; set; }
        public int MessageId { get; set; }
        public int? MessageThreadId { get; set; }
        public JobType JobType { get; set; }
        public int BotMessageId { get; set; }
    }
}