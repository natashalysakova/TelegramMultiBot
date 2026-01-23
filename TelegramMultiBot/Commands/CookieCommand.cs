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
                setting = await context.Settings.SingleOrDefaultAsync(x => x.SettingSection == "Svitlobot" && x.SettingsKey == "KremCookie");
                break;
            default:
                await client.SendMessageAsync(message.Chat.Id, "Невідомий регіон. Підтримувані регіони: krem, kem", messageThreadId: message.MessageThreadId);
                break;
        }

        if(setting == null)
        {
            await client.SendMessageAsync(message.Chat.Id, $"нема налаштувань кукі для цього регіону", messageThreadId: message.MessageThreadId);
            return;
        }

        setting.SettingsValue = cookieValue;

        var alerts = context.Alerts.Include(x=>x.Location).Where(x => x.Location.Region == region && x.isResolved == false);
        foreach (var item in alerts)
        {
            item.ResolvedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync();
        await client.SendMessageAsync(message.Chat.Id, $"Кука для регіону {region} успішно встановлена!", messageThreadId: message.MessageThreadId);

    }
}
