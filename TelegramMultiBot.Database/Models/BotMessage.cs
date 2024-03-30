using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
