using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.ImageGenerators
{
    internal interface IDiffusor
    {
        string UI { get; }
        HostSettings? ActiveHost { get; }

        bool CanHandle(JobType type);

        bool IsAvailable();

        //Task<bool> isAvailable(HostSettings host);

        Task<JobInfo> Run(JobInfo job);
    }

    public abstract class Diffusor(ILogger<Diffusor> logger, IConfiguration configuration) : IDiffusor
    {
        public abstract string UI { get; }
        protected abstract string PingPath { get; }

        public abstract bool CanHandle(JobType type);

        public abstract Task<JobInfo> Run(JobInfo job);

        private HostSettings? _hostSettings;

        public HostSettings? ActiveHost
        {
            get
            {
                _hostSettings ??= GetAvailable().Result;
                return _hostSettings;
            }
        }

        public abstract bool ValidateConnection(string content);

        private async Task<bool> IsAvailable(HostSettings host)
        {
            if (!host.Enabled)
            {
                logger.LogTrace("{uri} disabled", host.Uri);
                return false;
            }

            var httpClient = new HttpClient
            {
                BaseAddress = host.Uri,
                Timeout = TimeSpan.FromSeconds(10)
            };
            try
            {
                var resp = await httpClient.GetAsync(PingPath);

                if (resp.IsSuccessStatusCode)
                {
                    if (ValidateConnection(await resp.Content.ReadAsStringAsync()))
                    {
                        logger.LogTrace("{uri} available", host.Uri);
                        return true;
                    }
                    else
                    {
                        logger.LogDebug("{uri} host not valid", host.Uri);
                    }
                }
                else
                {
                    logger.LogDebug("{uri} request is not successful", host.Uri);
                }
            }
            catch (Exception)
            {
                logger.LogTrace("{uri} not available", host.Uri);
            }
            return false;
        }

        private bool CheckIfBusy(HostSettings host)
        {
            try
            {
                HttpClient client = new();
                var responce = client.GetAsync($"http://{host.Host}:5001/gpu").Result;
                if (responce.IsSuccessStatusCode)
                {
                    var value = responce.Content.ReadAsStringAsync().Result;
                    var sum = float.Parse(value, CultureInfo.InvariantCulture);
                    logger.LogTrace("Host GPU utilisation {host} - {sum}%", host.Host, sum);
                    return sum > 15;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception ex)
            {
                logger.LogWarning("Error getting host performance {host}: {error}", host.Host, ex.Message);
                return false;
            }
        }

        private async Task<HostSettings?> GetAvailable()
        {
            var hostSettings = configuration.GetSection(HostSettings.Name).Get<IEnumerable<HostSettings>>() ?? throw new InvalidOperationException($"Cannot get {HostSettings.Name} section");
            var hosts = hostSettings.Where(x => x.UI == UI);

            foreach (var host in hosts)
            {
                if (await IsAvailable(host))
                {
                    return host;
                }
            }

            return default;
        }

        public bool IsAvailable()
        {
            return ActiveHost is not null && !CheckIfBusy(ActiveHost);
        }
    }
}