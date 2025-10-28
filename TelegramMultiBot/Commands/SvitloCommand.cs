using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.ImageCompare;

namespace TelegramMultiBot.Commands
{

    [ServiceKey("svitlo")]
    internal class SvitloCommand(TelegramClientWrapper client, MonitorService monitorService, ILogger<SvitloCommand> logger) : BaseCommand, ICallbackHandler
    {
        private string supportedRegions = "регіони що підтримуються: krem - Київська область, kem - м. Київ";

        public async override Task Handle(Message message)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("м.Київ", "svitlo|kem"),
                    InlineKeyboardButton.WithCallbackData("Київська область", "svitlo|krem")
                }
            });

            await client.SendMessageAsync(message.Chat.Id, "Обери локацію", keyboard, message.MessageThreadId);
        }

        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data?.Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (data.Length == 2)
            {
                var region = data[1];
                var isSubscribed = await monitorService.IsSubscribed(callbackQuery.Message.Chat.Id, region); 

                InlineKeyboardButton subScriptionAction;
                if (isSubscribed["all"])
                {
                    subScriptionAction = InlineKeyboardButton.WithCallbackData("❌ Відписатися усі групи", callbackQuery.Data + "|unsub");
                }
                else
                {
                    subScriptionAction = InlineKeyboardButton.WithCallbackData("✅ Підписатися усі групи", callbackQuery.Data + "|sub");
                }

                var keyboard = new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
                {
                    new List<InlineKeyboardButton>()
                    {
                        InlineKeyboardButton.WithCallbackData("⚡️ Поточний графік", callbackQuery.Data + "|see"),
                        subScriptionAction
                    }
                });

                var buttons = new List<InlineKeyboardButton>();

                keyboard.AddNewRow();
                for (int i = 0; i < isSubscribed.Count; i++)
                {
                    if (i % 2 == 0)
                    {
                        keyboard.AddNewRow();
                    }

                    var subscription = isSubscribed.ElementAt(i);
                    if (subscription.Key == "all")
                        continue;

                    var groupName = subscription.Key.Replace("GPV", "Група ");

                    string buttonText = subscription.Value ? "❌ " + groupName : "✅ " + groupName;
                    string callbackData = callbackQuery.Data + "|" + (subscription.Value ? "unsub_" : "sub_") + subscription.Key;

                    keyboard.AddButton(InlineKeyboardButton.WithCallbackData(buttonText, callbackData));
                }

                keyboard.AddNewRow(buttons.ToArray());


                await client.SendMessageAsync(callbackQuery.Message.Chat.Id, $"Графіки {GetLocation(region)}. Хочеш подивитися актуальні графіки чи керувати автоматичними оновленнями в цьому чаті? ", keyboard, messageThreadId: callbackQuery.Message?.MessageThreadId);
            }
            else if (data.Length == 3)
            {
                var region = data[1];
                var action = data[2];
                switch (action)
                {
                    case "see":
                        await monitorService.SendExisiting(callbackQuery.Message.Chat.Id, region, callbackQuery.Message.MessageThreadId);
                        break;
                    case "sub":
                        int id = await monitorService.AddDtekJob(callbackQuery.Message.Chat.Id, region, callbackQuery.Message.MessageThreadId);
                        if (id == -1)
                        {
                            await client.SendMessageAsync(callbackQuery.Message.Chat.Id, "Шось я не впевнений що знаю про світло в цій локації", messageThreadId: callbackQuery.Message?.MessageThreadId);
                            break;
                        }

                        await client.SendMessageAsync(callbackQuery.Message.Chat.Id, "Підписка успішно оформлена!", messageThreadId: callbackQuery.Message?.MessageThreadId);
                        await monitorService.SendExisiting(id);
                        break;
                    case "unsub":
                        await monitorService.DisableJob(callbackQuery.Message.Chat.Id, region, "svitlo user action");
                        await client.SendMessageAsync(callbackQuery.Message.Chat.Id, "Підписка успішно видалена!", messageThreadId: callbackQuery.Message?.MessageThreadId);
                        break;

                }
            }
            else
            {
                await client.SendMessageAsync(callbackQuery.Message.Chat.Id, "Я шось нічо не поняв, яку кнопочку ти жмав", messageThreadId: callbackQuery.Message?.MessageThreadId);
            }

            await client.AnswerCallbackQueryAsync(callbackQuery.Id);
        }
        private static string GetLocation(string region)
        {
            switch (region)
            {
                case "krem":
                    return "для Київської області";
                case "kem":
                    return "для м.Київ";
                default:
                    return string.Empty;
            }
        }
    }

}
