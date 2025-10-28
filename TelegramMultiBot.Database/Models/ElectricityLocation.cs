using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    public class ElectricityLocation
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public required string Url { get; set; }
        public required string Location { get; set; }
        public DateTime LastChecked { get; set; }
        public DateTime LastUpdated { get => History.Max(x => x.Updated); }
        public long LastScheduleDay { get => History.Max(x => x.ScheduleDay); }

        public virtual ICollection<ElectricityHistory> History { get; set; } = new List<ElectricityHistory>();  
    }
}