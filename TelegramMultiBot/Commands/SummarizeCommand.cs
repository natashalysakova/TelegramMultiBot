using AngleSharp.Browser.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TelegramMultiBot.AiAssistant;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("summarize")]
    internal class SummarizeCommand(IAssistantDataService assistantDataService, TelegramClientWrapper clientWrapper) : BaseCommand
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

            string shortSummary;
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

            shortSummary = await SummarizeAiHelper.Summarize(history);
            await clientWrapper.EditMessageTextAsync(botMessage, shortSummary);
        }
    }
}
