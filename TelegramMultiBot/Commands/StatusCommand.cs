using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("status", "Статус доступних генераторів")]
    internal class StatusCommand(TelegramClientWrapper client, IServiceProvider serviceProvider) : BaseCommand
    {
        public override async Task Handle(Message message)
        {
            var newMessage = await client.SendMessageAsync(message, "Чекай, перевіряю", true);

            var diffusors = serviceProvider.GetRequiredService<IEnumerable<IDiffusor>>();
            string text = string.Empty;

            foreach (var diff in diffusors)
            {
                var status = diff.IsAvailable() ? "available" : "not available";
                text += $"{diff.UI} - {status}\n";
            }

            await client.EditMessageTextAsync(newMessage, text);
            //await _client.SendTextMessageAsync(message.Chat.Id, text, replyToMessageId: message.MessageId);
        }
    }
}