using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.Database.Models
{
    public class Alert
    {
        public Guid Id { get; set; }
        public Guid LocationId { get; set; }
        public virtual ElectricityLocation Location { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }
        public bool isResolved { get => ResolvedAt != null; }
        public bool AlertSent { get; set; }
        public DateTimeOffset? SentAt { get; set; }
    }
}
