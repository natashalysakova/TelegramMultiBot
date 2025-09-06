namespace TelegramMultiBot.MessageCache
{
    /// <summary>
    /// Сервис для хранения сообщений в памяти с ограничением по общему объему.
    /// При превышении лимита самые старые сообщения удаляются.
    /// </summary>
    public class InMemoryMessageCacheService : IMessageCacheService
    {
        // Максимальный общий размер кэша в байтах.
        private const int MaxTotalSizeInBytes = 1024;

        // Потокобезопасность обеспечивается блокировкой этого объекта.
        private readonly object _lock = new object();

        // Хранит все сообщения в хронологическом порядке для быстройго удаления старых.
        private readonly LinkedList<ChatMessage> _chronologicalMessages = new();

        // Хранит сообщения, сгруппированные по ключу "ChatId:ThreadId" для быстрого доступа.
        private readonly Dictionary<string, LinkedList<ChatMessage>> _messagesByChat = new();

        private int _currentTotalSize = 0;

        public void AddMessage(ChatMessage message)
        {
            lock (_lock)
            {
                // Добавляем новое сообщение в конец обеих коллекций.
                _chronologicalMessages.AddLast(message);

                var chatKey = GetChatKey(message.ChatId, message.ThreadId);
                if (!_messagesByChat.ContainsKey(chatKey))
                {
                    _messagesByChat[chatKey] = new LinkedList<ChatMessage>();
                }
                _messagesByChat[chatKey].AddLast(message);

                _currentTotalSize += message.SizeInBytes;

                // Применяем политику вытеснения: удаляем самые старые сообщения,
                // пока общий размер не станет меньше максимального.
                while (_currentTotalSize > MaxTotalSizeInBytes && _chronologicalMessages.Any())
                {
                    EvictOldestMessage();
                }
            }
        }

        public IEnumerable<ChatMessage> GetContextForChat(long chatId, int? threadId)
        {
            lock (_lock)
            {
                var chatKey = GetChatKey(chatId, threadId);
                if (_messagesByChat.TryGetValue(chatKey, out var messages))
                {
                    // Возвращаем копию, чтобы избежать изменения коллекции извне.
                    return messages.ToList();
                }
                return Enumerable.Empty<ChatMessage>();
            }
        }

        private void EvictOldestMessage()
        {
            // Получаем самое старое сообщение (первый элемент в списке).
            var oldestMessage = _chronologicalMessages.First?.Value;
            if (oldestMessage == null) return;

            // Удаляем его из хронологического списка.
            _chronologicalMessages.RemoveFirst();

            // Находим и удаляем его из списка для конкретного чата.
            var chatKey = GetChatKey(oldestMessage.ChatId, oldestMessage.ThreadId);
            if (_messagesByChat.TryGetValue(chatKey, out var chatMessages))
            {
                chatMessages.Remove(oldestMessage);
                // Если для чата не осталось сообщений, удаляем саму запись из словаря.
                if (!chatMessages.Any())
                {
                    _messagesByChat.Remove(chatKey);
                }
            }

            // Уменьшаем текущий размер кэша.
            _currentTotalSize -= oldestMessage.SizeInBytes;
        }

        /// <summary>
        /// Создает уникальный ключ для чата и темы.
        /// </summary>
        private static string GetChatKey(long chatId, int? threadId)
        {
            // threadId может быть 0, поэтому проверяем на null.
            return threadId.HasValue ? $"{chatId}:{threadId.Value}" : chatId.ToString();
        }
    }
}
