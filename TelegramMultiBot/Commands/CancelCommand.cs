using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("cancel")]
    internal class CancelCommand : BaseCommand
    {
        private readonly TelegramBotClient _client;
        private readonly DialogManager _dialogManager;

        public CancelCommand(TelegramBotClient client, DialogManager dialogManager)
        {
            _client = client;
            _dialogManager = dialogManager;
        }

        public override async Task Handle(Message message)
        {
            var activeDialog = _dialogManager[message.Chat.Id];
            if(activeDialog != null)
            {
                _dialogManager.Remove(activeDialog);
                await _client.SendTextMessageAsync(message.Chat.Id, "Операцію зупинено", disableNotification: true, messageThreadId: message.MessageThreadId);
            }
            else
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "Нема активної операції", disableNotification: true, messageThreadId: message.MessageThreadId);
            }
        }
    }
}
