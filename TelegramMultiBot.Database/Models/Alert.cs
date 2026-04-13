namespace TelegramMultiBot.Database.Models
{
    public class Alert
    {
        public Guid Id { get; set; }
        public Guid LocationId { get; set; }
        public virtual ElectricityLocation Location { get; set; } = null!;
        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }
        public bool AlertSent { get; set; }
        public DateTimeOffset? SentAt { get; set; }
        public string? AlertMessage { get; set; }
        public int FailureCount { get; set; } = 1;
    }
}
