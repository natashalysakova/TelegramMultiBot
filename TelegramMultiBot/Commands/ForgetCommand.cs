using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.AiAssistant;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.MessageCache;

namespace TelegramMultiBot.Commands;

[ServiceKey("forget", "ШІ асистент забуває контекст цього чату")]
internal class ForgetCommand(
    IAssistantDataService assistantDataService,
    TelegramClientWrapper clientWrapper,
    AiHelper chatHelper,
    ILogger<SummarizeCommand> logger,
    IMessageCacheService messageCacheService) : BaseCommand
{
    public override async Task Handle(Message message)
    {
        if (message.Text != null)
        {

            try
            {
                await clientWrapper.SendMessageAsync(message.Chat, "Контекст цього чату видалено.", messageThreadId: message.MessageThreadId);
                
                messageCacheService.RemoveContext(message.Chat.Id, message.MessageThreadId);

            }
            catch (Exception ex)
            {
                await clientWrapper.SendMessageAsync(message.Chat, "Упсі, шось не так: " + ex.Message, messageThreadId: message.MessageThreadId);
                logger.LogError(ex, ex.Message);
            }
        }
    }
}
