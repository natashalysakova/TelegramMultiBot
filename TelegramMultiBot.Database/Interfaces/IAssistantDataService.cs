using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface IAssistantDataService
    {
        int Cleanup();
        AssistantSubscriber? Get(long id, int? messageThreadId);
        AssistantSubscriber HandleSubscriber(long id, int? messageThreadId);
        void SaveToHistory(ChatHistory chatHistory);
    }
}