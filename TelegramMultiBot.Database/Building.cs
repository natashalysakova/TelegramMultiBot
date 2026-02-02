namespace TelegramMultiBot.Database;

public class Building
{
    public Guid Id { get; set; }
    public Guid StreetId { get; set; }
    public virtual Street? Street { get; set; }
    public required string Number { get; set; }
    public ICollection<string> GroupNames { get; set; } = new List<string>();
}
