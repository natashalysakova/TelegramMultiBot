using AutoMapper;
using DtekParsers;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.ImageCompare;

namespace TelegramMultiBot.Commands;


[ServiceKey("svitlo", "Бобер-Електрик")]
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
        if(callbackQuery.Data == null)
        {
            logger.LogWarning("CallbackQuery.Data is null in SvitloCommand");
            return;
        }

        if (callbackQuery.Message is null)
        {
            logger.LogWarning("CallbackQuery.Message is null in SvitloCommand");
            return;
        }

        var data = callbackQuery.Data.Split('|', StringSplitOptions.RemoveEmptyEntries);

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
                    InlineKeyboardButton.WithCallbackData("⚡️ Поточний графік всіх груп", callbackQuery.Data + "|see"),
                    subScriptionAction
                }
            });

            var buttons = new List<InlineKeyboardButton>();

            keyboard.AddNewRow();
            for (int i = 0; i < isSubscribed.Count; i++)
            {

                keyboard.AddNewRow();


                var subscription = isSubscribed.ElementAt(i);
                if (subscription.Key == "all")
                    continue;

                var groupName = subscription.Key.Replace("GPV", "Група ");

                keyboard.AddButton(InlineKeyboardButton.WithCallbackData("⚡️" + groupName, callbackQuery.Data + "|see_" + subscription.Key));


                string buttonText = subscription.Value ? "❌ Відписатися" : "✅ Підписатися" ;
                string callbackData = callbackQuery.Data + "|" + (subscription.Value ? "unsub_" : "sub_") + subscription.Key;

                keyboard.AddButton(InlineKeyboardButton.WithCallbackData(buttonText, callbackData));

                keyboard.AddButton(InlineKeyboardButton.WithCallbackData("📝 План", callbackQuery.Data + "|plan_" + subscription.Key));
            }

            keyboard.AddNewRow(buttons.ToArray());


            await client.SendMessageAsync(callbackQuery.Message.Chat.Id, $"Графіки {LocationNameUtility.GetLocationByRegion(region)}", keyboard, messageThreadId: callbackQuery.Message?.MessageThreadId);
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
                    var id = await monitorService.AddDtekJob(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageThreadId, region, null);
                    if (id == Guid.Empty)
                    {
                        await client.SendMessageAsync(callbackQuery.Message.Chat.Id, "Шось я не впевнений що знаю про світло в цій локації", messageThreadId: callbackQuery.Message?.MessageThreadId);
                        break;
                    }

                    await client.SendMessageAsync(callbackQuery.Message.Chat.Id, $"Підписка на {LocationNameUtility.GetLocationByRegion(region)} успішно оформлена!", messageThreadId: callbackQuery.Message?.MessageThreadId);
                    await monitorService.SendExisiting(id);
                    break;
                case "unsub":
                    await monitorService.DisableJob(callbackQuery.Message.Chat.Id, region, null, "svitlo user action");
                    await client.SendMessageAsync(callbackQuery.Message.Chat.Id, $"Підписка на {LocationNameUtility.GetLocationByRegion(region)} успішно видалена!", messageThreadId: callbackQuery.Message?.MessageThreadId);
                    break;
                default: 
                    if (action.StartsWith("sub_"))
                    {
                        var group = action.Substring(4);

                        var jobId = await monitorService.AddDtekJob(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageThreadId, region, group);
                        if (jobId == Guid.Empty)
                        {
                            await client.SendMessageAsync(callbackQuery.Message.Chat.Id, $"Шось я не впевнений що знаю про світло в {group} цій локації", messageThreadId: callbackQuery.Message?.MessageThreadId);
                            break;
                        }

                        await client.SendMessageAsync(callbackQuery.Message.Chat.Id, $"Підписка на {group} успішно оформлена!", messageThreadId: callbackQuery.Message?.MessageThreadId);
                        await monitorService.SendExisiting(jobId);
                        break;

                    }
                    else if (action.StartsWith("unsub_"))
                    {
                        var group = action.Substring(6);
                        await monitorService.DisableJob(callbackQuery.Message.Chat.Id, region, group, "svitlo user action");
                        await client.SendMessageAsync(callbackQuery.Message.Chat.Id, $"Підписка на {group} успішно видалена!", messageThreadId: callbackQuery.Message?.MessageThreadId);
                        break;
                    }
                    else if (action.StartsWith("see_"))
                    {
                        var group = action.Substring(4);
                        await monitorService.SendExisiting(callbackQuery.Message.Chat.Id, region, group, Database.Models.ElectricityJobType.SingleGroup , callbackQuery.Message.MessageThreadId);
                        break;
                    }
                    else if (action.StartsWith("plan_"))
                    {
                        var group = action.Substring(5);
                        await monitorService.SendExisiting(callbackQuery.Message.Chat.Id, region, group, Database.Models.ElectricityJobType.SingleGroupPlan, callbackQuery.Message.MessageThreadId);
                        break;
                    }

                    break;

            }
            await client.EditMessageReplyMarkupAsync(callbackQuery.Message, null);

        }
        else
        {
            await client.SendMessageAsync(callbackQuery.Message.Chat.Id, "Я шось нічо не поняв, яку кнопочку ти жмав", messageThreadId: callbackQuery.Message?.MessageThreadId);
        }

        await client.AnswerCallbackQueryAsync(callbackQuery.Id);
    }
}
