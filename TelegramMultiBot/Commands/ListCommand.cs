using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;


namespace TelegramMultiBot.Commands
{
    [Command("list")]
    class ListCommand : BaseCommand
    {
        private readonly ILogger<ListCommand> _logger;
        private readonly JobManager _jobManager;
        private readonly TelegramBotClient _client;

        public ListCommand(ILogger<ListCommand> logger, JobManager jobManager, TelegramBotClient client)
        {
            _logger = logger;
            _jobManager = jobManager;
            _client = client;
        }

        public override async Task Handle(Message message)
        {
            var jobs = _jobManager.GetJobsByChatId(message.Chat.Id);
            var response = string.Join('\n', jobs.Select(x => $"{x.Name} Наступний запуск: {x.NextExecution} Текст: {x.Message}"));
            if (string.IsNullOrEmpty(response))
            {
                await _client.SendTextMessageAsync(message.Chat, "Завдань не знайдено", disableNotification: true);
                return;
            }
            await _client.SendTextMessageAsync(message.Chat, response, disableWebPagePreview: true, disableNotification: true);
        }
    }
}
