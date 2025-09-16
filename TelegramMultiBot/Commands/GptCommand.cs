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
        IMessageCacheService messageCacheService,
        IPhrasesService phrasesService) : BaseCommand
    {

        public override bool CanHandle(Message message)
        {
            var lookupWords = new[] { "бобер", "бобр", "бобрик", BotService.BotName };

            if (lookupWords.Any(w => message.Text != null && message.Text.Contains(w, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return base.CanHandle(message);
        }
        public override async Task Handle(Message message)
        {
            if (message.Text != null)
            {

                try
                {
                    var contextMessages = messageCacheService.GetContextForChat(message.Chat.Id, message.MessageThreadId);
                    var contextForLlm = string.Join("\n\n", contextMessages.Select(m => $"{m.UserName}:\n{m.Text}"));

                    var text = message.Text.Replace("/gpt", string.Empty).Trim();

                    var llmRequest = $"Context:\n{contextForLlm}\n\nQuestion:{text}";
                    Console.WriteLine("LLM Request: " + llmRequest);
                    var response = await chatHelper.Chat(llmRequest);

                    if (response.Contains("</think>"))
                    {
                        var indexOfEnd = response.IndexOf("</think>");
                        response = response.Substring(indexOfEnd + "</think>".Length).Trim();
                    }

                    context.AddLast(new ChatMessage(text, BotService.BotName ?? "bober"));

                    await clientWrapper.SendMessageAsync(message.Chat, response, messageThreadId: message.MessageThreadId);

                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("No route to host"))
                    {
                        await clientWrapper.SendMessageAsync(message.Chat, phrasesService.GetRandomPhrase(), messageThreadId: message.MessageThreadId);
                    }
                    else
                    {
                        await clientWrapper.SendMessageAsync(message.Chat, "Упсі, шось не так: " + ex.Message, messageThreadId: message.MessageThreadId);
                        logger.LogError(ex, ex.Message);
                    }
                }
            }
        }
    }
}
