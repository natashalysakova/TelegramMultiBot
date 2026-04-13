using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database;
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

        try
        {
            var history = await meTubeClient.GetHistory();
            var toDelete = history.Done.Where(x => x.Timestamp < timestamp.ToUnixTimeSeconds());
            if (toDelete.Any())
            {
                logger.LogInformation("Cleaning up {count} old downloads from MeTube history", toDelete.Count());
                await meTubeClient.DeleteDownloads(toDelete.Select(x => x.Url));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve MeTube history during cleanup");
        }

        try
        {
            var oldEntries = await db.VideoDownloads
                .Where(x => x.CreatedAt < timestamp)
                .ToListAsync(cancellationToken);
            if (oldEntries.Any())
            {
                db.RemoveRange(oldEntries);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clean up old video download entries from the database");
        }
    }
}
