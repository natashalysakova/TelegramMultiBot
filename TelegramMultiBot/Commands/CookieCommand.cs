using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.Database;

namespace TelegramMultiBot.Commands;

[ServiceKey("cookie", "Оновити куку для сайту", isPublic: false)]
internal class CookieCommand(TelegramClientWrapper client, ILogger<CookieCommand> logger, BoberDbContext context) : BaseCommand
{
    public override async Task Handle(Message message)
    {
        var splited = message.Text!.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (splited.Length != 3)
        {
            await client.SendMessageAsync(message.Chat.Id, "Невірний формат команди. Використання: /cookie <region> <cookie_value>", messageThreadId: message.MessageThreadId);
            return;
        }

        var region = splited[1];
        var cookieValue = splited[2];

        Database.Models.Settings? setting = null;

        switch (region)
        {
            case "krem":
                setting = await context.Settings.SingleOrDefaultAsync(x=>x.SettingSection=="Svitlobot" && x.SettingsKey=="KremCookie");
                break;
            case "kem":
                setting = await context.Settings.SingleOrDefaultAsync(x => x.SettingSection == "Svitlobot" && x.SettingsKey == "KemCookie");
                break;
            case "oem":
                setting = await context.Settings.SingleOrDefaultAsync(x => x.SettingSection == "Svitlobot" && x.SettingsKey == "OemCookie");
                break;
            default:
                await client.SendMessageAsync(message.Chat.Id, "Невідомий регіон. Підтримувані регіони: krem, kem, oem", messageThreadId: message.MessageThreadId);
                break;
        }

        if(setting == null)
        {
            await client.SendMessageAsync(message.Chat.Id, $"нема налаштувань кукі для цього регіону", messageThreadId: message.MessageThreadId);
            return;
        }

        setting.SettingsValue = cookieValue;
        try
        {
            var alerts = context.Alerts
            .Include(x=>x.Location)
            .Where(x => x.Location.Region == region && x.ResolvedAt == null);
            foreach (var item in alerts)
            {
                item.ResolvedAt = DateTimeOffset.UtcNow;
            }

            await context.SaveChangesAsync();
            await client.SendMessageAsync(message.Chat.Id, $"Кука для регіону {region} успішно встановлена!", messageThreadId: message.MessageThreadId);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to update cookie for region {Region}", region);
            await client.SendMessageAsync(message.Chat.Id, $"Не вдалося оновити куку для регіону {region}: {ex.Message}", messageThreadId: message.MessageThreadId);
        }
    }
}
