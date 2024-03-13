using Microsoft.Extensions.Configuration;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.ImageGeneration.Exceptions;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    internal class ImageGenerator
    {
        private readonly IEnumerable<IDiffusor> _diffusors;

        public ImageGenerator(IEnumerable<IDiffusor> diffusors, IConfiguration configuration)
        {
            var directory = (configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>() ?? throw new InvalidOperationException("Cannot get ImageGeneationSettings")).BaseOutputDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _diffusors = diffusors;
        }

        public async Task<JobInfo> Run(JobInfo job)
        {

            var diffusor = _diffusors.Where(x => x.IsAvailable() && x.CanHandle(job.Type) && (job.Diffusor is null || x.UI == job.Diffusor)).OrderBy(x => x.ActiveHost!.Priority).FirstOrDefault();

            if(diffusor != null)
            {
                return await diffusor.Run(job);
            }

            throw new SdNotAvailableException("В бобра втомились лапки, він не може зараз малювати бо спить - він намалює, як тільки прокинеться");
        }
    }
}