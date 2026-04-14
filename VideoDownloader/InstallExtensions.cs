using Microsoft.Extensions.DependencyInjection;
using VideoDownloader.Client;

namespace VideoDownloader;

public static class InstallExtensions
{
    public static void InstallVideoDownloader(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<MeTubeClient>();

        services.AddSingleton<VideoDownloaderService>();
        services.AddHostedService(provider => provider.GetRequiredService<VideoDownloaderService>());

        services.AddTransient<IVideoProcessHandler, VideoProcessHandler>();
        services.AddTransient<IVideoCleanupHandler, VideoCleanupHandler>();
    }
}
