
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Bober.Worker.Configuration;
using Bober.Worker.Interfaces;
using Bober.Library.Exceptions;
using Bober.Library.Contract;


namespace Bober.Worker.ImageGeneration
{
    public class ImageGenerator 
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

        public async Task Run(JobInfo job)
        {

            foreach (var item in _diffusors)
            {
                if (item.isAvailable())
                {
                    await item.Run(job);
                }
            }

            throw new SdNotAvailableException("В бобра втомились лапки, він не може зараз малювати бо спить - спробуй пізніше");
        }
    }
}
