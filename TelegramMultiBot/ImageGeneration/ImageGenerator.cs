
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    class ImageGenerator
    {
        private readonly ILogger<ImageGenerator> _logger;
        private readonly TelegramBotClient _client;
        private readonly IEnumerable<IDiffusor> _diffusors;
        private readonly IConfiguration _configuration;
        string directory;

        public ImageGenerator(ILogger<ImageGenerator> logger, IConfiguration configuration, TelegramBotClient client, IEnumerable<IDiffusor> diffusors)
        {

            directory = Path.Combine(Directory.GetCurrentDirectory(), "tmp");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _logger = logger;
            _configuration = configuration;
            _client = client;
            _diffusors = diffusors;
        }

        public async Task Run(IJob job)
        {

            foreach (var item in _diffusors)
            {
                if (item.isAvailable())
                {
                    await item.Run(job, directory);
                    return;
                }
            }

            throw new SystemException("В бобра втомились лапки, він не може зараз малювати бо спить - спробуй пізніше");
        }
    }
}
