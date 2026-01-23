using AutoMapper;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.BackgroundServies;

namespace TelegramMultiBot.Commands;

[ServiceKey("address", "Бобер Електрик буде спостерігати за адресою")]
internal class AddressCommand(TelegramClientWrapper client, MonitorService monitorService, ILogger<AddressCommand> logger, IMapper mapper) : BaseCommand
{

    public async override Task Handle(Message message)
    {
        var split = message.Text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // possible options
        // /command region city street building <optional chatId>
        // /command region street building <optional chatId> - for KEM ONLY

        if(split.Length > 6 || split.Length < 4)
        {
            await client.SendMessageAsync(message.Chat.Id, "Невірний формат команди. Використання:\n/address | <регіон> | <місто> | <вулиця> | <будинок> | [<chatId>]\nабо\n/address | <регіон> | <вулиця> | <будинок> | [<chatId>] (тільки для м.Київ)");
            return;
        }

        var region = split[1];
        var chatId = message.Chat.Id;
        var city = "м. Київ";
        var street = "";
        var building = "";

        if (long.TryParse(split[split.Length-1], out long parsedChatId))
        {
            chatId = parsedChatId;

            building = split[split.Length - 2];
            street = split[split.Length - 3];

            if(split.Length-4 == 2)
            {
                city = split[2];
            }
        }
        else
        {
            building = split[split.Length - 1];
            street = split[split.Length - 2];
            if (split.Length - 3 == 2)
            {
                city = split[2];
            }
        }
        var messageThread = chatId == message.Chat.Id ? message.MessageThreadId : null;
        await monitorService.AddAddressJob(chatId, region, city, street, building, messageThread);
        await client.SendMessageAsync(message.Chat, "Addres job saved");
    }
}
