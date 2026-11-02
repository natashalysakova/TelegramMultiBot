using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.Database;

namespace TelegramMultiBot.Commands;

[ServiceKey("last", "Отримати час останньої перевірки по локаціях", isPublic: true)]
internal class LastCheckedCommand(TelegramClientWrapper client, ILogger<LastCheckedCommand> logger, BoberDbContext context) : BaseCommand
{
    public override async Task Handle(Message message)
    {
        try
        {
            var locations = await context.ElectricityLocations
                .AsNoTracking()
                .OrderBy(x => x.Region)
                .ToListAsync();

            var response = locations.Count == 0
                ? "Локацій не знайдено"
                : string.Join("\n", locations.Select(x => $"{x.Region}: {x.LastChecked:dd.MM.yyyy HH:mm:ss}"));

            await client.SendMessageAsync(message.Chat, response, messageThreadId: message.MessageThreadId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle last command");
        }
    }
}
