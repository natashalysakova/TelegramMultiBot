
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.ImageGeneration.Exceptions;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    class ImageGenerator
    {
        private readonly IEnumerable<IDiffusor> _diffusors;

        public ImageGenerator(IEnumerable<IDiffusor> diffusors, IConfiguration configuration)
        {
            var directory = configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().BaseOutputDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            _diffusors = diffusors;
        }

        public async Task<JobInfo> Run(JobInfo job)
        {

            foreach (var item in _diffusors)
            {
                if (item.isAvailable() && item.CanHnadle(job.Type))
                {
                    return await item.Run(job);
                }
            }

            throw new SdNotAvailableException("В бобра втомились лапки, він не може зараз малювати бо спить - спробуй пізніше");
        }
    }
}
