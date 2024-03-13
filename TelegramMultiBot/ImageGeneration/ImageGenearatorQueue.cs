using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGeneration.Exceptions;
using TelegramMultiBot.ImageGenerators.Automatic1111;

namespace TelegramMultiBot.ImageGenerators
{
    internal class ImageGenearatorQueue
    {
        //Queue<IJob> _jobs = new Queue<IJob>();
        private readonly ILogger<ImageGenearatorQueue> _logger;

        private readonly ImageGenerator _imageGenerator;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public ImageGenearatorQueue(ILogger<ImageGenearatorQueue> logger, ImageGenerator imageGenerator, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _imageGenerator = imageGenerator;
            _configuration = configuration;
            _serviceProvider = serviceProvider;

            using var scope = _serviceProvider.CreateScope();
            var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
            if (databaseService.RunningJobs > 0)
            {
                databaseService.CancelUnfinishedJobs();
            }
        }

        public void Run(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    //var activeJobsNumber = _configuration.GetSection("ImageGeneation:BotSettings").Get<BotSettings>().ActiveJobs;
                    //if (_databaseService.RunningJobs < activeJobsNumber)
                    //{
                    JobInfo? job = default;
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        try
                        {
                            var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                            databaseService.TryDequeue(out job);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Cannot dequeue: {error}", ex.Message);
                            await Task.Delay(30000);
                        }
                    }

                    if (job != default)
                    {
                        //var task =  Task.Run(async () =>
                        //{
                        _logger.LogDebug("Starting {id}", job.Id);

                        try
                        {
                            var result = await _imageGenerator.Run(job);
                            JobFinished?.Invoke(result);
                            _logger.LogDebug("Finished {id}", job.Id);
                        }
                        catch (SdNotAvailableException)
                        {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                                databaseService.ReturnToQueue(job);
                            }
                            _logger.LogDebug("SD not available, job returned to the queue {id}", job.Id);
                            await Task.Delay(10000);
                        }
                        catch (Exception ex)
                        {
                            //refresh job info
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();
                                job = databaseService.GetJob(job.Id);
                            }

                            JobFailed?.Invoke(job, ex);
                            _logger.LogDebug("Failed {id}", job.Id);
                        }
                        //});
                    }

                    //}
                    await Task.Delay(1000);
                }
            }, cancellationToken);
        }

        internal void AddJob(IInputData message)
        {
            using var scope = _serviceProvider.CreateScope();
            var databaseService = scope.ServiceProvider.GetRequiredService<IDatabaseService>();

            var config = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>();
            if (config is null)
                throw new NullReferenceException(nameof(config));

            var jobLimit = config.JobLimitPerUser;
            if (databaseService.ActiveJobsCount(message.UserId) >= jobLimit)
            {
                throw new AlreadyRunningException($"Ти вже маєш {jobLimit} активні завдання на рендер. Спробуй пізніше");
            }

            try
            {
                databaseService.Enqueue(message);
            }
            catch
            {
                throw new OldJobException("Job is too old");
            }
        }

        public event Action<JobInfo>? JobFinished;

        public event Action<JobInfo, Exception>? JobFailed;
    }
}