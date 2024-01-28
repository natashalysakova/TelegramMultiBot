using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    internal class MentionAllCommand : ICommand
    {
        private readonly TelegramBotClient _client;
        private readonly UserManager _users;
                public int Priority => 99;

        public bool CanHandle(string textCommand)
        {
            return textCommand.ToLower().StartsWith("/all");
        }

        public MentionAllCommand(TelegramBotClient client, UserManager users)
        {
            _client = client;
            _users = users;
        }

        public void Handle(Message message)
        {
            var names = _users.Where(x => x.chatId == message.Chat.Id && x.userId != message.From.Id).Select(x => "@" + x.username);          
            var text = string.Join(" ", names);
            _client.SendTextMessageAsync(message.Chat.Id, text);
        }

        public void HandleCallback(CallbackData callbackData)
        {
            return;
        }
    }

}
