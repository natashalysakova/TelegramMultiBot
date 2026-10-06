using DtekParsers;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.BackgroundServies;
using TelegramMultiBot.Commands.Interfaces;

namespace TelegramMultiBot.Commands;

[ServiceKey("svitlo", "Бобер-Електрик")]
internal class SvitloCommand(TelegramClientWrapper client, MonitorService monitorService, ILogger<SvitloCommand> logger) : BaseCommand, ICallbackHandler
{
    private const int GroupsPerPage = 20;

    private string supportedRegions = "регіони що підтримуються: krem - Київська область, kem - м. Київ";

    public override bool CanHandle(Message message)
    {
        return base.CanHandle(message) && !message.Text!.StartsWith("/svitlobot") && !message.Text!.StartsWith("/svitloRun");
    }

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

    // "GPV10.1" -> (10, 1); unparsable parts sort last
    private static (int Major, int Minor) GroupSortKey(string key)
    {
        var parts = key.TrimStart("GPVgpv".ToCharArray()).Split('.');
        var major = int.TryParse(parts[0], out var a) ? a : int.MaxValue;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;
        return (major, minor);
    }

    private async Task<InlineKeyboardMarkup> BuildGroupsKeyboard(long chatId, string region, int page)
    {
        var isSubscribed = await monitorService.IsSubscribed(chatId, region);
        var baseData = "svitlo|" + region;

        var subScriptionAction = isSubscribed["all"]
            ? InlineKeyboardButton.WithCallbackData("❌ Відписатися усі групи", baseData + "|unsub")
            : InlineKeyboardButton.WithCallbackData("✅ Підписатися усі групи", baseData + "|sub");

        var keyboard = new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
        {
            new List<InlineKeyboardButton>()
            {
                InlineKeyboardButton.WithCallbackData("⚡️ Поточний графік всіх груп", baseData + "|see"),
                subScriptionAction
            }
        });

        var groups = isSubscribed
            .Where(s => s.Key != "all")
            .OrderBy(s => GroupSortKey(s.Key).Major)
            .ThenBy(s => GroupSortKey(s.Key).Minor)
            .ThenBy(s => s.Key, StringComparer.Ordinal)
            .ToList();
        var totalPages = Math.Max(1, (groups.Count + GroupsPerPage - 1) / GroupsPerPage);
        page = Math.Clamp(page, 0, totalPages - 1);

        foreach (var subscription in groups.Skip(page * GroupsPerPage).Take(GroupsPerPage))
        {
            keyboard.AddNewRow();

            var groupName = subscription.Key.Replace("GPV", "Група ");
            keyboard.AddButton(InlineKeyboardButton.WithCallbackData("⚡️" + groupName, baseData + "|see_" + subscription.Key));

            string buttonText = subscription.Value ? "❌ Відписатися" : "✅ Підписатися";
            string callbackData = baseData + "|" + (subscription.Value ? "unsub_" : "sub_") + subscription.Key;
            keyboard.AddButton(InlineKeyboardButton.WithCallbackData(buttonText, callbackData));

            keyboard.AddButton(InlineKeyboardButton.WithCallbackData("📝 План", baseData + "|plan_" + subscription.Key));
        }

        if (totalPages > 1)
        {
            var nav = new List<InlineKeyboardButton>();
            if (page > 0)
                nav.Add(InlineKeyboardButton.WithCallbackData("⬅️ Назад", baseData + "|page_" + (page - 1)));
            nav.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", baseData + "|page_" + page));
            if (page < totalPages - 1)
                nav.Add(InlineKeyboardButton.WithCallbackData("Вперед ➡️", baseData + "|page_" + (page + 1)));
            keyboard.AddNewRow(nav.ToArray());
        }

        return keyboard;
    }

    public async Task HandleCallback(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Data == null)
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
            var keyboard = await BuildGroupsKeyboard(callbackQuery.Message.Chat.Id, region, 0);

            await client.SendMessageAsync(callbackQuery.Message.Chat.Id, $"Графіки {LocationNameUtility.GetLocationByRegion(region)}", keyboard, messageThreadId: callbackQuery.Message?.MessageThreadId);
        }
        else if (data.Length == 3)
        {
            var region = data[1];
            var action = data[2];

            if (action.StartsWith("page_") && int.TryParse(action.AsSpan(5), out var page))
            {
                var pageKeyboard = await BuildGroupsKeyboard(callbackQuery.Message.Chat.Id, region, page);
                await client.EditMessageReplyMarkupAsync(callbackQuery.Message, pageKeyboard);
                await client.AnswerCallbackQueryAsync(callbackQuery.Id);
                return;
            }

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
                        await monitorService.SendExisiting(callbackQuery.Message.Chat.Id, region, group, Database.Models.ElectricityJobType.SingleGroup, callbackQuery.Message.MessageThreadId);
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
