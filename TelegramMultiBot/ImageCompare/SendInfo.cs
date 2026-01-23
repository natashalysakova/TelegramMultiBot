namespace TelegramMultiBot.ImageCompare;

public class SendInfo
{
    public BotMessageType Type { get; set; }
    public List<string> Filenames { get; set; } = new List<string>();
    public string Caption { get; set; }
    public long ChatId { get; set; }
    public int? MessageThreadId { get; set; }
}

public enum BotMessageType
{
    Unknown = 0,
    Alert = 1,
}
