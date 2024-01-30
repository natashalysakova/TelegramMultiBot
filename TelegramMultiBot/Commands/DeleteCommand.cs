using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;


namespace TelegramMultiBot.Commands
{
    [ServiceKey("delete")]
    class DeleteCommand : BaseCommand, ICallbackHandler
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

        public override async Task Handle(Message message)
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
                await _client.SendTextMessageAsync(message.Chat, "Виберіть завдання, яке треба видалити", replyMarkup: inlineKeyboard, disableNotification: true, messageThreadId: message.MessageThreadId);
            }
            else
            {
                _logger.LogDebug("No jobs found");
                await _client.SendTextMessageAsync(message.Chat, "Завдань не знайдено", disableNotification: true, messageThreadId: message.MessageThreadId);
            }
        }

        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            var callbackData = CallbackData.FromData(callbackQuery.Data);

            _logger.LogDebug("Deleting job: " + callbackData.data);

            var jobId = callbackData.data.ToString();
            if (jobId is null)
                return;

            _jobManager.DeleteJob(long.Parse(jobId));
            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завдання видалено", showAlert: true) ;
        }
    }
}
