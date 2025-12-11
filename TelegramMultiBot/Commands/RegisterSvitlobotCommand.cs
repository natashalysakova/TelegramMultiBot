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

            var spliited = messageText!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (spliited.Length != 5)
            {
                await _client.SendMessageAsync(message.Chat.Id, "Невірна кількість аргументів. Використання: /svitlobot <add|remove> <region> <group_name> <key>", messageThreadId: message.MessageThreadId);
                return;
            }
            var region = spliited[2];
            var groupName = spliited[3];
            
            if(string.IsNullOrEmpty(region))
            {
                await _client.SendMessageAsync(message.Chat.Id, "Регіон не може бути порожнім", messageThreadId: message.MessageThreadId);
                return;
            }

            if(string.IsNullOrEmpty(groupName))
            {
                await _client.SendMessageAsync(message.Chat.Id, "Назва групи не може бути порожньою", messageThreadId: message.MessageThreadId);
                return;
            }

            var group = await _service.GetGroupByCodeAndLocationRegion(region, groupName, true);

            var key = spliited[4];
            if (string.IsNullOrEmpty(key))
            {
                await _client.SendMessageAsync(message.Chat.Id, "Ключ не може бути порожнім", messageThreadId: message.MessageThreadId);
                return;
            }

            if (spliited[1] == "add")
            {
                try
                {
                    await _service.AddSvitlobotKey(key, group.Id);
                    await _client.SendMessageAsync(message.Chat.Id, "Ключ додано успішно", messageThreadId: message.MessageThreadId);
                }
                catch (Exception)
                {
                    await _client.SendMessageAsync(message.Chat.Id, "Виникла помилка під час додавання ключа", messageThreadId: message.MessageThreadId);
                }
            }
            else if(spliited[1] == "remove")
            {
                try
                {
                    await _service.RemoveSvitlobotKey(key, group.Id);
                    await _client.SendMessageAsync(message.Chat.Id, "Ключ видалено успішно", messageThreadId: message.MessageThreadId);
                }
                catch (Exception)
                {
                    await _client.SendMessageAsync(message.Chat.Id, "Виникла помилка під час видалення ключа", messageThreadId: message.MessageThreadId);
                }
            }
        }
    }
}
