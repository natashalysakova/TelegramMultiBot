using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database;

public class SvitlobotData
{
    public Guid Id { get; set; }
    public string SvitlobotKey { get; set; }
    public Guid GroupId { get; set; }
    public virtual ElectricityGroup Group { get; set; }
}