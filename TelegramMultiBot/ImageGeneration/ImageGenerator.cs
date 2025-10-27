using Microsoft.Extensions.DependencyInjection;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGeneration.Exceptions;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    internal class ImageGenerator
    {
        private readonly IEnumerable<IDiffusor> _diffusors;
        private readonly IServiceProvider _serviceProvider;

        public ImageGenerator(ISqlConfiguationService configuration, IServiceProvider serviceProvider)
        {
            var directory = configuration.IGSettings.BaseImageDirectory;
            if (!Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }
            _serviceProvider = serviceProvider;
        }

        public async Task<JobInfo> Run(JobInfo job)
        {
            var diffusors = _serviceProvider.GetRequiredService<IEnumerable<IDiffusor>>();
            var diffusor = diffusors.Where(x => 
            x.IsAvailable() 
            && x.CanHandle(job)).OrderBy(x => x.ActiveHost!.Priority).FirstOrDefault();

            if (diffusor != null)
            {
                return await diffusor.Run(job);
            }

            throw new SdNotAvailableException("В бобра втомились лапки, він не може зараз малювати бо спить - він намалює, як тільки прокинеться");
        }
    }
}