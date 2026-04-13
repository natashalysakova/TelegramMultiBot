using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using TelegramMultiBot.Database;
using VideoDownloader.Client;

namespace VideoDownloader;

public class VideoDownloaderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MeTubeClient _meTubeClient;
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ILogger<VideoDownloaderService> _logger;

    public VideoDownloaderService(
        IServiceScopeFactory scopeFactory,
        MeTubeClient meTubeClient,
        TelegramBotClient telegramBotClient,
        ILogger<VideoDownloaderService> logger)
    {
        _scopeFactory = scopeFactory;
        _meTubeClient = meTubeClient;
        _telegramBotClient = telegramBotClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDownloads(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in video download processing loop");
            }

            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider
                .GetRequiredService<TelegramMultiBot.Database.Interfaces.ISqlConfiguationService>()
                .VideoDownloaderSettings;
            var interval = settings.PollingIntervalSeconds > 0 ? settings.PollingIntervalSeconds : 15;

            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }

    private async Task ProcessDownloads(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BoberDbContext>();

        var pendingJobs = await db.VideoDownloads
            .Where(x => x.Status == "pending")
            .ToListAsync(cancellationToken);

        if (!pendingJobs.Any())
            return;

        MeTubeHistoryResponse? history;
        try
        {
            history = await _meTubeClient.GetHistory();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve MeTube history");
            return;
        }

        if (history == null)
            return;

        foreach (var item in history.Done)
        {
            var job = pendingJobs.FirstOrDefault(x => x.VideoUrl == item.Url || item.Id == GetId(x.VideoUrl));
            if (job == null)
                continue;

            if (item.Status == "finished" && item.Filename != null)
                await HandleFinishedDownload(job, item, db, cancellationToken);
            else if (item.Status == "error")
                await HandleFailedDownload(job, item, db, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private string GetId(string videoUrl)
    {
        if (videoUrl.Contains("youtube.com/watch"))
        {
            var index = videoUrl.LastIndexOf('=') + 1;
            return videoUrl.Substring(index, videoUrl.Length - index);
        }

        if (videoUrl.Contains("youtube.com/shorts"))
        {
            var index = videoUrl.LastIndexOf('/') + 1;
            return videoUrl.Substring(index, videoUrl.Length - index);
        }

        return string.Empty;
    }

    private const long MaxFileSizeBytes = 50L * 1024 * 1024;

    private async Task HandleFinishedDownload(VideoDownload job, MeTubeHistoryItem item, BoberDbContext db, CancellationToken cancellationToken)
    {
        if (item.Size.HasValue && item.Size.Value > MaxFileSizeBytes)
        {
            _logger.LogWarning("Video too large ({size} bytes) for Telegram, falling back to URL replacement for {url}", item.Size.Value, item.Url);
            await HandleOversizedDownload(job, item, db, cancellationToken);
            return;
        }

        try
        {
            using var response = await _meTubeClient.GetFileResponseAsync(item.Filename!);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var chatId = new ChatId(job.ChatId);
            var inputFile = InputFile.FromStream(stream, item.Filename);

            await _telegramBotClient.SendRequest(new SendVideoRequest
            {
                ChatId = chatId,
                Video = inputFile,
                MessageThreadId = job.MessageThreadId == 0 ? null : job.MessageThreadId,
                Caption = $"Відео від {job.RequestedBy}."
                    + (string.IsNullOrWhiteSpace(job.UserComment) ? string.Empty : $"\n{job.UserComment}")
                    + $"\nОригінальне відео: {job.VideoUrl}",
                ShowCaptionAboveMedia = true,
                SupportsStreaming = true
            }, cancellationToken);

            await DeleteStatusMessage(job, cancellationToken);
            await _meTubeClient.DeleteDownload(item.Id);
            db.VideoDownloads.Remove(job);

            _logger.LogInformation("Video sent for {url}", item.Url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send video for {url}", item.Url);
            job.Status = "error";

            await EditStatusMessage(job, "❌ Помилка надсилання відео", cancellationToken);
        }
    }

    private async Task HandleOversizedDownload(VideoDownload job, MeTubeHistoryItem item, BoberDbContext db, CancellationToken cancellationToken)
    {
        var fallbackUrl = GetFallbackUrl(job.VideoUrl);
        var sizeMb = item.Size!.Value / (1024 * 1024);

        string statusText = $"Відео від {job.RequestedBy}."
            + (string.IsNullOrWhiteSpace(job.UserComment) ? string.Empty : $"\n{job.UserComment}")
            + $"\n⚠️ Відео завелике для Telegram ({sizeMb} MB)."
            + (fallbackUrl != null ? $" {fallbackUrl}" : string.Empty);

        await EditStatusMessage(job, statusText, cancellationToken);

        try
        {
            await _meTubeClient.DeleteDownload(item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete oversized download {id}", item.Id);
        }

        db.VideoDownloads.Remove(job);
    }

    private static string? GetFallbackUrl(string videoUrl)
    {
        if (videoUrl.Contains("instagram.com"))
            return videoUrl.Replace("instagram", "kksave");
        if (videoUrl.Contains("x.com"))
            return videoUrl.Replace("x.com", "fixupx.com");
        if (videoUrl.Contains("twitter.com"))
            return videoUrl.Replace("twitter.com", "fxtwitter.com");
        return videoUrl;
    }

    private async Task HandleFailedDownload(VideoDownload job, MeTubeHistoryItem item, BoberDbContext db, CancellationToken cancellationToken)
    {
        _logger.LogWarning("MeTube reported download error for {url}: {msg}", item.Url, item.Title);

        await EditStatusMessage(job, "❌ Помилка завантаження відео", cancellationToken);

        try
        {
            await _meTubeClient.DeleteDownload(item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete errored download {id}", item.Id);
        }

        db.VideoDownloads.Remove(job);
    }

    private async Task DeleteStatusMessage(VideoDownload job, CancellationToken cancellationToken)
    {
        if (job.BotMessage <= 0)
            return;

        try
        {
            await _telegramBotClient.SendRequest(new DeleteMessageRequest
            {
                ChatId = new ChatId(job.ChatId),
                MessageId = job.BotMessage
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete status message {messageId}", job.BotMessage);
        }
    }

    private async Task EditStatusMessage(VideoDownload job, string text, CancellationToken cancellationToken)
    {
        if (job.BotMessage <= 0)
            return;

        try
        {
            await _telegramBotClient.SendRequest(new EditMessageTextRequest
            {
                ChatId = new ChatId(job.ChatId),
                MessageId = job.BotMessage,
                Text = text
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not edit status message {messageId}", job.BotMessage);
        }
    }
}
