using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Commands
{
    internal class FixUrlCommand(TelegramClientWrapper client, IBotMessageDatabaseService messageDatabaseService) : BaseCommand
    {
        public override bool CanHandle(Message message)
        {
            if (message.Text is null)
                return false;

            return _serviceItems.Any(x => message.Text.Contains(x.Service, StringComparison.CurrentCultureIgnoreCase));
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
                var service = _serviceItems.SingleOrDefault(x => link.Contains(x.Service));
                if (service is null)
                    return;

                string newlink = string.IsNullOrEmpty((string?)service.WhatReplace) || string.IsNullOrEmpty((string?)service.ReplaceWith)
                    ? link
                    : link.Replace((string)service.WhatReplace, (string)service.ReplaceWith);

                newlink = CutTrackingInfo(newlink);

                if (client.BotId == null)
                    throw new NullReferenceException(nameof(client.BotId));

                var bot = await client.GetChatMemberAsync(message.Chat, client.BotId.Value);
                var canDeleteMessages = bot.Status == ChatMemberStatus.Administrator || message.Chat.Type == ChatType.Private;

                string newMessage = string.Empty;
                if (canDeleteMessages)
                {
                    await client.DeleteMessageAsync(message);
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

                var botMessage = await client.SendMessageAsync(message, newMessage, true, disableNotification: true);
                messageDatabaseService.AddMessage(new BotMessageAddInfo(botMessage.Chat.Id, botMessage.MessageId, botMessage.Chat.Type == ChatType.Private, botMessage.Date, message.From.Id));
            }
        }

        private record ServiceItem(string Service, string? WhatReplace, string? ReplaceWith);

        private readonly List<ServiceItem> _serviceItems =
    [
        new ServiceItem("https://www.instagram.com", "instagram", "ddinstagram"),
        new ServiceItem("https://x.com", "x", "fixupx"),
        new ServiceItem("https://twitter.com", "twitter", "fxtwitter"),
        new ServiceItem("https://www.ddinstagram.com", null, null),
        new ServiceItem("https://facebook.com", null, null)
    ];

        private static string CutTrackingInfo(string link)
        {
            if (link.Contains('?'))
            {
                return link.Replace(link[link.IndexOf('?')..], string.Empty);
            }

            return link;
        }
    }
}