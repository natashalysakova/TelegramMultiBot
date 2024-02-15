using Bober.Database.Services;
using Bober.Worker.ImageGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Bober.Worker.Configuration;
using Bober.Worker.ImageGeneration;
using Bober.Library.Contract;
using Bober.Library.Exceptions;

namespace Bober.Worker
{
    public class ImageRenderQuery : BackgroundService
    {
        private readonly ILogger<ImageRenderQuery> _logger;
        private readonly ImageGenerator _imageGenerator;
        private readonly IConfiguration _configuration;
        private readonly ImageDatabaseService _databaseService;

        public ImageRenderQuery(ILogger<ImageRenderQuery> logger, IConfiguration configuration, ImageGenerator imageGenerator, ImageDatabaseService databaseService)
        {
            _logger = logger;
            _imageGenerator = imageGenerator;
            _configuration = configuration;
            _databaseService = databaseService;

            if (databaseService.RunningJobs > 0)
            {
                databaseService.CancelUnfinishedJobs();
            }

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }


                if (_databaseService.TryDequeue(out var job))
                {

                    //var task =  Task.Run(async () =>
                    //{
                    _logger.LogDebug("Starting " + job.Id);

                    try
                    {
                        var result = await _imageGenerator.Run(job);
                        _databaseService.JobFinished(job);
                        //JobFinished?.Invoke(job);
                        _logger.LogDebug("Finished " + job.Id);
                    }
                    catch (Exception ex)
                    {
                        _databaseService.JobFailed(job);

                        //JobFailed?.Invoke(job, ex);
                        _logger.LogDebug("Failed " + job.Id);
                    }
                    //});
                }



                await Task.Delay(1000, stoppingToken);
            }
        }

        internal void AddJob(IInputData message)
        {
            var jobLimit = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().JobLimitPerUser;
            if (_databaseService.ActiveJobs.Where(x => x.UserId == message.UserId).Count() >= jobLimit)
            {
                throw new AlreadyRunningException($"Ти вже маєш {jobLimit} активні завдання на рендер. Спробуй пізніше");
            }

            _databaseService.Enqueue(message);
        }
    }
}
