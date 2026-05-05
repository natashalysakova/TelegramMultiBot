using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Database;
using VideoDownloader.Client;

namespace VideoDownloader;

public interface IVideoProcessHandler
{
    Task AddDownloads(Message message, CancellationToken cancellationToken = default);
    Task ProcessDownloads(CancellationToken cancellationToken);
}

public class VideoProcessHandler(ILogger<VideoProcessHandler> logger, TelegramBotClient telegramBotClient, MeTubeClient meTubeClient, BoberDbContext db) : IVideoProcessHandler
{
    public async Task ProcessDownloads(CancellationToken cancellationToken)
    {
        var pendingJobs = await db.VideoDownloads
            .Where(x => x.Status == VideoDownloadStatus.Pending)
            .ToListAsync(cancellationToken);

        if (!pendingJobs.Any())
            return;

        MeTubeHistoryResponse? history;
        try
        {
            history = await meTubeClient.GetHistory();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve MeTube history");
            return;
        }

        if (history == null)
            return;

        foreach (var item in history.Done)
        {
            var job = pendingJobs.FirstOrDefault(x => item.Id.Contains(x.Id.ToString()));
            if (job == null)
                continue;

            if (item.Status == "finished" && item.Filename != null)
                await HandleFinishedDownload(job, item, cancellationToken);
            else if (item.Status == "error")
                await HandleFailedDownload(job, item, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        var allJobsGuid = history.GetAllIds().Select(id => Guid.Parse(id.Split('.', StringSplitOptions.RemoveEmptyEntries).First()));
        var missingJobs = pendingJobs.ToList().Where(j => j.Status == VideoDownloadStatus.Pending && !allJobsGuid.Contains(j.Id));

        foreach (var item in missingJobs)
        {
            await HandleMissingJob(item, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleMissingJob(VideoDownload job, CancellationToken cancellationToken)
    {
        job.Status = VideoDownloadStatus.Failed;
        var fallbackUrl = GetFallbackUrlForNoVideo(job.VideoUrl);

        if (fallbackUrl != job.VideoUrl)
        {
            //await EditStatusMessage(job, $"Відео не знайдено\n{fallbackUrl}", cancellationToken);
            await HandleFailedDownload(job, "There is no video in this post", fallbackUrl, cancellationToken);
        }
        else
        {
            await EditStatusMessage(job, $"В посту немає відео", cancellationToken);
        }
    }

    private const long MaxFileSizeBytes = 50L * 1024 * 1024;

    private async Task HandleFinishedDownload(VideoDownload job, MeTubeHistoryItem item, CancellationToken cancellationToken)
    {
        if (item.Size.HasValue && item.Size.Value > MaxFileSizeBytes)
        {
            logger.LogWarning("Video too large ({size} bytes) for Telegram, falling back to URL replacement for {url}", item.Size.Value, item.Url);
            logger.LogTrace("Oversized video — JobId: {jobId}, ChatId: {chatId}, BotMessage: {msgId}, Size: {size}", job.Id, job.ChatId, job.BotMessage, item.Size.Value);
            await HandleOversizedDownload(job, item, cancellationToken);
            return;
        }

        try
        {
            using var response = await meTubeClient.GetFileResponseAsync(item.Filename!, cancellationToken);

            using var ms = new MemoryStream();
            await response.Content.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            int? videoWidth = null, videoHeight = null;
            try
            {
                using var tagFile = TagLib.File.Create(new StreamAbstraction(item.Filename!, ms));
                videoWidth = tagFile.Properties.VideoWidth > 0 ? tagFile.Properties.VideoWidth : null;
                videoHeight = tagFile.Properties.VideoHeight > 0 ? tagFile.Properties.VideoHeight : null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read video dimensions from {filename}", item.Filename);
            }
            ms.Position = 0;

            var chatId = new ChatId(job.ChatId);
            var inputFile = InputFile.FromStream(ms, item.Filename);

            bool sendWithCaption = true;
            var caption = $"Відео від {job.RequestedBy}."
                + (string.IsNullOrWhiteSpace(job.UserComment) && job.MessageToDelete == 0 ? string.Empty : $"\n{job.UserComment}")
                + $"\nОригінальне відео: {job.VideoUrl}";

            if (caption.Length > 1024)
            {
                caption = $"Відео від {job.RequestedBy}."
                + $"\nОригінальне відео: {job.VideoUrl}";
                sendWithCaption = false;
            }

            if (!sendWithCaption)
            {
                await telegramBotClient.SendRequest(new SendMessageRequest
                {
                    ChatId = chatId,
                    Text = $"Повідомлення від {job.RequestedBy}:\n{job.UserComment}",
                    MessageThreadId = job.MessageThreadId == 0 ? null : job.MessageThreadId,
                    DisableNotification = true,
                }, cancellationToken);
            }
            await telegramBotClient.SendRequest(new SendVideoRequest
            {
                ChatId = chatId,
                Video = inputFile,
                Width = videoWidth,
                Height = videoHeight,
                MessageThreadId = job.MessageThreadId == 0 ? null : job.MessageThreadId,
                Caption = caption,
                ShowCaptionAboveMedia = false,
                SupportsStreaming = true
            }, cancellationToken);



            logger.LogInformation("Video sent for {url}", item.Url);

            job.Status = VideoDownloadStatus.Completed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send video for {url}", item.Url);
            logger.LogTrace("Send failure — JobId: {jobId}, ChatId: {chatId}, BotMessage: {msgId}, Filename: {filename}", job.Id, job.ChatId, job.BotMessage, item.Filename);
            job.Status = VideoDownloadStatus.Failed;

            var fallbackUrl = GetFallbackUrl(job.VideoUrl);
            await EditStatusMessage(job, $"❌ Помилка надсилання відео\n{fallbackUrl}", cancellationToken);
        }

        if (job.Status == VideoDownloadStatus.Completed)
        {
            await DoCleanup(job, item, cancellationToken);
        }
    }

    private async Task DoCleanup(VideoDownload job, MeTubeHistoryItem item, CancellationToken cancellationToken)
    {
        await DeleteStatusMessage(job, cancellationToken);
        await DeleteOriginalMessage(job, cancellationToken);
        await meTubeClient.DeleteDownload(item.Url);
        db.VideoDownloads.Remove(job);
    }

    private async Task HandleOversizedDownload(VideoDownload job, MeTubeHistoryItem item, CancellationToken cancellationToken)
    {
        var fallbackUrl = GetFallbackUrl(job.VideoUrl);
        var sizeMb = item.Size!.Value / (1024 * 1024);

        string statusText = $"⚠️ Відео завелике для Telegram ({sizeMb} MB)."
            + (fallbackUrl != job.VideoUrl ? $" {fallbackUrl}" : string.Empty);

        await EditStatusMessage(job, statusText, cancellationToken);

        try
        {
            await meTubeClient.DeleteDownload(item.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete oversized download {id}", item.Id);
        }

        db.VideoDownloads.Remove(job);
    }

    public static string GetFallbackUrl(string videoUrl)
    {
        var uri = new Uri(videoUrl);
        if (uri.Host.Equals("instagram.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.instagram.com", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(uri) { Host = "kksav.com" };
            return builder.Uri.ToString();
        }

        return GetFallbackUrlForNoVideo(videoUrl);
    }

    public static string GetFallbackUrlForNoVideo(string videoUrl)
    {
        var uri = new Uri(videoUrl);
        if (uri.Host.Equals("x.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.x.com", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(uri) { Host = "fixupx.com" };
            return builder.Uri.ToString();
        }
        if (uri.Host.Equals("twitter.com", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("www.twitter.com", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(uri) { Host = "fxtwitter.com" };
            return builder.Uri.ToString();
        }

        return videoUrl;
    }

    private async Task HandleFailedDownload(VideoDownload job, MeTubeHistoryItem item, CancellationToken cancellationToken)
    {
        await HandleFailedDownload(job, item.Id, item.Message, null, cancellationToken);
    }

    private async Task HandleFailedDownload(VideoDownload job, string errorMessage, CancellationToken cancellationToken)
    {
        await HandleFailedDownload(job, null, errorMessage, null, cancellationToken);
    }

    private async Task HandleFailedDownload(VideoDownload job, string errorMessage, string fallbackUrl, CancellationToken cancellationToken)
    {
        await HandleFailedDownload(job, null, errorMessage, fallbackUrl, cancellationToken);
    }

    private async Task HandleFailedDownload(VideoDownload job, string? itemId, string? errorMessage, string? fallbackUrl, CancellationToken cancellationToken)
    {
        logger.LogWarning("Download failure — JobId: {jobId}, {msgId}", job.Id, errorMessage);

        if (fallbackUrl is null)
        {
            fallbackUrl = GetFallbackUrl(job.VideoUrl);
        }

        string text;
        bool deleteOriginal = false;
        if (errorMessage != null && (errorMessage.Contains("No video formats found") || errorMessage.Contains("Unsupported URL")))
        {
            text = fallbackUrl;
            deleteOriginal = true;
        }
        else if (errorMessage != null && errorMessage.Contains("There is no video in this post"))
        {
            text = "В посту немає відео";
        }
        else
        {
            text = $"❌ Помилка завантаження відео\n{fallbackUrl}";
        }

        if (deleteOriginal)
        {
            await DeleteOriginalMessage(job, cancellationToken);
            await DeleteStatusMessage(job, cancellationToken);
            // If the entity is in the Added state, it means it hasn't been saved to the database yet.
            // Detach it instead of removing, as Remove is only valid for entities tracked as Unchanged/Modified/Deleted.
            // This prevents errors when handling failed downloads that were never persisted.
            var entry = db.Entry(job);
            if (entry.State == EntityState.Added)
                entry.State = EntityState.Detached;
            else if (entry.State != EntityState.Detached)
                db.VideoDownloads.Remove(job);

            await telegramBotClient.SendRequest(new SendMessageRequest()
            {
                ChatId = job.ChatId,
                MessageThreadId = job.MessageThreadId,
                Text = $"{job.RequestedBy}: {text}" + (job.UserComment != null ? $"\n{job.UserComment}" : string.Empty)
            }, cancellationToken);
        }
        else
        {
            await EditStatusMessage(job, text, cancellationToken);
            job.Status = VideoDownloadStatus.Failed;
        }

        if (itemId != null)
        {
            try
            {
                await meTubeClient.DeleteDownload(itemId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not delete errored download {id}", itemId);
            }
        }
    }


    private async Task DeleteStatusMessage(VideoDownload job, CancellationToken cancellationToken)
    {
        if (job.BotMessage <= 0)
            return;

        try
        {
            await telegramBotClient.SendRequest(new DeleteMessageRequest
            {
                ChatId = new ChatId(job.ChatId),
                MessageId = job.BotMessage
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete status message {messageId}", job.BotMessage);
        }
    }

    private async Task DeleteOriginalMessage(VideoDownload job, CancellationToken cancellationToken)
    {
        if (job.MessageToDelete is null || job.MessageToDelete <= 0)
            return;

        var activeJobsFromMessage = await db.VideoDownloads.AnyAsync(x =>
            x.Id != job.Id &&
            x.MessageToDelete == job.MessageToDelete &&
            x.ChatId == job.ChatId, cancellationToken);

        if (activeJobsFromMessage)
        {
            return;
        }

        // only for last job for that specific message - remove the message
        try
        {
            await telegramBotClient.SendRequest(new DeleteMessageRequest
            {
                ChatId = new ChatId(job.ChatId),
                MessageId = job.MessageToDelete.Value
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete original message {messageId}", job.MessageToDelete);
        }
    }

    private async Task EditStatusMessage(VideoDownload job, string text, CancellationToken cancellationToken)
    {
        if (job.BotMessage <= 0)
            return;

        try
        {
            await telegramBotClient.SendRequest(new EditMessageTextRequest
            {
                ChatId = new ChatId(job.ChatId),
                MessageId = job.BotMessage,
                Text = text,
                LinkPreviewOptions = new LinkPreviewOptions()
                {
                    IsDisabled = false,
                    PreferLargeMedia = true
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not edit status message {messageId}", job.BotMessage);
        }
    }

    public async Task AddDownloads(Message message, CancellationToken cancellationToken)
    {

        var links = message.Text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.StartsWith("https://")
                && ServiceItems.Any(s => x.Contains(s, StringComparison.OrdinalIgnoreCase))
                && !FallbackDomains.Any(f => x.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var userComment = GetUserComment(message.Text, links);

        bool canDeleteMessages = false;
        var bot = await telegramBotClient.GetChatMember(message.Chat, telegramBotClient.BotId);
        canDeleteMessages = bot.Status == ChatMemberStatus.Administrator || message.Chat.Type == ChatType.Private;

        foreach (var link in links)
        {
            string statusText = $"⏳ Завантаження відео...";

            //var statusMessage = await telegramBotClient.SendMessageAsync(message, statusText, !canDeleteMessages, disableNotification: true);
            var statusMessage = await telegramBotClient.SendRequest(new SendMessageRequest
            {
                ChatId = message.Chat.Id,
                MessageThreadId = message.MessageThreadId,
                DisableNotification = true,
                Text = statusText,
                ReplyParameters = new ReplyParameters
                {
                    ChatId = message.Chat.Id,
                    MessageId = message.MessageId
                }
            });
            var presets = GetPresetList(link);
            var id = Guid.NewGuid();

            var userName = GetUserName(message.From);
            var job = new VideoDownload
            {
                Id = id,
                VideoUrl = link,
                ChatId = message.Chat.Id,
                MessageThreadId = message.IsTopicMessage ? message.MessageThreadId ?? 0 : 0,
                BotMessage = statusMessage.MessageId,
                MessageToDelete = canDeleteMessages ? message.MessageId : 0,
                RequestedBy = userName,
                UserComment = userComment,
                CreatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                db.VideoDownloads.Add(job);

                var response = await meTubeClient.AddDownload(link, id.ToString(), presets);
                if (response?.Status != MeTubeStatus.Ok)
                {
                    throw new Exception($"Failed to add download for link {link}. Response: {response}");
                }
            }
            catch (Exception ex)
            {
                await HandleFailedDownload(job, ex.Message, cancellationToken);
            }
            finally
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private static readonly string CongifPath = "/config/ytdl-presets.json";

    private string[] GetPresetList(string link)
    {
        var uri = new Uri(link);
        var result = new List<string>()
        {
            "default"
        };

        var alias = GetLinkAlias(link);
        try
        {
            if (!File.Exists(CongifPath))
            {
                logger.LogWarning("Preset file not found at {path}, using default preset only", CongifPath);
                return result.ToArray();
            }

            var text = File.ReadAllText(CongifPath);
            logger.LogInformation(text);

            var json = JsonSerializer.Deserialize<JsonObject>(text);
            if (json.ContainsKey(alias))
            {
                result.Add(alias);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to read preset file: {error}", ex.Message);
        }

        return result.ToArray();
    }

    private string GetLinkAlias(string link)
    {
        var host = new Uri(link).Host;

        foreach (var aliasEntry in aliases)
        {
            foreach (var aliasHost in aliasEntry.Value)
            {
                if (aliasHost == host)
                {
                    return aliasEntry.Key;
                }
            }
        }
        return host;
    }

    private static readonly Dictionary<string, string[]> aliases = new Dictionary<string, string[]>()
    {
        { "instagram", ["www.instagram.com", "instagram.com"] },
        { "twitter", ["www.x.com", "x.com", "www.twitter.com", "twitter.com"] },
        { "youtube", ["www.youtube.com", "youtube.com", "www.youtu.be", "youtu.be"] }
    };

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

    public static readonly IEnumerable<string> ServiceItems =
    [
        "instagram.com",
        "x.com",
        "twitter.com",
        "facebook.com",
        "youtube.com/shorts/"
    ];

    public static readonly IEnumerable<string> FallbackDomains =
    [
        "fixupx.com",
        "fxtwitter.com",
        "kksave.com",
        "kksav.com",
        "ddinstagram.com",
        "kkinstagram.com"
    ];
}

sealed class StreamAbstraction(string name, Stream stream) : TagLib.File.IFileAbstraction
{
    public string Name { get; } = name;
    public Stream ReadStream { get; } = stream;
    public Stream WriteStream { get; } = stream;
    public void CloseStream(Stream s) { }
}