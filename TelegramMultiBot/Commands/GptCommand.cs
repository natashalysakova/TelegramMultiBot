using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.AiAssistant;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("gpt")]
    internal class GptCommand(IAssistantDataService assistantDataService, TelegramClientWrapper clientWrapper, AiHelper chatHelper, ILogger<SummarizeCommand> logger) : BaseCommand
    {
        public override async Task Handle(Message message)
        {
            if (message.Text != null)
            {

                try
                {
                    var text = message.Text.Replace("/gpt", string.Empty).Trim();
                    var response = await chatHelper.Chat(text);

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
