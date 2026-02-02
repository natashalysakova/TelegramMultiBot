using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.Database;

namespace TelegramMultiBot.Commands;

[ServiceKey("group", "Дізнатися групу відключень для адрес цього чату", isPublic: true)]
internal class GroupCommand(TelegramClientWrapper client, ILogger<GroupCommand> logger, BoberDbContext context) : BaseCommand
{
    public override async Task Handle(Message message)
    {
        var chatId = message.Chat.Id;

        var addressJobs = await context.AddressJobs
            .Include(aj => aj.Location)
            .Where(x => x.ChatId == chatId && x.IsActive)
            .ToListAsync();

        if (!addressJobs.Any())
        {
            await client.SendMessageAsync(message.Chat.Id, "У цьому чаті немає активних адресних завдань.", messageThreadId: message.MessageThreadId);
            return;
        }

        var responseLines = addressJobs.Select(job => 
        {
            var line = $"Адреса: {job.City}, {job.Street}, {job.Number}\n";
            if(job.BuildingId == null)
            {
                return $"{line}Група відключень: Невідомо (будівля не знайдена)";
            }

            var groupCodes = context.Buildings.FirstOrDefault(b => b.Id == job.BuildingId)?.GroupNames;
            if(groupCodes == null || !groupCodes.Any())
            {
                return $"{line}Група відключень: Невідомо (групи не знайдені)";
            }

            var groupNames = context.ElectricityGroups
                .Where(g => g.LocationRegion == job.Location.Region && groupCodes.Contains(g.GroupCode))
                .Select(g => g.GroupName)
                .ToList();

            var groupInfo = string.Join(", ", groupNames);
            return $"{line}Група відключень: {groupInfo}";
        });

        var response = string.Join("\n\n", responseLines);

        await client.SendMessageAsync(message.Chat.Id, response, messageThreadId: message.MessageThreadId);
    }
}
