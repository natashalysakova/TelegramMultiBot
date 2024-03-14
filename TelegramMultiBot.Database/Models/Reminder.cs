using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    public class Reminder
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; }

        public string Name { get; set; }
        public string Message { get; set; }
        public string Config { get; set; }
        public long ChatId { get; }
    }
}