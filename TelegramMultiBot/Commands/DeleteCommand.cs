using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;


namespace TelegramMultiBot.Commands
{
    class DeleteCommand : ICommand
    {
        private readonly ILogger<DeleteCommand> _logger;
        private readonly JobManager _jobManager;
        private readonly TelegramBotClient _client;

        public DeleteCommand(ILogger<DeleteCommand> logger, JobManager jobManager, TelegramBotClient client)
        {
            _logger = logger;
            _jobManager = jobManager;
            _client = client;
        }
        public bool CanHandle(string textCommand)
        {
            return textCommand.ToLower().StartsWith("/delete");
        }

        public async void Handle(Message message)
        {
            var buttons = new List<InlineKeyboardButton[]>();
            var jobs = _jobManager.GetJobsByChatId(message.Chat.Id);
            if (jobs.Any())
            {
                foreach (var job in jobs)
                {
                    var data = new CallbackData(message.Chat.Id, nameof(DeleteCommand), job.Id).ToString();
                    buttons.Add([InlineKeyboardButton.WithCallbackData(job.Name, data)]);
                }
                InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(buttons);
                _logger.LogDebug("Sending list of available jobs");
                await _client.SendTextMessageAsync(message.Chat, "Виберіть завдання, яке треба видалити", replyMarkup: inlineKeyboard, disableNotification: true);
            }
            else
            {
                _logger.LogDebug("No jobs found");
                await _client.SendTextMessageAsync(message.Chat, "Завдань не знайдено", disableNotification: true);
            }
        }

        public void HandleCallback(CallbackData callbackData)
        {
            _logger.LogDebug("Deleting job: " + callbackData.data);

            var jobId = callbackData.data.ToString();
            if (jobId is null)
                return;

            _jobManager.DeleteJob(long.Parse(jobId));
            _client.SendTextMessageAsync(callbackData.chatId, "Завдання видалено", disableNotification: true);
        }
    }
}
