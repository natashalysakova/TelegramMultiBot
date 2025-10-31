using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.Commands;

[ServiceKey("queue", "Черга генерації", false)]
internal class QueueCommand(TelegramClientWrapper client, ILogger<QueueCommand> logger, ImageGenearatorQueue imageGenearatorQueue, IImageDatabaseService databaseService) : BaseCommand, ICallbackHandler
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
                    InlineKeyboardButton jobbutton = InlineKeyboardButton.WithCallbackData($"{item.Status}: {item.Type} {item.Text}", $"queue|delete|{item.Id}");
                    keyboard.AddNewRow(jobbutton);
                }

                await client.SendMessageAsync(message.Chat.Id, $"Черга генерації. Усього { jobs.Count() } запитів. Оберіть запит, який хочете відмінити.", replyMarkup: keyboard, message.MessageThreadId, message.Id);
            }
            else
            {
                await client.SendMessageAsync(message.Chat.Id, "Черга генерації порожня", messageThreadId: message.MessageThreadId, replyMessageId: message.Id);
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
            var messageToUpdate = imageGenearatorQueue.CancelJob(splitted[2]);
            await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Запит видалено", true);
            await client.EditMessageTextAsync(callbackQuery.Message!.Chat, messageToUpdate, "Запит видалено");
        }
    }
}
