using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("delete")]
    internal class DeleteCommand(TelegramClientWrapper client) : BaseCommand
    {
        public override async Task Handle(Message message)
        {
            if (message.ReplyToMessage == null || message.ReplyToMessage.Type == MessageType.ForumTopicCreated || message.ReplyToMessage.From == null)
            {
                await client.SendMessageAsync(message, "Не знаю що видаляти. Надішли повідомлення з командою у відповідь на повідомлення бота");
                return;
            }

            if (!message.ReplyToMessage.From.IsBot || message.ReplyToMessage.From.Id != client.BotId)
            {
                await client.SendMessageAsync(message, "Я можу видалити лише власні повідомлення");
                return;
            }

            var hours = message.ReplyToMessage.Chat.Type == ChatType.Private ? -24 : -48;
            if (message.ReplyToMessage.Date < DateTime.Now.AddHours(hours))
            {
                await client.SendMessageAsync(message, $"Неможливо видалити повідомлення, що було відправлено більше ніж {Math.Abs(hours)} години тому ");
                return;
            }

            var bot = await client.GetChatMemberAsync(message.Chat.Id, client.BotId.Value);
            var canDeleteMessages = bot.Status == ChatMemberStatus.Administrator;

            await client.DeleteMessageAsync(message.ReplyToMessage.Chat.Id, message.ReplyToMessage.MessageId);

            if (canDeleteMessages)
            {
                await client.DeleteMessageAsync(message.Chat, message.MessageId);
            }
        }
    }
}