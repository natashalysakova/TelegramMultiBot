using Microsoft.Extensions.DependencyInjection;
using VideoDownloader.Client;

namespace VideoDownloader;

public static class InstallExtensions
{
    public static void InstallVideoDownloader(this IServiceCollection services)
    {
        services.AddSingleton<MeTubeClient>();
        services.AddHostedService<VideoDownloaderService>();
    }
}
