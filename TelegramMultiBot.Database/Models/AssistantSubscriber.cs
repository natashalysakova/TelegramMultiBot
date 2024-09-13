using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TelegramMultiBot.Database.Models
{
    public class AssistantSubscriber
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public long ChatId { get; set; }
        public bool IsActive { get; set; }
        public int? MessageThreadId { get; set; }

        public virtual ICollection<ChatHistory>? ChatHistory { get; set; }
    }

    public class ChatHistory
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int AssistantId { get; set; }
        public virtual required AssistantSubscriber Assistant { get; set; }

        public required int MessageId { get; set; }
        public required string Author { get; set; }
        public string? Text { get; set; }
        public bool HasLink { get; set; }
        public bool HasPhoto { get; set; }
        public bool HasVideo { get; set; }
        public string? RepostedFrom { get; set; }
        public DateTime SendTime { get; set; }
    }
}
