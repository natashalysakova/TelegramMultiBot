using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.ImageGenerators
{
    interface IDiffusor
    {
        string UI { get; }
        HostSettings ActiveHost { get; set; }

        bool CanHnadle(JobType type);
        Task<bool> isAvailable();
        Task<bool> isAvailable(HostSettings host);
        Task<JobInfo> Run(JobInfo job);
    }

    abstract public class Diffusor : IDiffusor
    {
        private readonly ILogger<Diffusor> _logger;
        private readonly IConfiguration _configuration;

        protected Diffusor(ILogger<Diffusor> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public abstract string UI { get; }
        protected abstract string pingPath { get; }

        public abstract bool CanHnadle(JobType type);
        public abstract Task<JobInfo> Run(JobInfo job);

        public HostSettings ActiveHost { get; set; }
        public abstract bool ValidateConnection(string content);
        public async Task<bool> isAvailable(HostSettings host)
        {
            if (!host.Enabled)
            {
                _logger.LogTrace($"{host.Uri} disabled");
                return false;
            }

            var httpClient = new HttpClient();
            httpClient.BaseAddress = host.Uri;
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            try
            {
                var resp = await httpClient.GetAsync(pingPath);

                if (resp.IsSuccessStatusCode)
                {
                    if (ValidateConnection(await resp.Content.ReadAsStringAsync()))
                    {
                        ActiveHost = host;
                        return true;
                    }
                    else
                    {
                        _logger.LogDebug($"{host.Uri} host not valid");
                    }
                }
                else
                {
                    _logger.LogDebug($"{host.Uri} request is not successful");
                }
                
            }
            catch (Exception)
            {
                _logger.LogTrace($"{host.Uri} not available");
            }
            return false;

        }
        public async Task<bool> isAvailable()
        {
            var hosts = _configuration.GetSection(HostSettings.Name).Get<IEnumerable<HostSettings>>().Where(x => x.UI == this.UI);

            foreach (var host in hosts)
            {
                if (await isAvailable(host))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
