using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("status")]
    internal class StatusCommand : BaseCommand
    {
        private readonly TelegramBotClient _client;
        private readonly IEnumerable<IDiffusor> _diffusors;

        public StatusCommand(TelegramBotClient client, IEnumerable<IDiffusor> diffusors)
        {
            _client = client;
            _diffusors = diffusors;
        }
        public override async Task Handle(Message message)
        {
            string text = string.Empty;

            foreach (var diff in _diffusors)
            {
                var status = await diff.isAvailable() ? "available" : "not available";
                text += $"{diff.UI} - {status}\n";
            }

            await _client.SendTextMessageAsync(message.Chat.Id, text, replyToMessageId: message.MessageId);
        }
    }
}
