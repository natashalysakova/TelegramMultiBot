using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using TelegramMultiBot.Database;
using VideoDownloader.Client;

namespace VideoDownloader;

public interface IVideoProcessHandler
{
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
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var chatId = new ChatId(job.ChatId);
            var inputFile = InputFile.FromStream(stream, item.Filename);

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

            await telegramBotClient.SendRequest(new SendVideoRequest
            {
                ChatId = chatId,
                Video = inputFile,
                MessageThreadId = job.MessageThreadId == 0 ? null : job.MessageThreadId,
                Caption = caption,
                ShowCaptionAboveMedia = true,
                SupportsStreaming = true
            }, cancellationToken);

            if (!sendWithCaption)
            {
                await telegramBotClient.SendRequest(new SendMessageRequest
                {
                    ChatId = chatId,
                    Text = $"Повідомлення від {job.RequestedBy}:\n{job.UserComment}",
                    MessageThreadId = job.MessageThreadId == 0 ? null : job.MessageThreadId,
                }, cancellationToken);
            }

            logger.LogInformation("Video sent for {url}", item.Url);

            job.Status = VideoDownloadStatus.Completed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send video for {url}", item.Url);
            logger.LogTrace("Send failure — JobId: {jobId}, ChatId: {chatId}, BotMessage: {msgId}, Filename: {filename}", job.Id, job.ChatId, job.BotMessage, item.Filename);
            job.Status = VideoDownloadStatus.Failed;

            await EditStatusMessage(job, $"❌ Помилка надсилання відео\n{job.VideoUrl}", false, cancellationToken);
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

        string statusText = $"Відео від {job.RequestedBy}."
            + (string.IsNullOrWhiteSpace(job.UserComment) ? string.Empty : $"\n{job.UserComment}")
            + $"\n⚠️ Відео завелике для Telegram ({sizeMb} MB)."
            + (fallbackUrl != null ? $" {fallbackUrl}" : string.Empty);

        await EditStatusMessage(job, statusText, true, cancellationToken);

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

    private static string GetFallbackUrl(string videoUrl)
    {
        if (videoUrl.Contains("instagram.com"))
            return videoUrl.Replace("instagram.com", "kksave.com");
        if (videoUrl.Contains("x.com"))
            return videoUrl.Replace("x.com", "fixupx.com");
        if (videoUrl.Contains("twitter.com"))
            return videoUrl.Replace("twitter.com", "fxtwitter.com");
        return videoUrl;
    }

    private async Task HandleFailedDownload(VideoDownload job, MeTubeHistoryItem item, CancellationToken cancellationToken)
    {
        logger.LogWarning("MeTube reported download error for {url}: {msg}", item.Url, item.Title);
        logger.LogTrace("Download failure — JobId: {jobId}, ChatId: {chatId}, BotMessage: {msgId}", job.Id, job.ChatId, job.BotMessage);

        await EditStatusMessage(job, $"❌ Помилка завантаження відео\n{job.VideoUrl}", false, cancellationToken);

        try
        {
            await meTubeClient.DeleteDownload(item.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not delete errored download {id}", item.Id);
        }

        db.VideoDownloads.Remove(job);
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

    private async Task EditStatusMessage(VideoDownload job, string text, bool showPreview, CancellationToken cancellationToken)
    {
        if (job.BotMessage <= 0)
            return;

        try
        {
            await telegramBotClient.SendRequest(new EditMessageTextRequest
            {
                ChatId = new ChatId(job.ChatId),
                MessageId = job.BotMessage,
                Text = text, LinkPreviewOptions = new LinkPreviewOptions()
                {
                    IsDisabled = !showPreview,
                    PreferLargeMedia = true
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not edit status message {messageId}", job.BotMessage);
        }
    }
}
