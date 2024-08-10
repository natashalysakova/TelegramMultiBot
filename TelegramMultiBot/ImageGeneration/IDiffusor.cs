using System.Diagnostics;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.ImageGenerators
{
    internal interface IDiffusor
    {
        string UI { get; }
        HostInfo? ActiveHost { get; }

        bool CanHandle(JobInfo job);

        bool IsAvailable();

        //Task<bool> isAvailable(HostSettings host);

        Task<JobInfo> Run(JobInfo job);
    }
}