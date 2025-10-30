using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("cancel", "Зупинити поточний діалог")]
    internal class CancelCommand(TelegramClientWrapper client, DialogManager dialogManager) : BaseCommand
    {
        public override async Task Handle(Message message)
        {
            var activeDialog = dialogManager[message.Chat.Id, message.From.Id];
            if (activeDialog != null)
            {
                dialogManager.Remove(activeDialog);
                await client.SendMessageAsync(message, "Операцію зупинено", disableNotification: true);
            }
            else
            {
                await client.SendMessageAsync(message, "Нема активної операції", disableNotification: true);
            }
        }
    }
}