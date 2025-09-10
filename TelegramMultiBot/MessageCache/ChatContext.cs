using System.Data;
using System.Text;
using System.Text.Json;
using TelegramMultiBot.AiAssistant;

namespace TelegramMultiBot.MessageCache
{
    public class ChatContext
    {
        private const int MaxTotalSizeInBytes = 2048;
        private int GetContextSizeInBytes()
        {
            return Encoding.UTF8.GetByteCount(this.ToString());
        }


        public ChatContext(string chatKey)
        {
            _chatKey = chatKey;
        }

        private readonly LinkedList<ChatMessage> messages = new();
        private readonly string _chatKey;

        public void AddLast(ChatMessage message)
        {
            messages.AddLast(message);

            while (GetContextSizeInBytes() > MaxTotalSizeInBytes)
            {
                EvictOldestMessage();
            }
        }

        override public string ToString()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            var builder = new StringBuilder();
            builder.Append("Messages:\n");
            var previousMessages = messages.Take(messages.Count - 1);

            builder.Append(string.Join("\n", previousMessages.Select(x => x.ToString())));
            
            var lastMessage = messages.Last?.Value;
            builder.Append($"\n\nLastMessage:\n {lastMessage.ToString()}");
            return builder.ToString();
        }

        private void EvictOldestMessage()
        {
            var oldestMessage = messages.First?.Value;
            if (oldestMessage == null) return;

            messages.Remove(oldestMessage);
        }

        internal IEnumerable<LLMChatMessage> GetMessages()
        {
            return messages.Select(x => new LLMChatMessage()
            {
                role = x.UserName == BotService.BotName ? "assistant" : "user",
                content = x.Text
            });
        }
    }
}
