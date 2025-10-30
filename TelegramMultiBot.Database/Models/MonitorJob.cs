using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    public class MonitorJob
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public required long ChatId { get; set; }
        public int? MessageThreadId { get; set; }

        public bool IsDtekJob { get; set; }
        public bool IsActive { get; set; } = true;
        public string? DeactivationReason { get; set; }
        public DateTime? LastScheduleUpdate { get; set; }
        public ElectricityJobType Type { get; set; }

        public Guid LocationId { get; set; }
        public virtual ElectricityLocation? Location { get; set; }

        public Guid? GroupId { get; set; }
        public virtual ElectricityGroup? Group { get; set; }
        public string? LastSentGroupSnapsot { get; set; }
    }

    public enum ElectricityJobType
    {
        Unknown = 0,
        AllGroups = 1,
        SingleGroup = 2,
        SingleGroupPlan = 3
    }
}
