using Bober.Library.Interfaces;

namespace Bober.Library.Contract
{
    public class CallbackData : IInputData
    {
        public long UserId { get; set; }
        public long ChatId { get; set; }
        public int MessageId { get; set; }
        public int? MessageThreadId { get; set; }
        public double? Upscale { get; set; }
        public ImagineCommands JobType { get; set; }
        public Guid PreviousJobResultId { get; set; }
    }
}
