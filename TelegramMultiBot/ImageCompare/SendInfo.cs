namespace TelegramMultiBot.ImageCompare;

public class SendInfo
{
    public List<string> Filenames { get; set; } = new List<string>();
    public string Caption { get; set; }
    public long ChatId { get; set; }
    public int? MessageThreadId { get; set; }
}
