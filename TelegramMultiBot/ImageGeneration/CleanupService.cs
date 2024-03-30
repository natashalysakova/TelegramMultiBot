using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.ImageGenerators
{
    internal class CleanupService(IImageDatabaseService databaseService, TelegramClientWrapper client, IConfiguration configuration, ILogger<CleanupService> logger)
    {
        internal async Task Run()
        {
            logger.LogDebug("Cleanup Started");

            var settings = configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>() ?? throw new InvalidOperationException($"Cannot bind {ImageGeneationSettings.Name}");
            var jobAge = settings.JobAge;
            var removeFiles = settings.RemoveFiles;

            var date = DateTime.Now.Subtract(TimeSpan.FromSeconds(jobAge));
            var jobsToDelete = databaseService.GetJobsOlderThan(date);
            logger.LogDebug("Jobs to cleanup: {count}", jobsToDelete.Count());
            if (removeFiles)
            {
                foreach (var item in jobsToDelete)
                {
                    foreach (var res in item.Results)
                    {
                        try
                        {
                            logger.LogDebug("Removing file: {path}", res.FilePath);
                            File.Delete(res.FilePath);

                            var directory = Path.GetDirectoryName(res.FilePath);

                            if (string.IsNullOrEmpty(directory))
                            {
                                throw new InvalidOperationException($"No path info found {res.FilePath}");
                            }

                            if (Directory.GetFiles(directory).Length == 0)
                            {
                                Directory.Delete(directory);
                            }
                        }
                        catch (Exception)
                        {
                            logger.LogError("Failed to remove: {path}", res.FilePath);
                        }
                    }

                    try
                    {
                        await client.EditMessageReplyMarkupAsync(item.ChatId, item.MessageId, null);
                        logger.LogDebug("Message edited {chatId} {messageId}", item.ChatId, item.MessageId);
                    }
                    catch
                    {
                    }
                }
            }

            databaseService.RemoveJobs(jobsToDelete.Select(x => x.Id));
            logger.LogDebug("Cleanup Ended");
        }
    }
}