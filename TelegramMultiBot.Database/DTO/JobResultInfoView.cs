namespace TelegramMultiBot.Database.DTO;

public class JobResultInfoView
{
    public string Id { get; set; }
    public long Seed { get; set; }
    public string? Info { get; set; }
    public double RenderTime { get; set; }
    public string FilePath { get; set; }
    public string? FileId { get; set; }
}
