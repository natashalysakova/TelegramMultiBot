using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("delete")]
    internal class DeleteCommand : BaseCommand
    {
        private readonly TelegramClientWrapper _client;
        private readonly ILogger<DeleteCommand> _logger;

        public DeleteCommand(TelegramClientWrapper client, ILogger<DeleteCommand> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async override Task Handle(Message message)
        {
            if (message.ReplyToMessage == null || message.ReplyToMessage.Type == Telegram.Bot.Types.Enums.MessageType.ForumTopicCreated)
            {
                await _client.SendMessageAsync(message, "Не знаю що видаляти. Надішли повідомлення з командою у відповідь на повідомлення бота");
                return;
            }


            if (!message.ReplyToMessage.From.IsBot || message.ReplyToMessage.From.Id != _client.BotId)
            {
                await _client.SendMessageAsync(message, "Я можу видалити лише власні повідомлення");
                return;
            }

            var hours = message.ReplyToMessage.Chat.Type == ChatType.Private ? -24 : -48;
            if(message.ReplyToMessage.Date < DateTime.Now.AddHours(hours)) 
            {
                await _client.SendMessageAsync(message, $"Неможливо видалити повідомлення, що було відправлено більше ніж {Math.Abs(hours)} години тому ");
                return;
            }

            var bot = await _client.GetChatMemberAsync(message.Chat.Id, _client.BotId.Value);
            var canDeleteMessages = bot.Status == ChatMemberStatus.Administrator;

            await _client.DeleteMessageAsync(message.ReplyToMessage.Chat.Id, message.ReplyToMessage.MessageId);

            if (canDeleteMessages)
            {
                await _client.DeleteMessageAsync(message.Chat, message.MessageId);
            }

        }
    }
}
