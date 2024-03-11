namespace TelegramMultiBot.Database.DTO;

public class JobResultInfoView
{ 
    public required string Id { get; set; }
    public long Seed { get; set; }
    public string? Info { get; set; }
    public double RenderTime { get; set; }
    public required string FilePath { get; set; }
}

public class JobResultInfoCreate
{
    public string? Info { get; set; }
    public double RenderTime { get; set; }
    public required string FilePath { get; set; }
}