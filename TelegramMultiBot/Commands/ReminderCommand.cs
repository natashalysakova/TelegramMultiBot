using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands.CallbackDataTypes;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Properties;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("reminder")]
    internal class ReminderCommand(TelegramClientWrapper client, ILogger<ReminderCommand> logger, DialogManager dialogManager, JobManager jobManager) : BaseCommand, ICallbackHandler
    {
        public override async Task Handle(Message message)
        {
            var buttons2 = new[]
            {
                InlineKeyboardButton.WithCallbackData("Список", new ReminderCallbackData(Command,ReminderCommands.List)),
                InlineKeyboardButton.WithCallbackData("Додати", new ReminderCallbackData(Command,ReminderCommands.Add)),
                InlineKeyboardButton.WithCallbackData("Видалити", new ReminderCallbackData(Command,ReminderCommands.Delete))
            };
            var menu2 = new InlineKeyboardMarkup(buttons2);

            using var stream = new MemoryStream(Resources.reminder);
            var photo = InputFile.FromStream(stream, "beaver.png");
            await client.SendPhotoAsync(message, photo, "Привіт, я бобер-нагадувач. Обери що ти хочеш зробити", markup: menu2);
        }

        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            var callbackData = ReminderCallbackData.FromString(callbackQuery.Data);

            switch (callbackData.JobType)
            {
                case ReminderCommands.List:
                    await GetList(callbackQuery);
                    break;

                case ReminderCommands.Add:
                    await AddJob(callbackQuery);
                    break;

                case ReminderCommands.Delete:
                    await Delete(callbackQuery);
                    break;

                case ReminderCommands.DeleteJob:
                    if (callbackData.JobId is null)
                    {
                        throw new NullReferenceException(nameof(callbackData.JobId));
                    }
                    await DeleteJob(callbackQuery, callbackData.JobId);
                    break;

                default:
                    break;
            }
        }

        private async Task Delete(CallbackQuery callbackQuery)
        {
            var message = callbackQuery.Message as Message ?? throw new NullReferenceException("Query message is null");

            var buttons = new List<InlineKeyboardButton[]>();
            var jobs = jobManager.GetJobsByChatId(message.Chat.Id);
            if (jobs.Count != 0)
            {
                await client.AnswerCallbackQueryAsync(callbackQuery.Id);

                foreach (var job in jobs)
                {
                    var text = $"{job.Name} ({job.Config})";
                    buttons.Add(
                    [
                        InlineKeyboardButton.WithCallbackData(text, new ReminderCallbackData(Command, ReminderCommands.DeleteJob,job.Id))
                    ]);
                }
                InlineKeyboardMarkup inlineKeyboard = new(buttons);
                logger.LogDebug("Sending list of available jobs");
                await client.SendMessageAsync(message, "Виберіть завдання, яке треба видалити", replyMarkup: inlineKeyboard);
            }
            else
            {
                logger.LogDebug("No jobs found");
                await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдань не знайдено", true);
            }
        }

        private async Task GetList(CallbackQuery callbackQuery)
        {
            var message = callbackQuery.Message as Message ?? throw new NullReferenceException("Query message is null");

            var jobs = jobManager.GetJobsByChatId(message.Chat.Id);
            var response = string.Join('\n', jobs.Select(x => $"{x.Name} ({x.Config}) Наступний запуск: {x.NextExecution} Текст: {x.Message}"));
            if (string.IsNullOrEmpty(response))
            {
                await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдань не знайдено", true);

                return;
            }

            await client.AnswerCallbackQueryAsync(callbackQuery.Id);
            await client.SendMessageAsync(message, response);
        }

        private async Task DeleteJob(CallbackQuery callbackQuery, string jobId)
        {
            logger.LogDebug("Deleting job: {jobId}", jobId);
            jobManager.DeleteJob(long.Parse(jobId));
            await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдання видалено", true);
        }

        private async Task AddJob(CallbackQuery callbackQuery)
        {
            var message = callbackQuery.Message as Message ?? throw new NullReferenceException("Query message is null");

            var chatId = message.Chat.Id;
            var dialog = new AddJobDialog()
            {
                ChatId = chatId,
                UserId = message.From.Id
            };

            dialogManager.Add(dialog);
            await client.AnswerCallbackQueryAsync(callbackQuery.Id);
            await dialogManager.HandleActiveDialog(message, dialog);
        }
    }

    public enum ReminderCommands
    {
        List,
        Add,
        Delete,
        DeleteJob
    }
}