using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.BackgroundServies;

namespace TelegramMultiBot.Commands;

[ServiceKey("grafic", "desc", isPublic: false)]
internal class GraficCommand(TelegramClientWrapper client, ILogger<GraficCommand> logger, IDtekSiteParserService dtekSiteParserService) : BaseCommand
{
    public override Task Handle(Message message)
    {
        dtekSiteParserService.ParseImmediately();
        return client.SendMessageAsync(message.Chat.Id, "Перевірка графіків світла розпочата!", messageThreadId: message.MessageThreadId);
    }
}
