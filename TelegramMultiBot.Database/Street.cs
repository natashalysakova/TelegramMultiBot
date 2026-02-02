namespace TelegramMultiBot.Database;

public class Street
{
    public Guid Id { get; set; }
    public Guid CityId { get; set; }
    public virtual City? City { get; set; }
    public string Name { get; set; }
    public virtual ICollection<Building> Buildings { get; set; } = new List<Building>();
}
