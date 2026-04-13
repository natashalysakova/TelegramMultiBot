using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Database;
using VideoDownloader.Client;
using static System.Net.WebRequestMethods;

namespace TelegramMultiBot.Commands;

internal class FixUrlCommand(TelegramClientWrapper client, MeTubeClient meTubeClient, BoberDbContext context) : BaseCommand
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

        foreach (var link in links)
        {
            bool canDeleteMessages = false;
            if (client.BotId != null)
            {
                var bot = await client.GetChatMemberAsync(message.Chat, client.BotId.Value);
                canDeleteMessages = bot.Status == ChatMemberStatus.Administrator || message.Chat.Type == ChatType.Private;
            }

            if (canDeleteMessages)
                await client.DeleteMessageAsync(message);

            string statusText = canDeleteMessages
                ? $"🦫 {GetUserName(message.From)}: {message.Text}\n⏳ Завантаження відео..."
                : "⏳ Завантаження відео...";

            var statusMessage = await client.SendMessageAsync(message, statusText, !canDeleteMessages, disableNotification: true);

            var response = await meTubeClient.AddDownload(link);
            if (response?.Status == MeTubeStatus.Ok)
            {
                context.VideoDownloads.Add(new VideoDownload
                {
                    Id = Guid.NewGuid(),
                    VideoUrl = link,
                    Status = "pending",
                    ChatId = message.Chat.Id,
                    MessageThreadId = message.IsTopicMessage ? message.MessageThreadId ?? 0 : 0,
                    BotMessage = statusMessage.MessageId,
                    MessageToDelete = 0,
                    RequestedBy = GetUserName(message.From),
                    UserComment = userComment
                });
                await context.SaveChangesAsync();
            }
            else
            {
                await client.EditMessageTextAsync(statusMessage, "❌ Не вдалося поставити відео в чергу завантаження");
            }
        }
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