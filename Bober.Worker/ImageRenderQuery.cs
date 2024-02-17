using Bober.Database.Services;
using Bober.Worker.ImageGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Bober.Worker.Configuration;
using Bober.Worker.ImageGeneration;
using Bober.Library.Exceptions;
using Bober.Library.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bober.Worker
{
    public class ImageRenderQuery : BackgroundService
    {
        private readonly ILogger<ImageRenderQuery> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public ImageRenderQuery(ILogger<ImageRenderQuery> logger, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            using (var scope = _serviceProvider.CreateScope())
            {

                var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
                if (databaseService.RunningJobs > 0)
                {
                    databaseService.CancelUnfinishedJobs();
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_logger.IsEnabled(LogLevel.Information))
                    {
                        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                    }


                    if (databaseService.TryDequeue(out var job))
                    {

                        //var task =  Task.Run(async () =>
                        //{
                        _logger.LogDebug("Starting " + job.Id);
                        var webhookService = scope.ServiceProvider.GetService<WebhookService>();

                        try
                        {
                            var imageGenerator = scope.ServiceProvider.GetService<ImageGenerator>();
                            await imageGenerator.Run(job);
                            await webhookService.PublishMessage(WebhookService.successTopic, job);
                            _logger.LogDebug("Finished " + job.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug("Failed " + job.Id);
                            databaseService.PostProgress(job.Id, -1, "Error. " + ex.Message);
                            job.Exception = ex;
                            await webhookService.PublishMessage(WebhookService.failureTopic, job);
                        }
                        //});
                    }
                    else
                    {
                        _logger.LogDebug("Waiting for jobs");
                    }



                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        internal void AddJob(IInputData message)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();

                var jobLimit = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().JobLimitPerUser;
                if (databaseService.ActiveJobsCount(message.UserId) >= jobLimit)
                {
                    throw new AlreadyRunningException($"Ти вже маєш {jobLimit} активні завдання на рендер. Спробуй пізніше");
                }

                databaseService.Enqueue(message);

            }

        }
    }
}
