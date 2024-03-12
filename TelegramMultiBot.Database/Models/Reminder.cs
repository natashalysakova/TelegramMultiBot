using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    public class Reminder
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; }

        public required string Name { get; set; }
        public required string Message { get; set; }
        public required string Config { get; set; }
        public long ChatId { get; }
    }
}