using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    public class ReminderJob
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

        public string Name { get; set; }
        public string? Message { get; set; }
        public string Config { get; set; }
        public long ChatId { get; set; }
        public DateTime NextExecution { get; set; }
        public string? FileId { get; internal set; }
    }
}