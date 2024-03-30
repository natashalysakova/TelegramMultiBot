using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.ImageGeneration.Exceptions;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    internal class ImageGenerator
    {
        private readonly IEnumerable<IDiffusor> _diffusors;
        private readonly IServiceProvider _serviceProvider;

        public ImageGenerator(IConfiguration configuration, IServiceProvider serviceProvider)
        {
            var directory = (configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>() ?? throw new InvalidOperationException("Cannot get ImageGeneationSettings")).BaseOutputDirectory;
            if (!Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }
            _serviceProvider = serviceProvider;
        }

        public async Task<JobInfo> Run(JobInfo job)
        {
            var diffusors = _serviceProvider.GetRequiredService<IEnumerable<IDiffusor>>();
            var diffusor = diffusors.Where(x => x.IsAvailable() && x.CanHandle(job.Type) && (job.Diffusor is null || x.UI == job.Diffusor)).OrderBy(x => x.ActiveHost!.Priority).FirstOrDefault();

            if (diffusor != null)
            {
                return await diffusor.Run(job);
            }

            throw new SdNotAvailableException("В бобра втомились лапки, він не може зараз малювати бо спить - він намалює, як тільки прокинеться");
        }
    }
}