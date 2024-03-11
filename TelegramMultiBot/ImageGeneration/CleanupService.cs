using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.ImageGeneration;
using Microsoft.Extensions.Configuration;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.ImageGenerators
{
    internal class CleanupService
    {
        private readonly IDatabaseService _databaseService;
        private readonly TelegramClientWrapper _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CleanupService> _logger;

        public CleanupService(IDatabaseService databaseService, TelegramClientWrapper client, IConfiguration configuration, ILogger<CleanupService> logger)
        {
            _databaseService = databaseService;
            _client = client;
            _configuration = configuration;
            _logger = logger;
        }

        internal async Task Run()
        {
            _logger.LogDebug("Cleanup Started");

            var settings = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>();
            var jobAge = settings.JobAge;
            var removeFiles = settings.RemoveFiles;

            var date = DateTime.Now.Subtract(TimeSpan.FromSeconds(jobAge));
            var jobsToDelete = _databaseService.GetJobsOlderThan(date); 
            _logger.LogDebug("Jobs to cleanup:" + jobsToDelete.Count());
            if (removeFiles)
            {
                foreach (var item in jobsToDelete)
                {
                    foreach (var res in item.Results)
                    {
                        try
                        {
                            _logger.LogDebug("Removing file:" + res.FilePath);
                            System.IO.File.Delete(res.FilePath);

                            var directory = Path.GetDirectoryName(res.FilePath);

                            if (!Directory.GetFiles(directory).Any())
                            {
                                Directory.Delete(directory);
                            }
                        }
                        catch (Exception)
                        {
                            _logger.LogError("Failed to remove:" + res.FilePath);
                        }

                    }

                    try
                    {
                        await _client.EditMessageReplyMarkupAsync(item.ChatId, item.MessageId, null);
                        _logger.LogDebug($"Message edited {item.ChatId} {item.MessageId}");
                    }
                    catch
                    {
                    }
                }
            }

            _databaseService.RemoveJobs(jobsToDelete.Select(x=>x.Id));
            _logger.LogDebug("Cleanup Ended");

        }

        internal void CleanupOldJobs(int jobAge, bool removeFiles)
        {
        }

    }
}