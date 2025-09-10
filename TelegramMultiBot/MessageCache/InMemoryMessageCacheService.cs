using System.Collections.Generic;
using System.Text;

namespace TelegramMultiBot.MessageCache
{
    /// <summary>
    /// Сервис для хранения сообщений в памяти с ограничением по общему объему.
    /// При превышении лимита самые старые сообщения удаляются.
    /// </summary>
    public class InMemoryMessageCacheService : IMessageCacheService
    {
        private readonly object _lock = new object();

        private readonly Dictionary<string, ChatContext> _messagesByChat = new();

        public void AddMessage(long chatId, int? threadId, ChatMessage message)
        {
            lock (_lock)
            {
                var chatKey = GetChatKey(chatId, threadId);
                if (!_messagesByChat.ContainsKey(chatKey))
                {
                    _messagesByChat[chatKey] = new ChatContext(chatKey);
                }
                _messagesByChat[chatKey].AddLast(message);

            }
        }

        public ChatContext GetContextForChat(long chatId, int? threadId)
        {
            lock (_lock)
            {
                var chatKey = GetChatKey(chatId, threadId);
                if (_messagesByChat.TryGetValue(chatKey, out var context))
                {
                    return context;
                }

                var newContext = new ChatContext(chatKey);
                _messagesByChat[chatKey] = newContext;
                return newContext;
            }
        }

        private static string GetChatKey(long chatId, int? threadId)
        {
            return threadId.HasValue ? $"{chatId}:{threadId.Value}" : chatId.ToString();
        }

        public void RemoveContext(long chatId, int? messageThreadId)
        {
            lock (_lock)
            {
                var chatKey = GetChatKey(chatId, messageThreadId);
                
                if (_messagesByChat.TryGetValue(chatKey, out var context))
                {
                    _messagesByChat.Remove(chatKey);
                }
            }
        }
    }
}
