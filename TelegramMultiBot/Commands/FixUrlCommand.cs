using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


namespace TelegramMultiBot.Commands
{
    class FixUrlCommand : BaseCommand
    {
        private readonly ILogger _logger;
        private readonly TelegramBotClient _client;

        public FixUrlCommand(ILogger<FixUrlCommand> logger, TelegramBotClient client)
        {
            _logger = logger;
            _client = client;
        }

        public override bool CanHandle(Message message)
        {
            return ServiceItems.Any(x => message.Text.ToLower().Contains(x.service));
        }

        public override async Task Handle(Message message)
        {
            var links = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Contains("https://"));

            if (links is null)
                return;

            foreach (var link in links)
            {
                var service = ServiceItems.SingleOrDefault(x => link.Contains(x.service));
                if (service is null)
                    return;

                string newlink = string.IsNullOrEmpty(service.whatReplace) && string.IsNullOrEmpty(service.replaceWith)
                    ? link
                    : link.Replace(service.whatReplace, service.replaceWith);

                newlink = CutTrackingInfo(newlink);

                string newMessage;
                var bot = await _client.GetChatMemberAsync(message.Chat.Id, _client.BotId.Value);
                var canDeleteMessages = bot.Status == ChatMemberStatus.Administrator;
                if (canDeleteMessages)
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


                    int? messageThread = message.Chat.IsForum.HasValue && message.Chat.IsForum.Value ? message.MessageThreadId : null;
                    int? replyTo = message.ReplyToMessage != null ? message.ReplyToMessage.MessageId : null;


                    await _client.SendTextMessageAsync(message.Chat, newMessage, disableNotification: false, replyToMessageId: replyTo, messageThreadId: messageThread);

                }
                else
                {
                    newMessage = $"🦫 Дякую, я не можу видалити твоє повідомлення, тримай лінк: {newlink}";
                    await _client.SendTextMessageAsync(message.Chat, newMessage, replyToMessageId: message.MessageId, disableNotification: true);
                }
            }
        }

        record ServiceItem(string service, string? whatReplace, string? replaceWith);
        List<ServiceItem> ServiceItems = new List<ServiceItem>()
    {
        new ServiceItem("https://www.instagram.com", "instagram", "ddinstagram"),
        new ServiceItem("https://x.com", "x", "fixupx"),
        new ServiceItem("https://twitter.com", "twitter", "fxtwitter"),
        new ServiceItem("https://www.ddinstagram.com", null, null),
        new ServiceItem("https://facebook.com", null, null)
    };


        private string CutTrackingInfo(string link)
        {
            if (link.Contains('?'))
            {
                return link.Replace(link.Substring(link.IndexOf('?')), string.Empty);
            }

            return link;
        }
    }
}
