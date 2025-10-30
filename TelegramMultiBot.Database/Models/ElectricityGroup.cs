using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    public class ElectricityGroup
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public required string LocationRegion { get; set; }
        public required string GroupCode { get; set; }
        public required string GroupName { get; set; }
        public required string? DataSnapshot { get; set; }

        public virtual ICollection<ElectricityHistory> History { get; set; } = new List<ElectricityHistory>();
        public virtual ICollection<MonitorJob> Jobs { get; set; } = new List<MonitorJob>();
    }
}