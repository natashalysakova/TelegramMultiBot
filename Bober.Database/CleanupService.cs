using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Bober.Database.Configuration;
using Microsoft.Extensions.Configuration;
using Bober.Library.Interfaces;

namespace Bober.Database.Services
{
    public class CleanupService
    {
        private readonly IDatabaseService _databaseService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CleanupService> _logger;

        public CleanupService(IDatabaseService databaseService, IConfiguration configuration, ILogger<CleanupService> logger)
        {
            _databaseService = databaseService;
            _configuration = configuration;
            _logger = logger;
        }

        internal IEnumerable<CleanupResult> Run()
        {
            _logger.LogDebug("Cleanup Started");

            var settings = _configuration.GetSection(DBSettings.Name).Get<DBSettings>();
            var jobAge = settings.Cleanup.JobAge;
            var removeFiles = settings.Cleanup.RemoveFiles;

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
                }
            }

            _databaseService.RemoveJobs(jobsToDelete.Select(x=>x.Id));
            _logger.LogDebug("Cleanup Ended");

            return jobsToDelete.Select(x => new CleanupResult(x.ChatId, x.MessageId));
        }
    }

    record CleanupResult(long chatId, int messageId);
}