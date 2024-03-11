using AngleSharp.Html;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands.CallbackDataTypes;
using TelegramMultiBot.Properties;
using static TelegramMultiBot.Commands.ReminderCommand;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("reminder")]
    internal class ReminderCommand : BaseCommand, ICallbackHandler
    {
        private readonly TelegramClientWrapper _client;
        private readonly ILogger<ReminderCommand> _logger;
        private readonly DialogManager _dialogManager;
        private readonly JobManager _jobManager;

        public ReminderCommand(TelegramClientWrapper client, ILogger<ReminderCommand> logger, DialogManager dialogManager, JobManager jobManager)
        {
            _client = client;
            _logger = logger;
            _dialogManager = dialogManager;
            _jobManager = jobManager;
        }

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
            await _client.SendPhotoAsync(message, photo, "Привіт, я бобер-нагадувач. Обери що ти хочеш зробити", markup: menu2);
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
                    if (callbackData.Id is null)
                    {
                        throw new NullReferenceException(nameof(callbackData.Id));
                    }
                    await DeleteJob(callbackQuery, callbackData.Id);
                    break;
                default:
                    break;
            }

        }

        private async Task Delete(CallbackQuery callbackQuery)
        {
            var message = callbackQuery.Message as Message ?? throw new NullReferenceException("Query message is null");



            var buttons = new List<InlineKeyboardButton[]>();
            var jobs = _jobManager.GetJobsByChatId(message.Chat.Id);
            if (jobs.Count != 0)
            {
                await _client.AnswerCallbackQueryAsync(callbackQuery.Id);

                foreach (var job in jobs)
                {
                    var text = $"{job.Name} ({job.Config})";
                    buttons.Add(new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData(text, new ReminderCallbackData(Command, ReminderCommands.DeleteJob,job.Id))
                    });
                }
                InlineKeyboardMarkup inlineKeyboard = new(buttons);
                _logger.LogDebug("Sending list of available jobs");
                await _client.SendMessageAsync(message, "Виберіть завдання, яке треба видалити", replyMarkup: inlineKeyboard);

            }
            else
            {
                _logger.LogDebug("No jobs found");
                await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдань не знайдено", true);
            }

        }

        private async Task GetList(CallbackQuery callbackQuery)
        {
            var message = callbackQuery.Message as Message ?? throw new NullReferenceException("Query message is null");

            var jobs = _jobManager.GetJobsByChatId(message.Chat.Id);
            var response = string.Join('\n', jobs.Select(x => $"{x.Name} ({x.Config}) Наступний запуск: {x.NextExecution} Текст: {x.Message}"));
            if (string.IsNullOrEmpty(response))
            {
                await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдань не знайдено", true);

                return;
            }

            await _client.AnswerCallbackQueryAsync(callbackQuery.Id);
            await _client.SendMessageAsync(message, response);

        }
       

        private async Task DeleteJob(CallbackQuery callbackQuery, string jobId)
        {
            _logger.LogDebug("Deleting job: {jobId}", jobId);
            _jobManager.DeleteJob(long.Parse(jobId));
            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдання видалено", true);
        }

        private async Task AddJob(CallbackQuery callbackQuery)
        {
            var message = callbackQuery.Message as Message ?? throw new NullReferenceException("Query message is null");

            var chatId = message.Chat.Id;
            var dialog = new AddJobDialog()
            {
                ChatId = chatId
            };

            _dialogManager[chatId] = dialog;
            await _client.AnswerCallbackQueryAsync(callbackQuery.Id);
            await _dialogManager.HandleActiveDialog(message, dialog);
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
