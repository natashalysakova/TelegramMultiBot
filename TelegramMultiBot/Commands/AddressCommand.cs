using AutoMapper;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.BackgroundServies;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Commands;

[ServiceKey("address", "Бобер Електрик буде спостерігати за адресою")]
internal class AddressCommand(
        TelegramClientWrapper client, 
        IMonitorDataService dataService, 
        MonitorService monitorService,
        ILogger<AddressCommand> logger, 
        IMapper mapper) : BaseCommand, ICallbackHandler, IInlineQueryHandler
{
    public override bool CanHandle(InlineQuery query)
    {
        return query.Query.StartsWith("address|");
    }

    private string format = "/address | <регіон> | <місто> | <вулиця> | <будинок> | [<chatId>]";
    public async override Task Handle(Message message)
    {
        var split = message.Text.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // possible options
        // /command region city street building <optional chatId>
        // /command region street building <optional chatId> - for KEM ONLY

        if(split.Length > 6 || split.Length < 5)
        {
            await client.SendMessageAsync(
                message.Chat.Id, 
                $"Невірний формат команди. Використання:\n{format}",
                messageThreadId: message.MessageThreadId);
            return;
        }

        var chatId = message.Chat.Id;
        bool hasChatId = split.Length == 6;
        if (hasChatId)
        {
            if(!long.TryParse(split[5], out chatId))
            {
                await client.SendMessageAsync(
                    message.Chat.Id, 
                    $"Невірний формат chatId. Використання:\n{format}",
                    messageThreadId: message.MessageThreadId);
                return;
            }
        }

        var region = split[1];
        var city = split[2];
        var street = split[3];
        var building = split[4];
 
        var messageThread = chatId == message.Chat.Id ? message.MessageThreadId : null;
        await monitorService.AddAddressJob(chatId, region, city, street, building, messageThread);
        await client.SendMessageAsync(message.Chat, "Addres job saved", messageThreadId: messageThread);
        return;

        var keyboad = new InlineKeyboardMarkup()
        {
            InlineKeyboard = new InlineKeyboardButton[][]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("Додати адресу", "address|add"),
                    InlineKeyboardButton.WithCallbackData("Мої адреси", "address|list")
                }
            }
        };

        await client.SendMessageAsync(
            message.Chat.Id,
            "Виберіть дію:",
            replyMarkup: keyboad,
            messageThreadId: message.MessageThreadId);
    }

    public Task HandleCallback(CallbackQuery callbackQuery)
    {
        var callbackHandler = GetHandler(callbackQuery.Data);
        if (callbackHandler != null)
        {
            return callbackHandler.Handle(callbackQuery);
        }

        return client.AnswerCallbackQueryAsync(callbackQuery.Id);
    }

    private IAddressCallbackHandler? GetHandler(string data)
    {
        var lastStep = data?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if(lastStep != null && lastStep.Contains('='))
        {
            lastStep = lastStep.Split('=')[0];
        }
        return lastStep switch
        {
            "add" => new AddAddressHandler(client),
            "r" => new SelectStreetHandler(client, dataService),
            "s" => new SelectBuildingHandler(client, dataService),
            "b" => new ChatSelectorHandler(),
            "c" => new ConfirmAddressHandler(client, dataService),
        };
    }

    public async Task HandleInlineQuery(InlineQuery inlineQuery)
    {
        var streetPart = inlineQuery.Query
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.StartsWith("s="))?
            .Substring(2) ?? "";
        var cityPart = inlineQuery.Query
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.StartsWith("c="))?
            .Substring(2) ?? "";
        var regionPart = inlineQuery.Query
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.StartsWith("r="))?
            .Substring(2) ?? "";
        var buildingPart = inlineQuery.Query
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.StartsWith("b="))?
            .Substring(2) ?? "";

        var lastPart = inlineQuery.Query
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        var value = lastPart?.Contains('=') == true ? lastPart.Split('=')[1] : "";
        lastPart = lastPart?.Contains('=') == true ? lastPart.Split('=')[0] : lastPart;

        if (value.Length < 3)
        {
            // user is typing, do not show any results
            await client.AnswerInlineQueryAsync(
                inlineQuery.Id,
                Array.Empty<Telegram.Bot.Types.InlineQueryResults.InlineQueryResult>());
            return;
        }


        if(lastPart == "c")
        {

            var cities = await dataService.GetAvailableCitiesByRegionAndPartialName(regionPart, value);
            cities = cities.Where(c => c.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();
            var results = cities.Select(city => new Telegram.Bot.Types.InlineQueryResults.InlineQueryResultArticle(
                id: $"address_city_{city.Id}",
                title: city.Name,
                inputMessageContent: new Telegram.Bot.Types.InlineQueryResults.InputTextMessageContent($"address|r={regionPart}|c={city.Name}|s=")
            )).ToArray();

            await client.AnswerInlineQueryAsync(
                inlineQuery.Id,
                results);
            return;
        }

        if(lastPart == "s")
        {
            var streets = await dataService.GetAvailableStreetsByRegionAndCity(regionPart, cityPart);
            streets = streets.Where(s => s.Name.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();
            var results = streets.Select(street => new Telegram.Bot.Types.InlineQueryResults.InlineQueryResultArticle(
                id: $"address_street_{street.Id}",
                title: street.Name,
                inputMessageContent: new Telegram.Bot.Types.InlineQueryResults.InputTextMessageContent($"address|r={regionPart}|c={cityPart}|s={street.Name}|b=")
            )).ToArray();

            await client.AnswerInlineQueryAsync(
                inlineQuery.Id,
                results);
            return;
        }

        if(lastPart == "b")
        {
            var buildings = await dataService.GetAvailableBuildingsByRegionCityAndStreet(regionPart, cityPart, streetPart);
            buildings = buildings.Where(b => b.Number.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList();
            var results = buildings.Select(building => new Telegram.Bot.Types.InlineQueryResults.InlineQueryResultArticle(
                id: $"address_building_{building.Id}",
                title: building.Number,
                inputMessageContent: new Telegram.Bot.Types.InlineQueryResults.InputTextMessageContent($"address|r={regionPart}|c={cityPart}|s={streetPart}|b={building.Number}")
            )).ToArray();

            await client.AnswerInlineQueryAsync(
                inlineQuery.Id,
                results);
            return;
        }  
    }
}

    interface IAddressCallbackHandler
    {
        Task Handle(CallbackQuery callbackQuery);
    }
    class AddAddressHandler(TelegramClientWrapper client) : IAddressCallbackHandler
    {
        public async Task Handle(CallbackQuery callbackQuery)
        {
            var replyMarkup = new InlineKeyboardMarkup(new InlineKeyboardButton[][]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("М.Київ", "address|r=kem|c="),
                    InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Київська область", "address|r=krem|c=")
                }
            });
            await client.SendMessageAsync(
                callbackQuery.Message!.Chat.Id,
                "Оберіть регіон:",
                replyMarkup: replyMarkup,
                messageThreadId: callbackQuery.Message.MessageThreadId);
        }
}

    class SelectStreetHandler : IAddressCallbackHandler
    {
        private TelegramClientWrapper client;
        private IMonitorDataService dataService;

        public SelectStreetHandler(TelegramClientWrapper client, IMonitorDataService dataService)
        {
            this.client = client;
            this.dataService = dataService;
        }

        public Task Handle(CallbackQuery callbackQuery)
        {
            var keyboad = new ForceReplyMarkup
            {
                Selective = true,
                InputFieldPlaceholder = "Введіть назву вулиці",
            };
            return client.SendMessageAsync(
                callbackQuery.Message!.Chat.Id,
                "Введіть назву вулиці:",
                replyMarkup: keyboad,
                messageThreadId: callbackQuery.Message.MessageThreadId);
        }
    }
    class SelectBuildingHandler : IAddressCallbackHandler
{
        private TelegramClientWrapper client;
        private IMonitorDataService dataService;

        public SelectBuildingHandler(TelegramClientWrapper client, IMonitorDataService dataService)
        {
            this.client = client;
            this.dataService = dataService;
        }

        public Task Handle(CallbackQuery callbackQuery)
        {
            throw new NotImplementedException();
        }
    }
class ConfirmAddressHandler : IAddressCallbackHandler
{
        private TelegramClientWrapper client;
        private IMonitorDataService dataService;

        public ConfirmAddressHandler(TelegramClientWrapper client, IMonitorDataService dataService)
        {
            this.client = client;
            this.dataService = dataService;
        }

        public Task Handle(CallbackQuery callbackQuery)
        {
            throw new NotImplementedException();
        }
    }
    class ChatSelectorHandler : IAddressCallbackHandler
    {
        public Task Handle(CallbackQuery callbackQuery)
        {
            throw new NotImplementedException();
        }
    }