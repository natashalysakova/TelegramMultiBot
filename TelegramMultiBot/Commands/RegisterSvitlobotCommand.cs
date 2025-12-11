using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("svitlobot", "Register svitlobot key to be able to update schedule", false)]
    internal class RegisterSvitlobotCommand : BaseCommand
    {
        private readonly IMonitorDataService _service;
        private readonly TelegramClientWrapper _client;

        public RegisterSvitlobotCommand(IMonitorDataService service, TelegramClientWrapper client)
        {
            _service = service;
            _client = client;
        }

        public async override Task Handle(Message message)
        {
            var messageText = message.Text?.Trim();

            if (!string.IsNullOrEmpty(messageText)) 
            {
                return;
            }
            var spliited = messageText!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (spliited.Length != 5)
            {
                return;
            }
            var region = spliited[2];
            var groupName = spliited[3];
            
            if(string.IsNullOrEmpty(region))
            {
                return;
            }

            if(string.IsNullOrEmpty(groupName))
            {
                return;
            }

            var group = await _service.GetGroupByCodeAndLocationRegion(region, groupName);

            var key = spliited[4];
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (spliited[1] == "add")
            {
                await _service.AddSvitlobotKey(key, group.Id);
                await _client.SendMessageAsync(message.Chat.Id, "Ключ додано успішно", messageThreadId: message.MessageThreadId);
                return;
            }

            if(spliited[1] == "remove")
            {
                await _service.RemoveSvitlobotKey(key, group.Id);

                return;
            }
        }
    }
}
