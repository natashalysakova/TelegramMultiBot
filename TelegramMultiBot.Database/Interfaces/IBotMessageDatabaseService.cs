namespace TelegramMultiBot.Database.Interfaces;

public interface IBotMessageDatabaseService
{
    void AddMessage(BotMessageAddInfo info);
    void DeleteMessage(BotMessageInfo info);
    bool IsBotMessage(BotMessageInfo info);
    bool IsActiveJob(BotMessageInfo info);
    int RunCleanup();
    long GetUserId(BotMessageInfo info);
}

public record BotMessageAddInfo(long chatId, int messageId, bool isPrivate, DateTime time,  long? userId = null);
public record BotMessageInfo(long chatId, int messageId);