using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("edit", "Хз шо це, не пам'ятаю", false)]
    internal class EditCommand(TelegramClientWrapper client, DialogManager dialogManager) : BaseCommand
    {
        public override bool CanHandle(Message message)
        {
            return base.CanHandle(message) && message.ReplyToMessage != null && message.ReplyToMessage.Text != null && (message.ReplyToMessage.Text.Contains("@" + message.From.Username) || message.ReplyToMessage.Text.Contains(message.From.FirstName));
        }

        public override async Task Handle(Message message)
        {
            var messageToChange = message.ReplyToMessage;

            var oldText = messageToChange.Text;
            var messageBegin = oldText.IndexOf(":") + 1;
            var linkIndex = oldText.IndexOf("http", messageBegin);
            var endLink = oldText.IndexOf(" ", linkIndex);
            string link;
            if (endLink != -1)
                link = oldText.Substring(linkIndex, endLink - linkIndex);
            else
                link = oldText.Substring(linkIndex);


            var newText = oldText.Substring(0, messageBegin) + " " + message.Text.Substring(5).Trim() + " " + link;

            await client.EditMessageTextAsync(messageToChange, newText);

            var bot = await client.GetChatMemberAsync(message.Chat, client.BotId.Value);
            var canDeleteMessages = bot.Status == ChatMemberStatus.Administrator;

            if (canDeleteMessages)
            {
                await client.DeleteMessageAsync(message);
            }
        }
    }
}
