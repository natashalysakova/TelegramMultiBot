using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography.X509Certificates;

namespace TelegramMultiBot.Database.Models;

public class ElectricityLocation
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; set; }
    public required string Url { get; set; }
    public required string Region { get; set; }
    public DateTime LastChecked { get; set; }

    public DateTime LastUpdated { get; set; }

    public long LastScheduleDay
    {
        get => History.Any() ? History.Max(x => x.ScheduleDay) : 0;
    }

    public virtual ICollection<ElectricityHistory> History { get; set; } = new List<ElectricityHistory>();
    public virtual ICollection<MonitorJob> Jobs { get; set; } = new List<MonitorJob>();
    public virtual ICollection<RegionConfigSnapshot> ConfigSnapshots { get; set; } = new List<RegionConfigSnapshot>();
}
