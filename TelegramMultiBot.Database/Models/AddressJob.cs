namespace TelegramMultiBot.Database.Models;

public class AddressJob
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public virtual ElectricityLocation Location { get; set; }
    public bool IsActive { get; set; }
    public string City { get; set; }
    public string Street { get; set; }
    public string Building { get; set; }

    public string? LastFetchedInfo { get; set; }
    public bool ShouldBeSent { get; set; }
    public long ChatId { get; set; }
    public int? MessageThreadId { get; set; }
    public string Group { get; set; }
}
