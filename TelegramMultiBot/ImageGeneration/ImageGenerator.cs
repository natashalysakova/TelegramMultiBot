
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
        private readonly IConfiguration _configuration;

        public ImageGenerator(IEnumerable<IDiffusor> diffusors, IConfiguration configuration)
        {
            var directory = configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().BaseOutputDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _diffusors = diffusors;
            _configuration = configuration;
        }

        public async Task<JobInfo> Run(JobInfo job)
        {
            var hosts = _configuration.GetSection(HostSettings.Name).Get<IEnumerable<HostSettings>>().OrderBy(x=>x.Priority);

            foreach (var host in hosts)
            {
                var diffusor = _diffusors.Single(x => x.UI == host.UI && job.Diffusor is null ? true : x.UI == job.Diffusor);

                if (await diffusor.isAvailable(host) && diffusor.CanHnadle(job.Type))
                {
                    return await diffusor.Run(job);
                }
            }


            //foreach (var item in _diffusors)
            //{
            //    if (await item.isAvailable() && item.CanHnadle(job.Type))
            //    {
            //        return await item.Run(job);
            //    }
            //}

            throw new SdNotAvailableException("В бобра втомились лапки, він не може зараз малювати бо спить - спробуй пізніше");
        }
    }
}
