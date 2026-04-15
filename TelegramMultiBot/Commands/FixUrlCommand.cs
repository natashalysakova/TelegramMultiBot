using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System.Text.Json;
using System.Text.Json.Nodes;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Database;
using VideoDownloader.Client;

namespace TelegramMultiBot.Commands;

internal class FixUrlCommand(TelegramClientWrapper client, MeTubeClient meTubeClient, BoberDbContext context, ILogger<FixUrlCommand> logger) : BaseCommand
{
    public override bool CanHandle(Message message)
    {
        if (message.Text is null)
            return false;

        return _serviceItems.Any(x => message.Text.Contains(x, StringComparison.OrdinalIgnoreCase))
            && !_fallbackDomains.Any(x => message.Text.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    public override bool CanHandle(InlineQuery query)
    {
        return false;
    }

    public override async Task Handle(Message message)
    {
        if (message.Text is null)
            throw new NullReferenceException(nameof(message.Text));

        var links = message.Text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.StartsWith("https://")
                && _serviceItems.Any(s => x.Contains(s, StringComparison.OrdinalIgnoreCase))
                && !_fallbackDomains.Any(f => x.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var userComment = GetUserComment(message.Text, links);

        bool canDeleteMessages = false;
        if (client.BotId != null)
        {
            var bot = await client.GetChatMemberAsync(message.Chat, client.BotId.Value);
            canDeleteMessages = bot.Status == ChatMemberStatus.Administrator || message.Chat.Type == ChatType.Private;
        }

        foreach (var link in links)
        {
            string statusText;
            if (canDeleteMessages)
            {
                statusText = $"🦫 {GetUserName(message.From)}: {message.Text}\n⏳ Завантаження відео...";
            }
            else
            {
                statusText = $"⏳ Завантаження відео...";
            }

            var statusMessage = await client.SendMessageAsync(message, statusText, !canDeleteMessages, disableNotification: true);
            var presets = GetPresetList(link);
            var id = Guid.NewGuid();

            try
            {
                var response = await meTubeClient.AddDownload(link, id.ToString(), presets);
                if (response?.Status == MeTubeStatus.Ok)
                {
                    context.VideoDownloads.Add(new VideoDownload
                    {
                        Id = id,
                        VideoUrl = link,
                        ChatId = message.Chat.Id,
                        MessageThreadId = message.IsTopicMessage ? message.MessageThreadId ?? 0 : 0,
                        BotMessage = statusMessage.MessageId,
                        MessageToDelete = canDeleteMessages ? message.MessageId : 0,
                        RequestedBy = GetUserName(message.From),
                        UserComment = userComment,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    await context.SaveChangesAsync();
                }
                else
                {
                    await client.EditMessageTextAsync(statusMessage, "❌ Не вдалося поставити відео в чергу завантаження");
                    logger.LogError("Failed to add download for link {Link}. Response: {Response}", link, response);
                }
            }
            catch (Exception ex)
            {
                await client.EditMessageTextAsync(statusMessage, "❌ Не вдалося поставити відео в чергу завантаження");
                logger.LogError("Failed to add download for link {Link}. Exception: {Exception}", link, ex.Message);
            }
        }
    }

    private string[] GetPresetList(string link)
    {
        var uri = new Uri(link);
        var host = uri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>()
        {
            "default"
        };

        try
        {
            var json = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText("/config/ytdl-presets.json"));
            var availblePresets = host.Where(x => json.ContainsKey(x));
            result.AddRange(availblePresets);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to read perset file: {error}", ex.Message);
        }

        return result.ToArray();
    }

    private static string GetUserName(User? user)
    {
        if (user is null) return "Unknown";
        return user.Username is null ? user.FirstName : "@" + user.Username;
    }

    private static string? GetUserComment(string messageText, IEnumerable<string> links)
    {
        var comment = links.Aggregate(messageText, (text, link) => text.Replace(link, string.Empty)).Trim();
        return string.IsNullOrWhiteSpace(comment) ? null : comment;
    }

    private readonly List<string> _serviceItems =
    [
        "instagram.com",
        "x.com",
        "twitter.com",
        "facebook.com",
        "youtube.com/shorts/"
    ];

    private readonly List<string> _fallbackDomains =
    [
        "fixupx.com",
        "fxtwitter.com",
        "kksave.com",
        "ddinstagram.com",
        "kkinstagram.com"
    ];
}