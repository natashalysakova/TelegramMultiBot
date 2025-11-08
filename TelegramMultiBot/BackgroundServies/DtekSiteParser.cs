using DtekParsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.BackgroundServies;

public class DtekSiteParser : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DtekSiteParser> _logger;

    public DtekSiteParser(IServiceProvider serviceProvider, ILogger<DtekSiteParser> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

#if DEBUG
    const int STANDART_DELAY = 30; // 30 seconds
#else
    const int STANDART_DELAY = 300; // 5 minutes
#endif

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int delay = STANDART_DELAY;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scope = _serviceProvider.CreateScope();
                var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

                _logger.LogTrace("checking all locations at {date}", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss.fff"));

                var locations = await dbservice.GetLocations();

                foreach (var location in locations)
                {
                    _logger.LogTrace("Parsing site for location {region}", location.Region);
                    try
                    {
                        await ParseSite(dbservice, location);

                        delay = STANDART_DELAY; // reset delay on success
                    }
                    catch (ParseException ex)
                    {
                        _logger.LogError(ex, "Error occurred while parsing site {url}: {message}", location.Region, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                var newDelay = Math.Min(delay * 2, 6000); // exponential backoff up to 100 minutes
                _logger.LogError(ex, "Error occurred while execution. Delay {delay} seconds: {message}", newDelay, ex.Message);
                delay = newDelay;
            }
            finally
            {
                _logger.LogTrace("checking all locations ended");
            }

            try
            {
                _logger.LogTrace("Starting cleanup process");
                await DoCleanup();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during cleanup: {message}", ex.Message);
            }
            finally
            {
                _logger.LogTrace("Cleanup ended");
            }

            _logger.LogTrace("Waiting for {delay} seconds before next check", delay);
            await Task.Delay(TimeSpan.FromSeconds(delay));
        }
    }

    private async Task DoCleanup()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();
        var cutoffDate = DateTime.Now.AddDays(-7);

        await dbservice.DeleteOldHistory(cutoffDate);

        var filesInDb = await dbservice.GetAllHistoryImagePaths();

        _logger.LogInformation("found {count} files in db", filesInDb.Count());

        if(!Directory.Exists(baseDirectory))
        {
            _logger.LogWarning("Base directory {dir} does not exist. Skipping cleanup.", baseDirectory);
            return;
        }

        var files = Directory.GetFiles(baseDirectory, "*.png", SearchOption.AllDirectories);
        var ophanedFiles = files.Except(filesInDb);

        if (ophanedFiles.Any())
        {
            _logger.LogInformation("ophanedFiles:" + string.Join('\n', ophanedFiles));

            foreach (var file in ophanedFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    _logger.LogInformation("Deleted file: {file}", file);
                }
            }
        }

        var missingFiles = filesInDb.Except(files);
        if (missingFiles.Any())
        {
            _logger.LogWarning("Missing files in storage: {file}", string.Join("\n", missingFiles));
            await dbservice.DeleteHistoryWithMissingFiles(missingFiles);
        }
    }


    private async Task ParseSite(IMonitorDataService dbservice, ElectricityLocation location)
    {
        var schedule = await new ScheduleParser().Parse(location.Url);
        //locationSchedules.Add(location.Id, schedule);

        location.LastChecked = DateTime.Now;

        var scheduleUpdateDate = schedule.Updated;

        if (location.LastUpdated == scheduleUpdateDate)
        {
            _logger.LogTrace("location {location} was not updated. Last update at {date}", location.Region, location.LastUpdated);
            return;
        }

        _logger.LogInformation("Location {location} was updated", location.Region);

        var images = await ScheduleImageGenerator.GenerateAllImages(schedule);

        _logger.LogInformation("Generated {count} images", images.Count());

        foreach (var image in images)
        {
            var filename = SaveFile(location.Region, scheduleUpdateDate, image);

            var group = schedule.Groups.SingleOrDefault(x => x.Id == image.Group);
            ElectricityGroup? dbGroup = null;

            if (group != null)
            {
                dbGroup = await dbservice.GetGroupByCodeAndLocationRegion(location.Region, group.Id);
                if (dbGroup == null)
                {
                    dbGroup = new ElectricityGroup()
                    {
                        LocationRegion = location.Region,
                        GroupCode = group.Id,
                        GroupName = group.GroupName,
                        DataSnapshot = group.DataSnapshot,
                    };
                    await dbservice.Add(dbGroup);
                }
                else
                {
                    dbGroup.DataSnapshot = group.DataSnapshot;
                    await dbservice.Update(dbGroup);
                }
            }

            await dbservice.Add(new ElectricityHistory()
            {
                Updated = schedule.RealSchedule.Max(x => x.Updated),
                ImagePath = filename,
                GroupId = dbGroup?.Id,
                LocationId = location.Id,
                ScheduleDay = image.Date.HasValue ? image.Date.Value : 0,
                JobType = GetJobType(image)
            });

        }
    }

    private ElectricityJobType GetJobType(ImageGenerationModel image)
    {
        if (image.IsPlanned)
        {
            return ElectricityJobType.SingleGroupPlan;
        }
        else if (image.Group != null)
        {
            return ElectricityJobType.SingleGroup;
        }
        else
        {
            return ElectricityJobType.AllGroups;
        }
    }

    const string baseDirectory = "monitor";
    private string SaveFile(string region, DateTime updated, ImageGenerationModel image)
    {
        var folder = Path.Combine(baseDirectory, region);
        string subfolder = GeSubFolder(image);
        folder = Path.Combine(folder, subfolder);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        var extention = "png";
        var prefix = image.IsPlanned ? "p_" : "";
        var filename = $"{prefix}{updated.Ticks}.{extention}";
        var filePath = Path.Combine(folder, filename);
        File.WriteAllBytes(filePath, image.ImageData);
        _logger.LogTrace("Saving image {date} {group} {file}", image.Date, image.Group, filePath);
        return filePath;
    }

    static string ConvertUrlToValidFilename(string url)
    {
        //Convert the URL to a file-safe format
        string filename = url
            .Replace("https://", "") // Remove protocol part
            .Replace("http://", "")  // Remove protocol part
            .Replace("/", "_")       // Replace slashes with underscores
            .Replace(":", "_")       // Replace colons with underscores
            .Replace("?", "_")       // Replace question marks with underscores
            .Replace("&", "_")       // Replace ampersands with underscores
            .Replace("=", "_")       // Replace equal signs with underscores
            .Replace(" ", "_");      // Replace spaces with underscores

        filename = RemoveInvalidFilenameChars(filename);
        filename = filename.Length > 128 ? filename.Substring(0, 128) : filename;

        return filename;
    }

    static string RemoveInvalidFilenameChars(string filename)
    {
        string invalidCharsPattern = new string(Path.GetInvalidFileNameChars());
        return Regex.Replace(filename, $"[{Regex.Escape(invalidCharsPattern)}]", "_");
    }

    private static string GeSubFolder(ImageGenerationModel image)
    {
        if (!string.IsNullOrWhiteSpace(image.Group))
        {
            return image.Group;
        }
        return string.Empty;
    }
}
