using Bober.Library.Interfaces;

namespace Bober.Library.Contract
{
    public class MessageData : IInputData
    {
        public long UserId { get; set; }
        public string? Text { get; set; }
        public long ChatId { get; set; }
        public int? MessageThreadId { get; set; }
        public int BotMessageId { get; set; }
        public int MessageId { get; set; }
        public ImagineCommands JobType { get; set; }
    }
}
