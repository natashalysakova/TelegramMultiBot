using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Database.DTO
{
    public class MessageData : IInputData
    {
        public long UserId { get; set; }
        public string Text { get; set; }
        public long ChatId { get; set; }
        public int? MessageThreadId { get; set; }
        public int BotMessageId { get; set; }
        public int MessageId { get; set; }
        public JobType JobType { get; set; }
    }
}
