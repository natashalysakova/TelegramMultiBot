using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.ImageGenerators
{
    internal class CleanupService(IImageDatabaseService databaseService, TelegramClientWrapper client, ISqlConfiguationService configuration, IBotMessageDatabaseService botMessageService, IAssistantDataService assistantDataService, ILogger<CleanupService> logger)
    {
        internal async Task Run()
        {
            logger.LogDebug("Cleanup Started");

            var settings = configuration.IGSettings;
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
            var botMessagesCleaned = botMessageService.RunCleanup();
            logger.LogDebug("BotMessages cleaned: {0}", botMessagesCleaned);

            if (Directory.Exists("monitor"))
            {

                var monitorDirectories = Directory.GetDirectories("monitor", "*", SearchOption.TopDirectoryOnly);

                foreach (var directory in monitorDirectories)
                {

                    var files = Directory.EnumerateFiles(directory);
                    if (files.Count() <= 1)
                    {
                        continue;
                    }

                    var dateToCheck = DateTime.Now.AddMinutes(-10);

                    foreach (var item in files.Order().SkipLast(2))
                    {
                        FileInfo file = new FileInfo(item);
                        if (file.CreationTime < dateToCheck)
                        {
                            logger.LogTrace("removing " + item);
                            File.Delete(item);
                        }
                    }
                }
            }


            var cleanedhistory = assistantDataService.Cleanup();
            logger.LogDebug("ChatHistory cleaned: {0}", cleanedhistory);

            logger.LogDebug("Cleanup Ended");
        }
    }
}