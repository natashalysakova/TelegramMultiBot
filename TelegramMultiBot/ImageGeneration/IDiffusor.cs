using TelegramMultiBot.Database.DTO;

namespace TelegramMultiBot.ImageGeneration;

internal interface IDiffusor
{
    string UI { get; }
    HostInfo? ActiveHost { get; }

    bool CanHandle(JobInfo job);

    bool IsAvailable();

    //Task<bool> isAvailable(HostSettings host);

    Task<JobInfo> Run(JobInfo job);
}