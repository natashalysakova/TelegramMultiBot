using TelegramMultiBot.Database.DTO;

namespace TelegramMultiBot.ImageGenerators
{
    interface IDiffusor
    {
        public string UI { get; }

        bool CanHnadle(JobType type);
        bool isAvailable();
        Task<JobInfo> Run(JobInfo job);
    }
}
