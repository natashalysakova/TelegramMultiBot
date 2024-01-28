using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;


namespace TelegramMultiBot.Commands
{
    class FixUrlCommand : ICommand
    {
        private readonly ILogger _logger;
        private readonly TelegramBotClient _client;

        public FixUrlCommand(ILogger<FixUrlCommand> logger, TelegramBotClient client)
        {
            _logger = logger;
            _client = client;
        }

        public bool CanHandle(string textCommand)
        {
            return ServiceItems.Any(x => textCommand.Contains(x.service));
        }

        public async void Handle(Message message)
        {
            string link = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(x => x.Contains("https://"));

            if (link is null)
                return;

            var service = ServiceItems.SingleOrDefault(x => link.Contains(x.service));
            if (service is null)
                return;

            var newlink = link.Replace(service.whatReplace, service.replaceWith);
            newlink = CutTrackingInfo(newlink);

            string newMessage;
            try
            {
                await _client.DeleteMessageAsync(message.Chat, message.MessageId);
                var oldMessage = message.Text.Replace(link, newlink);

                string name = string.Empty;
                if (string.IsNullOrEmpty(message.From.Username))
                {
                    name = $"{message.From.FirstName}";
                }
                else
                {
                    name = "@" + message.From.Username;
                }

                newMessage = $"\U0001f9ab {name}: {oldMessage}\n";
                await _client.SendTextMessageAsync(message.Chat, newMessage, disableNotification: false);
            }
            catch (Exception)
            {
                newMessage = $"🦫 Дякую, я не зміг видалити твоє повідомлення, тому ось твій лінк: {newlink}";
                await _client.SendTextMessageAsync(message.Chat, newMessage, replyToMessageId: message.MessageId, disableNotification: true);
            }


        }

        record ServiceItem(string service, string whatReplace, string replaceWith);
        List<ServiceItem> ServiceItems = new List<ServiceItem>()
    {
        new ServiceItem("https://www.instagram.com", "instagram", "ddinstagram"),
        new ServiceItem("https://x.com", "x", "fixupx"),
        new ServiceItem("https://twitter.com", "twitter", "fxtwitter"),
    };
        private string CutTrackingInfo(string link)
        {
            if (link.Contains('?'))
            {
                return link.Replace(link.Substring(link.IndexOf('?')), string.Empty);
            }

            return link;
        }

        public void HandleCallback(CallbackData callbackData)
        {
            return;
        }
    }
}
