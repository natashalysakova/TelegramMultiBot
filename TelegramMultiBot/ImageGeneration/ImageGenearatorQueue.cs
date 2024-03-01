
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Timers;
using Telegram.Bot.Types;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.ImageGeneration.Exceptions;
using TelegramMultiBot.ImageGenerators.Automatic1111;
using static System.Formats.Asn1.AsnWriter;

namespace TelegramMultiBot.ImageGenerators
{
    class ImageGenearatorQueue
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

            using (var scope = _serviceProvider.CreateScope())
            {
                var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
                if (databaseService.RunningJobs > 0)
                {
                    databaseService.CancelUnfinishedJobs();
                }
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
                            var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
                            databaseService.TryDequeue(out job);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Cannot dequeue: " + ex.Message);
                            await Task.Delay(30000);
                        }
                    }

                    if (job != default)
                    {

                        //var task =  Task.Run(async () =>
                        //{
                        _logger.LogDebug("Starting " + job.Id);

                        try
                        {
                            var result = await _imageGenerator.Run(job);

                            //databaseService.JobFinished(job);
                            JobFinished?.Invoke(result);
                            _logger.LogDebug("Finished " + job.Id);
                        }
                        catch (SdNotAvailableException)
                        {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
                                databaseService.ReturnToQueue(job);
                            }

                            _logger.LogDebug("SD not available, job returned to the queue " + job.Id);

                        }
                        catch (Exception ex)
                        {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
                                job = databaseService.GetJob(job.Id);
                            }

                            JobFailed?.Invoke(job, ex);
                            _logger.LogDebug("Failed " + job.Id);

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
            using (var scope = _serviceProvider.CreateScope())
            {
                var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();

                var jobLimit = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().JobLimitPerUser;
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
        }
        //internal void AddJob(Message message, int botMessageId)
        //{
        //    using (var scope = _serviceProvider.CreateScope())
        //    {
        //        var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();
        //        CheckLimits(message, databaseService);
        //        databaseService.Enqueue(message, botMessageId);
        //    }

        //}

        //internal void AddJob(CallbackQuery callbackQuery)
        //{
        //    using (var scope = _serviceProvider.CreateScope())
        //    {
        //        var databaseService = scope.ServiceProvider.GetService<IDatabaseService>();

        //        CheckLimits(callbackQuery.Message, databaseService);
        //        databaseService.Enqueue(callbackQuery);
        //    }
        //}

        //private void CheckLimits(Message message, ImageDatabaseService databaseService)
        //{
        //    var jobLimit = _configuration.GetSection("ImageGeneation:BotSettings").Get<BotSettings>().JobLimitPerUser;
        //    if (databaseService.ActiveJobs.Where(x => x.UserId == message.From.Id).Count() >= jobLimit)
        //    {
        //        throw new AlreadyRunningException($"Ти вже маєш {jobLimit} активні завдання на рендер. Спробуй пізніше");
        //    }

        //}

        //private void CheckLimits(Message message, IDatabaseService databaseService)
        //{

        //    var jobLimit = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().JobLimitPerUser;
        //    if (databaseService.ActiveJobsCount(message.From.) >= jobLimit)
        //    {
        //        throw new AlreadyRunningException($"Ти вже маєш {jobLimit} активні завдання на рендер. Спробуй пізніше");
        //    }
        //}

        //public void Dispose()
        //{
        //    _timer.Stop();
        //    _timer.Elapsed -= RunCleanup;
        //    _timer.Dispose();
        //}

        public event Action<JobInfo> JobFinished;
        public event Action<JobInfo, Exception> JobFailed;
    }
}
