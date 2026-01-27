
using Microsoft.Extensions.Hosting;
using TelegramMultiBot.Database.DTO;

namespace VideoDownloader;

public class VideoDownloaderService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}