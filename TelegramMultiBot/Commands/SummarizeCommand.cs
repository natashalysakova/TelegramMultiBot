using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.AiAssistant;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Commands;

[ServiceKey("summarize", "ШІ підсумовує що читав останім часом в чаті")]
internal class SummarizeCommand(IAssistantDataService assistantDataService, TelegramClientWrapper clientWrapper, AiHelper summarizeAiHelper, ILogger<SummarizeCommand> logger) : BaseCommand
{
    public override async Task Handle(Message message)
    {
        var assistant = assistantDataService.Get(message.Chat.Id, message.MessageThreadId);
        if (assistant == null)
        {
            await clientWrapper.SendMessageAsync(message.Chat, "Вибачте, але я не працюю в цьому чаті. Використайте команду /assistant щоб активувати асистента", messageThreadId: message.MessageThreadId);
            return;
        }

        var botMessage = await clientWrapper.SendMessageAsync(message.Chat, "Чекайте, підбиваю підсумки", messageThreadId: message.MessageThreadId);

        IEnumerable<ChatHistory> history;

        if (message.ReplyToMessage != null)
        {
            history = assistant.ChatHistory?.Where(x => x.SendTime >= message.ReplyToMessage.Date);
        }
        else
        {
            history = assistant.ChatHistory;
        }

        if (history is null || !history.Any())
        {
            await clientWrapper.EditMessageTextAsync(botMessage, "Не можу знайти історію чату. Спробуй пізніше");
            return;
        }
        try
        {
            var shortSummary = await summarizeAiHelper.Summarize(history);
            await clientWrapper.EditMessageTextAsync(botMessage, shortSummary);

        }
        catch (Exception ex)
        {
            await clientWrapper.EditMessageTextAsync(botMessage, "Сталась помилка");
            logger.LogError(ex, ex.Message);
        }
    }
}
