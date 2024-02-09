
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.ImageGenerators.Automatic1111;

namespace TelegramMultiBot.ImageGenerators
{
    class ImageGenearatorQueue
    {
        Queue<IJob> _jobs = new Queue<IJob>();
        List<IJob> _activeJobs = new List<IJob>();
        private readonly ILogger<ImageGenearatorQueue> _logger;
        private readonly ImageGenerator _imageGenerator;
        private readonly IConfiguration _configuration;
        object locker = new object();
        public ImageGenearatorQueue(ILogger<ImageGenearatorQueue> logger, ImageGenerator imageGenerator, IConfiguration configuration)
        {
            _logger = logger;
            _imageGenerator = imageGenerator;
            _configuration = configuration;
        }

        public void Run(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var activeJobsNumber = _configuration.GetSection("ImageGeneation:BotSettings").Get<BotSettings>().ActiveJobs;

                    if (_activeJobs.Count() < activeJobsNumber)
                    {

                        if (_jobs.TryDequeue(out var job))
                        {
                            lock (locker)
                            {
                                _activeJobs.Add(job);
                            }
                            var task = Task.Run(async () =>
                            {
                                _logger.LogDebug("Starting " + job.Id);

                                try
                                {
                                    await _imageGenerator.Run(job);
                                    JobFinished?.Invoke(job);
                                    _logger.LogDebug("Finished " + job.Id);
                                }
                                catch (Exception ex)
                                {
                                    JobFailed?.Invoke(job, ex);
                                    _logger.LogDebug("Failed " + job.Id);

                                }
                                lock (locker)
                                {
                                    _activeJobs.Remove(job);
                                }
                            });

                        }

                        Task.Delay(500);
                    }

                }

            }, cancellationToken);
        }

        internal void AddJob(IJob job)
        {
            var jobLimit = _configuration.GetSection("ImageGeneation:BotSettings").Get<BotSettings>().JobLimitPerUser;
            if (_jobs.Union(_activeJobs).Where(x => x.UserId == job.UserId).Count() >= jobLimit)
            {
                throw new InvalidOperationException($"Ти вже маєш {jobLimit} активні завдання на рендер. Спробуй пізніше");
            }
            _jobs.Enqueue(job);
        }

        public event Action<IJob> JobFinished;
        public event Action<IJob, Exception> JobFailed;
    }
}
