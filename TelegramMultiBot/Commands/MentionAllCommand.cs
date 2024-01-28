using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    internal class MentionAllCommand : ICommand
    {
        private readonly TelegramBotClient _client;

        public bool CanHandle(string textCommand)
        {
            return textCommand.Contains("/all") || textCommand.Contains("/everyone") || textCommand.Contains("/here");
        }

        public MentionAllCommand(TelegramBotClient client)
        {
            _client = client;
        }

        public void Handle(Message message)
        {

        }

        public void HandleCallback(CallbackData callbackData)
        {
            throw new NotImplementedException();
        }
    }
}
