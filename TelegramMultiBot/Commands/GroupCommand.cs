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
            .Where(x => x.ChatId == chatId && x.IsActive)
            .ToListAsync();

        if (!addressJobs.Any())
        {
            await client.SendMessageAsync(message.Chat.Id, "У цьому чаті немає активних адресних завдань.", messageThreadId: message.MessageThreadId);
            return;
        }

        var responseLines = addressJobs.Select(job => $"Адреса: {job.City}, {job.Street}, {job.Building}\nГрупа відключень: {job.Group}");

        var response = string.Join("\n\n", responseLines);

        await client.SendMessageAsync(message.Chat.Id, response, messageThreadId: message.MessageThreadId);
    }
}
