using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database;

public class City
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public virtual ElectricityLocation? Location { get; set; }
    public string Name { get; set; }
    public virtual ICollection<Street> Streets { get; set; } = new List<Street>();
}
