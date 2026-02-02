using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Extensions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Database;

namespace TelegramMultiBot.Commands;

[ServiceKey("alerts", "Отримати наявні алерти по локаціях", isPublic: false)]
internal class AlertsCommand(TelegramClientWrapper client, ILogger<CookieCommand> logger, BoberDbContext context) : BaseCommand
{
    public override async Task Handle(Message message)
    {
        try
        {
            await HandleMessageInternal(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle alerts command");
        }
    }

    private async Task HandleMessageInternal(Message message)
    {
        var alerts = await context.Alerts
            .Include(x => x.Location)
            .Where(x => x.ResolvedAt == null)
            .GroupBy(x => x.Location)
            .ToListAsync();

        if (alerts.Any())
        {
            string responce = "Знайдені алерти:\n";

            foreach (var location in alerts)
            {
                responce += $"{location.Key.Region}: {location.Sum(x => x.FailureCount)} невдалих спроб в {location.Count()} алертах\n";
            }

            var escaped = Markdown.Escape(responce);


            await client.SendMessageAsync(message.Chat, escaped, parseMode: ParseMode.Markdown, messageThreadId: message.MessageThreadId);
        }
        else
        {
            await client.SendMessageAsync(message.Chat, "Алертів не знайдено", messageThreadId: message.MessageThreadId);
        }
    }
}
