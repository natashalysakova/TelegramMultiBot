using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;


namespace TelegramMultiBot.Commands
{
    class AddCommand : ICommand
    {
        private readonly DialogManager _dialogManager;
        private readonly ILogger<AddCommand> _logger;

        public AddCommand(DialogManager dialogManager, ILogger<AddCommand> _logger)
        {
            _dialogManager = dialogManager;
            this._logger = _logger;
        }

        public bool CanHandle(string textCommand)
        {
            return textCommand.ToLower().StartsWith("/add");
        }

        public async void Handle(Message message)
        {
            var dialog = new AddJobDialog()
            {
                ChatId = message.Chat.Id
            };

            _dialogManager[message.Chat.Id] = dialog;
            await _dialogManager.HandleActiveDialog(message, dialog);
        }

        public void HandleCallback(CallbackData callbackData)
        {
            return;
        }
    }
}
