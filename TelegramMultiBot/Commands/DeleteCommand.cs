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
        private readonly TelegramBotClient _client;
        private readonly ILogger<DeleteCommand> _logger;

        public DeleteCommand(TelegramBotClient client, ILogger<DeleteCommand> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async override Task Handle(Message message)
        {
            if (message.ReplyToMessage == null || message.ReplyToMessage.Type == Telegram.Bot.Types.Enums.MessageType.ForumTopicCreated)
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "Не знаю що видаляти. Надішли повідомлення з командою у відповідь на повідомлення бота", messageThreadId: message.MessageThreadId);
                return;
            }


            if (!message.ReplyToMessage.From.IsBot || message.ReplyToMessage.From.Id != _client.BotId)
            {
                await _client.SendTextMessageAsync(message.Chat.Id, "Я можу видалити лише власні повідомлення", messageThreadId: message.IsTopicMessage == true ? message.MessageThreadId : null);
                return;
            }

            var hours = message.ReplyToMessage.Chat.Type == ChatType.Private ? -24 : -48;
            if(message.ReplyToMessage.Date < DateTime.Now.AddHours(hours)) 
            {
                await _client.SendTextMessageAsync(message.Chat.Id, $"Неможливо видалити повідомлення, що було відправлено більше ніж {Math.Abs(hours)} години тому ", messageThreadId: message.IsTopicMessage == true ? message.MessageThreadId : null);
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
