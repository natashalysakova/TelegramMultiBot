using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models;

public class RegionConfigSnapshot
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }

    public Guid LocationId { get; set; }
    public virtual ElectricityLocation? Location { get; set; }
    public required string ConfigJson { get; set; }
    public bool IsProcessed { get; set; } = false;
}