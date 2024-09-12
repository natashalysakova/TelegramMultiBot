using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("assistant")]
    internal class AssistantCommand(IAssistantDataService assistantDataService, TelegramClientWrapper clientWrapper) : BaseCommand
    {
        public override async Task Handle(Message message)
        {

            var subscriber = assistantDataService.HandleSubscriber(message.Chat.Id, message.MessageThreadId);

            if (subscriber.IsActive)
            {
                await clientWrapper.SendMessageAsync(message.Chat, "Асистента активовано");
            }
            else
            {
                await clientWrapper.SendMessageAsync(message.Chat, "Асистента деактивовано");
            }
        }
    }
}
