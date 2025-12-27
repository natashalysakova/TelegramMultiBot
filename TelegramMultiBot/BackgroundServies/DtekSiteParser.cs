using DtekParsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.BackgroundServies;

public interface IDtekSiteParserService
{
    public Task ParseImmediately();
}

public class DtekSiteParserService : IDtekSiteParserService
{   
    private readonly DtekSiteParser _dtekSiteParser;

    public DtekSiteParserService(DtekSiteParser dtekSiteParser)
    {
        _dtekSiteParser = dtekSiteParser;
    }

    public async Task ParseImmediately()
    {
        _dtekSiteParser.CancelDelay();
        await Task.CompletedTask;
    }
}

public class DtekSiteParser : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DtekSiteParser> _logger;
    private readonly ISvitlobotClient _svitlobotClient;
    private CancellationTokenSource _delayCancellationTokenSource;

    const string baseDirectory = "monitor";

    public DtekSiteParser(IServiceProvider serviceProvider, ILogger<DtekSiteParser> logger, ISvitlobotClient svitlobotClient)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _svitlobotClient = svitlobotClient;
        _delayCancellationTokenSource = new CancellationTokenSource();

        if(!Directory.Exists(baseDirectory))
        {
            Directory.CreateDirectory(baseDirectory);
        }
    }

