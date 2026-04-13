using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VideoDownloader;

public class VideoDownloaderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VideoDownloaderService> _logger;

    public VideoDownloaderService(
        IServiceScopeFactory scopeFactory,
        ILogger<VideoDownloaderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var processScope = _scopeFactory.CreateScope();
                var handler = processScope.ServiceProvider.GetRequiredService<IVideoProcessHandler>();
                await handler.ProcessDownloads(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in video download processing loop");
            }

            try
            {
                using var cleanupScope = _scopeFactory.CreateScope();
                var cleanupHandler = cleanupScope.ServiceProvider.GetRequiredService<IVideoCleanupHandler>();
                await cleanupHandler.CleanupOld(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in video cleanup processing loop");
            }

            using var waitScope = _scopeFactory.CreateScope();
            var settings = waitScope.ServiceProvider
                .GetRequiredService<TelegramMultiBot.Database.Interfaces.ISqlConfiguationService>()
                .VideoDownloaderSettings;
            var interval = settings.PollingIntervalSeconds > 0 ? settings.PollingIntervalSeconds : 15;

            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }

}
