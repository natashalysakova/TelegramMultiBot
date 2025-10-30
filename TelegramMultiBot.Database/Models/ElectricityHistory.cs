using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    public class ElectricityHistory
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public DateTime Updated { get; set; }
        public long ScheduleDay { get; set; }
        public required string ImagePath { get; set; }
        public ElectricityJobType JobType { get; set; }

        public Guid LocationId { get; set; }
        public virtual ElectricityLocation? Location { get; set; }


        public Guid? GroupId { get; set; }
        public virtual ElectricityGroup? Group { get; set; }
    }
}