
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Timers;
using Telegram.Bot.Types;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.ImageGeneration.Exceptions;
using TelegramMultiBot.ImageGenerators.Automatic1111;

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
                var databaseService = scope.ServiceProvider.GetService<ImageDatabaseService>();
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

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var databaseService = scope.ServiceProvider.GetService<ImageDatabaseService>();

                        if (databaseService.TryDequeue(out var job))
                        {

                            //var task =  Task.Run(async () =>
                            //{
                            _logger.LogDebug("Starting " + job.Id);

                            try
                            {
                                var result = await _imageGenerator.Run(job);
                                databaseService.JobFinished(job);
                                JobFinished?.Invoke(job);
                                _logger.LogDebug("Finished " + job.Id);
                            }
                            catch (Exception ex)
                            {
                                databaseService.JobFailed(job);

                                JobFailed?.Invoke(job, ex);
                                _logger.LogDebug("Failed " + job.Id);
                            }
                            //});
                        }
                    }
                    //}
                    await Task.Delay(1000);


                }

            }, cancellationToken);
        }


        internal void AddJob(Message message)
        {
            AddJob(message, -1);
        }
        internal void AddJob(Message message, int botMessageId)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var databaseService = scope.ServiceProvider.GetService<ImageDatabaseService>();
                CheckLimits(message, databaseService);
                databaseService.Enqueue(message, botMessageId);
            }

        }

        internal void AddJob(CallbackQuery callbackQuery)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var databaseService = scope.ServiceProvider.GetService<ImageDatabaseService>();

                CheckLimits(callbackQuery.Message, databaseService);
                databaseService.Enqueue(callbackQuery);
            }
        }

        //private void CheckLimits(Message message, ImageDatabaseService databaseService)
        //{
        //    var jobLimit = _configuration.GetSection("ImageGeneation:BotSettings").Get<BotSettings>().JobLimitPerUser;
        //    if (databaseService.ActiveJobs.Where(x => x.UserId == message.From.Id).Count() >= jobLimit)
        //    {
        //        throw new AlreadyRunningException($"Ти вже маєш {jobLimit} активні завдання на рендер. Спробуй пізніше");
        //    }

        //}

        private void CheckLimits(Message message, ImageDatabaseService databaseService)
        {
            var jobLimit = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().JobLimitPerUser;
            if (databaseService.ActiveJobs.Where(x => x.UserId == message.From.Id).Count() >= jobLimit)
            {
                throw new AlreadyRunningException($"Ти вже маєш {jobLimit} активні завдання на рендер. Спробуй пізніше");
            }

        }

        //public void Dispose()
        //{
        //    _timer.Stop();
        //    _timer.Elapsed -= RunCleanup;
        //    _timer.Dispose();
        //}

        public event Action<ImageJob> JobFinished;
        public event Action<ImageJob, Exception> JobFailed;
    }
}
