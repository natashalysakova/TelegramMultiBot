using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("delete", "Видалити повідомлення від бота")]
    internal class DeleteCommand(TelegramClientWrapper client, IBotMessageDatabaseService messageDatabaseService) : BaseCommand, IMessageReactionHandler
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

            await client.DeleteMessageAsync(message.ReplyToMessage.Chat.Id, message.ReplyToMessage.MessageId);

            var bot = await client.GetChatMemberAsync(message.Chat.Id, client.BotId.Value);
            if (bot.Status == ChatMemberStatus.Administrator)
            {
                await client.DeleteMessageAsync(message.Chat, message.MessageId);
            }
        }

        public override bool CanHandle(MessageReactionUpdated reactions)
        {
            return true;
        }

        public async Task HandleMessageReactionUpdate(MessageReactionUpdated messageReaction)
        {
            var info = new BotMessageInfo(messageReaction.Chat.Id, messageReaction.MessageId);

            var userId = messageDatabaseService.GetUserId(info);
            if (userId == 0)
                return;

            if (messageDatabaseService.IsBotMessage(info) && !messageDatabaseService.IsActiveJob(info) && messageReaction.User.Id == userId )
            {
                var emojis = messageReaction.NewReaction.Where(x => x.Type == ReactionTypeKind.Emoji).Select(x => (ReactionTypeEmoji)x);

                if (emojis.Any(x => x.Emoji == ReactionEmoji.PileOfPoo))
                {
                    await client.DeleteMessageAsync(info.chatId, info.messageId);
                    messageDatabaseService.DeleteMessage(info);
                }
            }

        }
    }
}