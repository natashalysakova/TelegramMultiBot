using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System.Text.Json;
using System.Text.Json.Nodes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Database;
using TelegramMultiBot.Reminder;
using VideoDownloader;
using VideoDownloader.Client;

namespace TelegramMultiBot.Commands;

internal class FixUrlCommand(ILogger<FixUrlCommand> logger, IVideoProcessHandler videoProcessHandler) : BaseCommand
{
    public override bool CanHandle(Message message)
    {
        if (message.Text is null)
            return false;

        return VideoProcessHandler.ServiceItems.Any(x => message.Text.Contains(x, StringComparison.OrdinalIgnoreCase))
            && !VideoProcessHandler.FallbackDomains.Any(x => message.Text.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    public override bool CanHandle(InlineQuery query)
    {
        return false;
    }

    public override async Task Handle(Message message)
    {
        if (message.Text is null)
            throw new NullReferenceException(nameof(message.Text));

        await videoProcessHandler.AddDownloads(message);
    }
}