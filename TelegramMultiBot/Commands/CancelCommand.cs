using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("cancel")]
    internal class CancelCommand : BaseCommand
    {
        private readonly TelegramClientWrapper _client;
        private readonly DialogManager _dialogManager;

        public CancelCommand(TelegramClientWrapper client, DialogManager dialogManager)
        {
            _client = client;
            _dialogManager = dialogManager;
        }

        public override async Task Handle(Message message)
        {
            var activeDialog = _dialogManager[message.Chat.Id];
            if (activeDialog != null)
            {
                _dialogManager.Remove(activeDialog);
                await _client.SendMessageAsync(message, "Операцію зупинено", disableNotification: true);
            }
            else
            {
                await _client.SendMessageAsync(message, "Нема активної операції", disableNotification: true);
            }
        }
    }
}