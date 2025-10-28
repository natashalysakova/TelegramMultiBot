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
        public DateTime LastUpdated
        {
            get
            {
                return History.Any() ? History.Max(x => x.Updated) : DateTime.MinValue;
            }
        }
        public long LastScheduleDay
        {
            get => History.Any() ? History.Max(x => x.ScheduleDay) : 0;
        }

        public virtual ICollection<ElectricityHistory> History { get; set; } = new List<ElectricityHistory>();  
    }
}