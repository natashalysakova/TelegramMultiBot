using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.ImageGenerators
{
    internal interface IDiffusor
    {
        string UI { get; }
        HostSettings ActiveHost { get; }

        bool CanHandle(JobType type);

        bool IsAvailable();

        //Task<bool> isAvailable(HostSettings host);

        Task<JobInfo> Run(JobInfo job);
    }

    public abstract class Diffusor : IDiffusor
    {
        private readonly ILogger<Diffusor> _logger;
        private readonly IConfiguration _configuration;

        public Diffusor(ILogger<Diffusor> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public abstract string UI { get; }
        protected abstract string pingPath { get; }

        public abstract bool CanHandle(JobType type);

        public abstract Task<JobInfo> Run(JobInfo job);

        private HostSettings? _hostSettings;
        public HostSettings ActiveHost
        {
            get
            {
                if (_hostSettings == null)
                    _hostSettings = GetAvailable().Result;

                return _hostSettings;
            }
        }

        public abstract bool ValidateConnection(string content);

        private async Task<bool> isAvailable(HostSettings host)
        {
            if (!host.Enabled)
            {
                _logger.LogTrace("{uri} disabled", host.Uri);
                return false;
            }

            var httpClient = new HttpClient
            {
                BaseAddress = host.Uri,
                Timeout = TimeSpan.FromSeconds(10)
            };
            try
            {
                var resp = await httpClient.GetAsync(pingPath);

                if (resp.IsSuccessStatusCode)
                {
                    if (ValidateConnection(await resp.Content.ReadAsStringAsync()))
                    {
                        _logger.LogTrace($"{host.Uri} available");
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

        private async Task<HostSettings?> GetAvailable()
        {
            var hostSettings = _configuration.GetSection(HostSettings.Name).Get<IEnumerable<HostSettings>>();
            if (hostSettings == null)
                throw new InvalidOperationException($"Cannot get {HostSettings.Name} section");

            var hosts = hostSettings.Where(x => x.UI == UI);

            foreach (var host in hosts)
            {
                if (await isAvailable(host))
                {
                    return host;
                }
            }

            return default;
        }

        public bool IsAvailable()
        {
            return ActiveHost is not null;
        }
    }
}