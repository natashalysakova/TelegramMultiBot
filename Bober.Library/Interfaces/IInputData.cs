namespace Bober.Library.Interfaces
{
    public interface IInputData
    {
        public long UserId { get; set; }
        public long ChatId { get; set; }
        public int MessageId { get; set; }
        public int? MessageThreadId { get; set; }
        public ImagineCommands JobType { get; set; }


    }
}