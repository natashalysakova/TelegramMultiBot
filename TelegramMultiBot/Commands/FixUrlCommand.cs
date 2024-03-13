using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramMultiBot.Commands
{
    internal class FixUrlCommand : BaseCommand
    {
        private readonly TelegramClientWrapper _client;

        public FixUrlCommand(TelegramClientWrapper client)
        {
            _client = client;
        }

        public override bool CanHandle(Message message)
        {
            if (message.Text is null)
                return false;

            return ServiceItems.Any(x => message.Text.ToLower().Contains(x.service));
        }

        public override bool CanHandle(InlineQuery query)
        {
            return false;
        }

        public override async Task Handle(Message message)
        {
            if (message.Text is null)
                throw new NullReferenceException(nameof(message.Text));

            var links = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x.Contains("https://"));

            if (links is null)
                return;

            foreach (var link in links)
            {
                var service = ServiceItems.SingleOrDefault(x => link.Contains(x.service));
                if (service is null)
                    return;

                string newlink = string.IsNullOrEmpty(service.whatReplace) || string.IsNullOrEmpty(service.replaceWith)
                    ? link
                    : link.Replace(service.whatReplace, service.replaceWith);

                newlink = CutTrackingInfo(newlink);

                if (_client.BotId == null)
                    throw new NullReferenceException(nameof(_client.BotId));

                var bot = await _client.GetChatMemberAsync(message.Chat, _client.BotId.Value);
                var canDeleteMessages = bot.Status == ChatMemberStatus.Administrator;

                string newMessage = string.Empty;
                if (canDeleteMessages)
                {
                    await _client.DeleteMessageAsync(message);
                    var oldMessage = message.Text.Replace(link, newlink);

                    if (message.From is null)
                    {
                        throw new NullReferenceException(nameof(message.From));
                    }

                    string name = string.Empty;
                    if (message.From.Username is null)
                    {
                        name = $"{message.From.FirstName}";
                    }
                    else
                    {
                        name = "@" + message.From.Username;
                    }

                    newMessage = $"\U0001f9ab {name}: {oldMessage}\n";

                    //await _client.SendTextMessageAsync(message.Chat, newMessage, disableNotification: false, replyToMessageId: replyTo, messageThreadId: messageThread);
                }
                else
                {
                    newMessage = $"🦫 Дякую, я не можу видалити твоє повідомлення, тримай лінк: {newlink}";
                    //await _client.SendTextMessageAsync(message.Chat, newMessage, replyToMessageId: message.MessageId, disableNotification: true);
                }

                await _client.SendMessageAsync(message, newMessage, true, disableNotification: false);
            }
        }

        private record ServiceItem(string service, string? whatReplace, string? replaceWith);

        private readonly List<ServiceItem> ServiceItems =
    [
        new ServiceItem("https://www.instagram.com", "instagram", "ddinstagram"),
        new ServiceItem("https://x.com", "x", "fixupx"),
        new ServiceItem("https://twitter.com", "twitter", "fxtwitter"),
        new ServiceItem("https://www.ddinstagram.com", null, null),
        new ServiceItem("https://facebook.com", null, null)
    ];

        private string CutTrackingInfo(string link)
        {
            if (link.Contains('?'))
            {
                return link.Replace(link[link.IndexOf('?')..], string.Empty);
            }

            return link;
        }
    }
}