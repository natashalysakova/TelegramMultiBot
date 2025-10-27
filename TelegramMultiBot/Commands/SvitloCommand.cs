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

            await client.SendMessageAsync(message.Chat.Id, "here", keyboard, message.MessageThreadId);
        }

        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data?.Split('|', StringSplitOptions.RemoveEmptyEntries);

            if (data.Length == 2)
            {
                var region = data[1];
                var isSubscribed = monitorService.IsSubscribed(callbackQuery.Message.Chat.Id, region);

                InlineKeyboardButton subScriptionAction;
                if (isSubscribed)
                {
                    subScriptionAction = InlineKeyboardButton.WithCallbackData("Відписатися", callbackQuery.Data + "|unsub");
                }
                else
                {
                    subScriptionAction = InlineKeyboardButton.WithCallbackData("Підписатися", callbackQuery.Data + "|sub");
                }

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new InlineKeyboardButton[]
                    {
                        InlineKeyboardButton.WithCallbackData("Поточний графік", callbackQuery.Data + "|see"),
                        subScriptionAction
                    }
                });
                await client.SendMessageAsync(callbackQuery.Message.Chat.Id, "І шо?", keyboard, messageThreadId: callbackQuery.Message?.MessageThreadId);
            }
            else if (data.Length == 3)
            {
                var region = data[1];
                var action = data[2];
                switch (action)
                {
                    case "see":
                        monitorService.SendExisiting(callbackQuery.Message.Chat.Id, region);
                        break;
                    case "sub":
                        int id = monitorService.AddDtekJob(callbackQuery.Message.Chat.Id, region);
                        await client.SendMessageAsync(callbackQuery.Message.Chat.Id, "Підписка успішно оформлена!", messageThreadId: callbackQuery.Message?.MessageThreadId);
                        monitorService.SendExisiting(id);
                        break;
                    case "unsub":
                        monitorService.DisableJob(callbackQuery.Message.Chat.Id, region, "svitlo user action");
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
    }
}
