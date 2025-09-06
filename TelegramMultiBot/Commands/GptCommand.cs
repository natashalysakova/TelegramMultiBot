using Microsoft.Extensions.Logging;
using System.Threading;
using Telegram.Bot.Types;
using TelegramMultiBot.AiAssistant;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.MessageCache;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("gpt")]
    internal class GptCommand(
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
                    var contextMessages = messageCacheService.GetContextForChat(message.Chat.Id, message.MessageThreadId);
                    var contextForLlm = string.Join(";", contextMessages.Select(m => $"[{m.UserName}]:{m.Text}"));

                    var text = message.Text.Replace("/gpt", string.Empty).Trim();

                    var llmRequest = $"Context:\n{contextForLlm}|Question:[{message.From?.Username ?? message.From?.FirstName}]{text}";

                    var response = await chatHelper.Chat(llmRequest);

                    if (response.Contains("</think>"))
                    {
                        var indexOfEnd = response.IndexOf("</think>");
                        response = response.Substring(indexOfEnd + "</think>".Length).Trim();
                    }

                    await clientWrapper.SendMessageAsync(message.Chat, response, messageThreadId: message.MessageThreadId);

                }
                catch (Exception ex)
                {
                    await clientWrapper.SendMessageAsync(message.Chat, "Упсі, шось не так: " + ex.Message, messageThreadId: message.MessageThreadId);
                    logger.LogError(ex, ex.Message);
                }
            }
        }
    }
}