#if DEBUG
    const int STANDART_DELAY = 60; // 30 seconds
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
                var locations = await dbservice.GetLocations();

                foreach (var location in locations)
                {
                    using var locationScope = _logger.BeginScope("Location: {region}", location.Region);
                    _logger.LogDebug("Parsing site");
                    var retryCount = 0;
                    bool success = false;
                    do
                    {
                        try
                        {
                            await ParseSite(dbservice, location);
                            success = true;
                            delay = STANDART_DELAY; // reset delay on success
                        }
                        catch (ParseException ex) // parsing error - maybe page wasn't loaded correctly
                        {
                            retryCount++;
                            _logger.LogError(ex, "Error occurred while parsing site {url}: {message}", location.Region, ex.Message);
                            await Task.Delay(TimeSpan.FromSeconds(10 * retryCount)); // wait before retry
                        }
                    }
                    while (retryCount < 3 && !success);
                    _logger.LogDebug("Finished parsing site");
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
                _logger.LogTrace("Starting sending updates to svitlobot");
                await SendUpdatesToSvitlobot();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error during sending updates to svitlobot: {message}", ex.Message);
            }
            finally
            {
                _logger.LogTrace("Sending updates to svitlobot ended");
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
            try
            {
                _logger.LogDebug("Waiting for {delay} seconds before next check", delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), _delayCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Delay cancelled, running the parser again immediately");
            }
            finally
            {
                _delayCancellationTokenSource = new CancellationTokenSource();
            }
        }
    }

    public async Task SendUpdatesToSvitlobot()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        var svitlobots = await dbservice.GetAllSvitlobots();
        foreach (var svitlobot in svitlobots)
        {
            string data = string.Empty;
            try
            {
                if(svitlobot.LastSentData == svitlobot.Group.DataSnapshot)
                {
                    _logger.LogTrace("Svitlobot key {key} data snapshot not changed, skipping", svitlobot.SvitlobotKey);
                    continue;
                }

                data = await _svitlobotClient.GetTimetable(svitlobot.SvitlobotKey);

                if(data == $@"Channel ""{svitlobot.SvitlobotKey}"" is not found! Insert correct key from chat bot!")
                {
                    await dbservice.RemoveSvitlobotKey(svitlobot.SvitlobotKey, svitlobot.GroupId);
                    _logger.LogWarning("Svitlobot key {key} not found, removed from db", svitlobot.SvitlobotKey);
                    continue;
                } 

                var schedule = data?.Split(";&&&;", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();

                var newSchedule = ConvertDataSnapshotToNewSchedule(schedule, svitlobot.Group.DataSnapshot);
                if (newSchedule is null)
                {
                    _logger.LogWarning("Svitlobot key {key} conversion returned null new schedule", svitlobot.SvitlobotKey);
                    continue;
                }

                var updateResponse = await _svitlobotClient.UpdateTimetable(svitlobot.SvitlobotKey, newSchedule);
                if (!updateResponse)
                {
                    _logger.LogWarning("Svitlobot key {key} update returned failure", svitlobot.SvitlobotKey);
                }

                svitlobot.LastSentData = svitlobot.Group.DataSnapshot;
                await dbservice.Update(svitlobot);
            }
            catch (Exception)
            {
                _logger.LogError("Error during updating svitlobot key {key} with data {data}", svitlobot.SvitlobotKey, data);
                throw;
            }
        }
    }

    public string ConvertDataSnapshotToNewSchedule(string? schedule, string? dataSnapshot)
    {
        // 1764885600|1:0|2:0|3:0|4:0|5:0|6:0|7:0|8:0|9:1|10:1|11:0|12:0|13:0|14:0|15:0|16:0|17:0|18:1|19:1|20:1|21:3|22:0|23:0|24:0;
        // |
        // V
        // 031200000000000000000000%3B000000000000100100000000%3B000000000000000030000000%3B000000000000000000000000%3B010000010000000100000000%3B000000000000000000000000%3B00000000000000000000000

        var splitted = dataSnapshot?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new Dictionary<int, string>();

        if (splitted != null && splitted.Any())
        {
            foreach (string day in splitted)
            {
                var row = day.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var date = row[0];

                StringBuilder stringBuilder = new StringBuilder();
                for (int i = 1; i < row.Length; i++)
                {
                    var splittedInfo = row[i].Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (splittedInfo.Length != 2)
                    {
                        break;
                    }

                    var valueToInsert = ParseFromSnapshot(splittedInfo[1]);

                    stringBuilder.Append(valueToInsert);
                }
                var readyDay = stringBuilder.ToString();

                var dayofWeek = (int)DateTimeOffset.FromUnixTimeSeconds(long.Parse(date)).ToLocalTime().DayOfWeek;

                var cultureInfo = Thread.CurrentThread.CurrentCulture;
                if (cultureInfo.DateTimeFormat.FirstDayOfWeek == DayOfWeek.Sunday) // adjust if week starts from Sunday
                {
                    dayofWeek = dayofWeek == 0 ? 6 : dayofWeek - 1;
                }

                result[dayofWeek] = readyDay;
            }
        }

        int[][]? scheduleArray = null;
        if (schedule == "no data")
        {
            _logger.LogWarning("Schedule is 'no data'");
        }
        else if (schedule != null)
        {
            try
            {
                scheduleArray = JsonSerializer.Deserialize<int[][]>(schedule);
            }
            catch (Exception)
            {
                _logger.LogWarning("Failed to deserialize schedule: {schedule}", schedule);
            }
        }

        for (int i = 0; i < 7; i++)
        {
            if (!result.ContainsKey(i) && scheduleArray != null && i < scheduleArray.Length)
            {
                var dayData = scheduleArray[i];
                StringBuilder stringBuilder = new StringBuilder();
                foreach (var hour in dayData)
                {
                    stringBuilder.Append(hour.ToString());
                }
                result[i] = stringBuilder.ToString();
            }
            else if (!result.ContainsKey(i))
            {
                // fill with zeros
                result[i] = new string('0', 24);
            }
        }

        var finalResult = string.Join("%3B", result.OrderBy(x => x.Key).Select(x => x.Value));

        return finalResult;
    }

    private string ParseFromSnapshot(string value)
    {
       /// See LightStatus enum in DtekParsers project
       /// We ignore 'maybe' and 'mfirst' and 'msecond' statuses for snapshot conversion

        return value switch
        {
            "0" => "0",
            "1" => "1",
            "3" => "2",
            "4" => "3",
            _ => "1",
        };
    }
    
    private async Task DoCleanup()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();
        var cutoffDate = DateTime.Today.AddDays(-7);

        var removedHistory = await dbservice.DeleteOldHistory(cutoffDate);

        var files = Directory.GetFiles(baseDirectory, "*.png", SearchOption.AllDirectories);

        foreach (var file in removedHistory)
        {
            var fullPath = files.SingleOrDefault(x => x.Contains(file.ImagePath));

            if (file != null && File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Deleted file: {file}", fullPath);
            }
        }

        //_logger.LogInformation("found {count} files in db", filesInDb.Count());

        //if (!Directory.Exists(baseDirectory))
        //{
        //    _logger.LogWarning("Base directory {dir} does not exist. Skipping cleanup.", baseDirectory);
        //    await dbservice.DeleteAllHistory();
        //    return;
        //}

        //var files = Directory.GetFiles(baseDirectory, "*.png", SearchOption.AllDirectories);
        //var ophanedFiles = files.Except(filesInDb);

        //if (ophanedFiles.Any())
        //{
        //    _logger.LogInformation("ophanedFiles:" + string.Join('\n', ophanedFiles));

        //    foreach (var file in ophanedFiles)
        //    {
        //        if (File.Exists(file))
        //        {
        //            File.Delete(file);
        //            _logger.LogInformation("Deleted file: {file}", file);
        //        }
        //    }
        //}

        
    }

    private async Task ParseSite(IMonitorDataService dbservice, ElectricityLocation location)
    {
        var schedule = await new ScheduleParser().Parse(location.Url);
        //locationSchedules.Add(location.Id, schedule);

        location.LastChecked = DateTime.Now;

        var scheduleUpdateDate = schedule.Updated;

        if (location.LastUpdated == scheduleUpdateDate &&  ! await HasMissingImages(dbservice, location))
        {
            _logger.LogDebug("location was not updated. Last update at {date}", location.LastUpdated);
            return;
        }

        _logger.LogInformation("Location was updated");

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
        
        _logger.LogInformation("Schedule and images updated in database");
    }

    private async Task<bool> HasMissingImages(IMonitorDataService dbservice, ElectricityLocation location)
    {
        var files = Directory.GetFiles(baseDirectory, "*.png", SearchOption.AllDirectories);
        var filesInDb = await dbservice.GetAllHistoryImagePaths(location.Id);
        var missingFiles = filesInDb.Except(files);
        if (missingFiles.Any())
        {
            _logger.LogWarning("Missing files in storage: {file}", string.Join("\n", missingFiles));
            //await dbservice.DeleteHistoryWithMissingFiles(missingFiles);
            return true;
        }

        return false;
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

    internal void CancelDelay()
    {
        _delayCancellationTokenSource.Cancel();
    }
}