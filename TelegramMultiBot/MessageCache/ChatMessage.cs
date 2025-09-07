using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.MessageCache
{
    public record ChatMessage
    {
        public long ChatId { get; init; }
        public int? ThreadId { get; init; } // Может быть null для обычных чатов без тем
        public string Text { get; init; }
        public DateTime Timestamp { get; init; }

        public string UserName { get; set; }

        // Рассчитываем и кэшируем размер сообщения в байтах для производительности.
        public int SizeInBytes { get; }

        public ChatMessage(long chatId, int? threadId, string text, string userName)
        {
            ChatId = chatId;
            ThreadId = threadId;
            Text = text;
            Timestamp = DateTime.UtcNow;
            SizeInBytes = Encoding.UTF8.GetByteCount(Text ?? string.Empty);
            UserName = userName;
        }
    }
}
