
using Microsoft.Extensions.Hosting;
using TelegramMultiBot.Database.DTO;

namespace VideoDownloader;

public class VideoDownloaderService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(6000000, stoppingToken);
    }
}