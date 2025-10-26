using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.Database.Models
{
    public class MonitorJob
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public required long ChatId { get; set; }
        public required string Url { get; set; }
        public bool IsDtekJob { get; set; }
        public bool IsActive { get; set; } = true;
        public string? DeactivationReason { get; set; }
        public DateTime NextRun { get; set; }
        public DateTime? LastScheduleUpdate { get; set; }

    }
}
