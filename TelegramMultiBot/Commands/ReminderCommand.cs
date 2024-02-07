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
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Properties;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("reminder")]
    internal class ReminderCommand : BaseCommand , ICallbackHandler
    {
        private readonly TelegramBotClient _client;
        private readonly ILogger<ReminderCommand> _logger;
        private readonly DialogManager _dialogManager;
        private readonly JobManager _jobManager;

        public ReminderCommand(TelegramBotClient client, ILogger<ReminderCommand> logger, DialogManager dialogManager, JobManager jobManager)
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
                InlineKeyboardButton.WithCallbackData("Список", new CallbackData(Command,$"{ReminderCommands.List}").DataString),
                InlineKeyboardButton.WithCallbackData("Додати", new CallbackData(Command,$"{ReminderCommands.Add}").DataString),
                InlineKeyboardButton.WithCallbackData("Видалити", new CallbackData(Command,$"{ReminderCommands.Delete}").DataString)
            };
            var menu2 = new InlineKeyboardMarkup(buttons2);

            using (var stream = new MemoryStream(Properties.Resources.reminder))
            {
                var photo = InputFile.FromStream(stream, "beaver.png");
                await _client.SendPhotoAsync(message.Chat, photo, message.MessageThreadId, "Привіт, я бобер-нагадувач. Обери що ти хочеш зробити", replyMarkup: menu2);
            }
        }

        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            var callbackData = CallbackData.FromString(callbackQuery.Data);

            if (Enum.TryParse<ReminderCommands>(callbackData.Data.ElementAt(0), out var result))
            {
                switch (result)
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
                        await DeleteJob(callbackQuery, callbackData);
                        break;
                    default:
                        break;
                }
            }
        }

        private async Task Delete(CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var messageThreadId = callbackQuery.Message.MessageThreadId;

            var buttons = new List<InlineKeyboardButton[]>();
            var jobs = _jobManager.GetJobsByChatId(chatId);
            if (jobs.Any())
            {
                foreach (var job in jobs)
                {
                    var text = $"{job.Name} ({job.Config})";
                    buttons.Add([InlineKeyboardButton.WithCallbackData(text, new CallbackData(Command, $"{ReminderCommands.DeleteJob}|{job.Id}").DataString)]);
                }
                InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(buttons);
                _logger.LogDebug("Sending list of available jobs");
                await _client.SendTextMessageAsync(chatId, "Виберіть завдання, яке треба видалити", replyMarkup: inlineKeyboard, disableNotification: true, messageThreadId: messageThreadId);
            }
            else
            {
                _logger.LogDebug("No jobs found");
                await _client.SendTextMessageAsync(chatId, "Завдань не знайдено", disableNotification: true, messageThreadId: messageThreadId);
            }

            _client.AnswerCallbackQueryAsync(callbackQuery.Id);
        }

        private async Task GetList(CallbackQuery callbackQuery)
        {
            var jobs = _jobManager.GetJobsByChatId(callbackQuery.Message.Chat.Id);
            var response = string.Join('\n', jobs.Select(x => $"{x.Name} ({x.Config}) Наступний запуск: {x.NextExecution} Текст: {x.Message}"));
            if (string.IsNullOrEmpty(response))
            {
                //await _client.SendTextMessageAsync(message.Chat, "Завдань не знайдено", disableNotification: true, messageThreadId: message.MessageThreadId);
                await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдань не знайдено");


                return;
            }
            await _client.AnswerCallbackQueryAsync(callbackQuery.Id);
            await _client.SendTextMessageAsync(callbackQuery.Message.Chat, response, disableWebPagePreview: true, disableNotification: true, messageThreadId: callbackQuery.Message.MessageThreadId);
        }

        private async Task DeleteJob(CallbackQuery callbackQuery, CallbackData callbackData)
        {
            if(callbackData.Data.Count() <= 1) {
                throw new Exception("invalid callback data " + callbackData.ToString());
            }
            var jobId = callbackData.Data.ElementAt(1).ToString();

            _logger.LogDebug("Deleting job: " + callbackData.Data.ElementAt(1));
            _jobManager.DeleteJob(long.Parse(jobId));
            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдання видалено", showAlert: true);
        }

        private async Task AddJob(CallbackQuery callbackQuery)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var dialog = new AddJobDialog()
            {
                ChatId = chatId
            };

            _dialogManager[chatId] = dialog;
            _client.AnswerCallbackQueryAsync(callbackQuery.Id);
            await _dialogManager.HandleActiveDialog(callbackQuery.Message, dialog);
        }

        enum ReminderCommands
        {
            List,
            Add,
            Delete, 
            DeleteJob
        }
    }
}
