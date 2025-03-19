using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("queue")]
    internal class QueueCommand(TelegramClientWrapper client, ILogger<QueueCommand> logger, ImageGenearatorQueue imageGenearatorQueue, IImageDatabaseService databaseService) : BaseCommand, ICallbackHandler, IInlineQueryHandler
    {
        public async override Task Handle(Message message)
        {
            if (message.Text == "/queue")
            {
                var jobs = imageGenearatorQueue.GetJobs();

                if (jobs.Any())
                {

                    var keyboard = new InlineKeyboardMarkup();

                    foreach (var item in jobs.Take(10))
                    {
                        InlineKeyboardButton jobbutton = InlineKeyboardButton.WithCallbackData($"{item.Status}: {(item.Text?.Length > 64 ? item.Text?.Substring(0, 64) : item.Text)}", $"queue|delete|{item.Id}");
                        keyboard.AddNewRow(jobbutton);
                    }

                    await client.SendMessageAsync(message.Chat.Id, $"Черга генерації. Усього { jobs.Count() } запитів. Оберіть запит, який хочете відмінити.", replyMarkup: keyboard);
                }
                else
                {
                    await client.SendMessageAsync(message.Chat.Id, "Черга генерації порожня");
                }
            }
        }

        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            if (callbackQuery.Data is null)
                return;

            var splitted = callbackQuery.Data.Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (splitted[1] == "delete")
            {
                imageGenearatorQueue.CancelJob(splitted[2]);
                await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Запит видалено", true);
            }
        }

        public Task HandleInlineQuery(InlineQuery inlineQuery)
        {
            throw new NotImplementedException();
        }
    }
}
