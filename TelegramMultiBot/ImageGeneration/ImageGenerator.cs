
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using TelegramMultiBot.ImageGeneration.Exceptions;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    class ImageGenerator
    {
        private readonly IEnumerable<IDiffusor> _diffusors;
        string directory;

        public ImageGenerator(IEnumerable<IDiffusor> diffusors)
        {

            directory = Path.Combine(Directory.GetCurrentDirectory(), "images", DateTime.Today.ToString("yyyyMMdd"));
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            _diffusors = diffusors;
        }

        public async Task<ImageJob?> Run(ImageJob job)
        {

            foreach (var item in _diffusors)
            {
                if (item.isAvailable())
                {
                    return await item.Run(job, directory);
                }
            }

            throw new SdNotAvailableException("В бобра втомились лапки, він не може зараз малювати бо спить - спробуй пізніше");
        }
    }
}
