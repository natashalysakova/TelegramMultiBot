using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    public class BotMessage
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }
        public long ChatId { get; set; }
        public int MessageId { get; set; }
        public DateTime SendTime { get; set; }
        public bool IsPrivateChat { get; set; }
        public long? UserId { get; set; }
    }
}
