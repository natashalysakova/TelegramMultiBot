using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database;
using System.Text.Json;
using VideoDownloader.Client;

namespace VideoDownloader;

public interface IVideoCleanupHandler
{
    Task CleanupOld(CancellationToken cancellationToken);
}

public class VideoCleanupHandler(ILogger<VideoCleanupHandler> logger, MeTubeClient meTubeClient, BoberDbContext db) : IVideoCleanupHandler
{
    public async Task CleanupOld(CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.Now.AddDays(-14);

        var operation = "retrieve history";
        try
        {
            var history = await meTubeClient.GetHistory(cancellationToken);
            var toDelete = history.Done.Where(x => x.Timestamp < timestamp.ToUnixTimeSeconds()).ToArray();
            if (toDelete.Length > 0)
            {
                logger.LogInformation("Cleaning up {count} old downloads from MeTube history", toDelete.Length);
                operation = "delete old downloads";
                var response = await meTubeClient.DeleteDownloads(toDelete.Select(x => x.Url), cancellationToken);
                if (response is null || response.Status != MeTubeStatus.Ok)
                {
                    logger.LogError("MeTube did not confirm cleanup of old downloads: {reason}", response?.ErrorMessage ?? "empty response");
                }
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            logger.LogWarning("Could not {operation}: MeTube is unreachable ({reason}). Check VideoDownloader.MeTubeUrl and network connectivity; cleanup will retry on the next poll.", operation, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "MeTube returned an HTTP error while attempting to {operation}", operation);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Timed out while attempting to {operation} from MeTube; cleanup will retry on the next poll.", operation);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "MeTube returned an invalid response while attempting to {operation}", operation);
        }

        var oldEntries = await db.VideoDownloads
            .Where(x => x.CreatedAt < timestamp)
            .ToListAsync(cancellationToken);
        if (oldEntries.Count > 0)
        {
            db.RemoveRange(oldEntries);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
