using System.Data;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface IBotMessageDatabaseService
    {
        void AddMessage(BotMessageAddInfo info, DateTime date);
        void DeleteMessage(BotMessageInfo info);
        bool IsBotMessage(BotMessageInfo info);
        bool IsActiveJob(BotMessageInfo info);
        int RunCleanup();
    }

    public record BotMessageAddInfo(long chatId, int messageId, bool isPrivate);
    public record BotMessageInfo(long chatId, int messageId);
}