using Microsoft.Extensions.Logging;
using System.Globalization;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.ImageGenerators
{
    public abstract class Diffusor(ILogger<Diffusor> logger, ISqlConfiguationService configuration) : IDiffusor
    {
        public abstract string UI { get; }
        protected abstract string PingPath { get; }

        public bool CanHandle(JobInfo job)
        {
            return TypeSupported(job.Type) && CorrectDiffusor(job.Diffusor) && SupportedModel(job.Text);
        }

        protected abstract bool TypeSupported(JobType jobType);
        protected bool CorrectDiffusor(string? diffusor)
        {
            return diffusor is null || UI == diffusor;
        }
        protected abstract bool SupportedModel(string? text);

        public abstract Task<JobInfo> Run(JobInfo job);
        private HostInfo? _hostSettings;

        public HostInfo? ActiveHost
        {
            get
            {
                _hostSettings ??= GetAvailable().Result;
                return _hostSettings;
            }
        }

        public abstract bool ValidateConnection(string content);

        private async Task<bool> IsAvailable(HostInfo host)
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
                        logger.LogTrace("{uri} online", host.Uri);
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
                logger.LogTrace("{uri} offline", host.Uri);
            }
            return false;
        }

        private bool CheckIfBusy(HostInfo host)
        {
            //temp till service is back 

            var maxGPUUtil = configuration.IGSettings.MaxGpuUtil;

            try
            {
                HttpClient client = new();
                //client.Timeout = TimeSpan.FromSeconds(10);
                var responce = client.GetAsync($"http://{host.Address}:5001/gpu").Result;
                if (responce.IsSuccessStatusCode)
                {
                    var value = responce.Content.ReadAsStringAsync().Result;
                    var sum = float.Parse(value, CultureInfo.InvariantCulture);
                    logger.LogTrace("Host GPU utilisation {host} - {sum}%", host.Address, sum);
                    return sum > maxGPUUtil;
                }
                else
                {
                    return true;
                }

            }
            catch (Exception ex)
            {
                logger.LogWarning("Error getting host performance {host}: {error}", host.Address, string.Join("\n", ex.GetInnerExceptions().Select(x=>x.Message)));
                return true;
            }
        }

        private async Task<HostInfo?> GetAvailable()
        {
            var hosts = configuration.Hosts.Where(x => x.UI == UI);

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