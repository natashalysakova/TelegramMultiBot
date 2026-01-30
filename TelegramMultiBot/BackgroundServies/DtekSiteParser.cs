using AngleSharp.Dom;
using DtekParsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;
using TelegramMultiBot.Database.Services;

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
    private ISqlConfiguationService _configuationService;
    private CancellationTokenSource _delayCancellationTokenSource;

    const string baseDirectory = "monitor";
    private static volatile int delay = 300;

    public DtekSiteParser(IServiceProvider serviceProvider, ILogger<DtekSiteParser> logger, ISvitlobotClient svitlobotClient)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _svitlobotClient = svitlobotClient;
        _delayCancellationTokenSource = new CancellationTokenSource();

        if (!Directory.Exists(baseDirectory))
        {
            Directory.CreateDirectory(baseDirectory);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ParseSites();

            await SendUpdatesToSvitlobot();

            await DoCleanup();

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

    private async Task ParseSites()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            using var loggerScope = _logger.BeginScope("ParseSites");
            _configuationService = scope.ServiceProvider.GetRequiredService<ISqlConfiguationService>();
            delay = _configuationService.SvitlobotSettings.DtekParserDelay;

            var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();
            var locations = await dbservice.GetLocations();
            foreach (var location in locations)
            {
                using var locationScope = _logger.BeginScope(location.Region);

                var unresolvedAlerts = await dbservice
                    .GetNotResolvedAlertsByLocation(
                        location.Id, 
                        DateTimeOffset.Now.AddMinutes(-_configuationService.SvitlobotSettings.AlertIgnoreMinutes));
                if (unresolvedAlerts != null)
                {
                    _logger.LogWarning("Location has unresolved alerts, skipping parsing");
                    continue;
                }

                _logger.LogDebug("Parsing site");
                var retryCount = 0;
                bool success = false;
                do
                {
                    try
                    {
                        await ParseSite(dbservice, location);

                        await ParseAddreses(dbservice, location);

                        success = true;
                        delay = _configuationService.SvitlobotSettings.DtekParserDelay; // reset delay on success

                        await ResetAlertAfterSuccess(dbservice, location);
                    }
                    catch (IncapsulaException ex) // incapsula blocking
                    {
                        _logger.LogError(ex, "Incapsula blocking detected. Updating alert");
                        await CreateOrUpdateAlertForIncapsulaBlocking(dbservice, location);
                        retryCount = 2; // stop retrying
                    }
                    catch (ParseException ex) // parsing error - maybe page wasn't loaded correctly
                    {
                        retryCount++;
                        _logger.LogError(ex, "Error occurred while parsing: {message}", ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(20 * retryCount)); // wait before retry
                    }
                }
                while (retryCount < 2 && !success);
                if (!success)
                {
                    _logger.LogError("Failed to parse site after {retries} retries", retryCount);
                }
                else
                {
                    _logger.LogInformation("Finished parsing site");
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
    }

    private async Task ResetAlertAfterSuccess(IMonitorDataService dbservice, ElectricityLocation location)
    {
        var existingAlerts = await dbservice.GetNotResolvedAlertsByLocation(location.Id);

        foreach (var existingAlert in existingAlerts)
        {
            existingAlert.ResolvedAt = DateTimeOffset.Now;
            await dbservice.Update(existingAlert);
            _logger.LogDebug("Resolve alert after success: {region}", location.Region);
        }
    }

    private async Task ParseAddreses(IMonitorDataService dbservice, ElectricityLocation location)
    {
        var addressesToCheck = await dbservice.GetActiveAddresesJobs(location.Id);

        var infoParser = new AddressParser(_configuationService);

        foreach (var address in addressesToCheck)
        {
            _logger.LogInformation("Parsing address job {id}", address.Id);
            try
            {
                var buildingInfos = await infoParser.ParseAddress(address, DateTimeOffset.Now);

                var fetchedInfo = JsonSerializer.Serialize(buildingInfos);

                if (address.LastFetchedInfo != fetchedInfo && buildingInfos.Type != "")
                {
                    address.LastFetchedInfo = JsonSerializer.Serialize(buildingInfos);
                    address.ShouldBeSent = true;
                    await dbservice.Update(address);
                    _logger.LogInformation("Address job {id} info updated", address.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to parse address job {id}: {ex}", address.Id, ex.Message);
                continue;
            }
        }
        _logger.LogInformation("Finished parsing address jobs for location {region}", location.Region);
    }

    private async Task CreateOrUpdateAlertForIncapsulaBlocking(IMonitorDataService dataService, ElectricityLocation location)
    {
        var existingAlerts = await dataService.GetNotResolvedAlertsByLocation(location.Id);

        if (!existingAlerts.Any())
        {
            Alert alert = new Alert()
            {
                LocationId = location.Id,
                CreatedAt = DateTimeOffset.Now,
                AlertMessage = "Incapsula blocking detected. Please update the cookie using /cookie command."
            };
            await dataService.Add(alert);
        }
        else // actually should be only one
        {
            foreach (var existingAlert in existingAlerts)
            {
                existingAlert.FailureCount += 1;
                await dataService.Update(existingAlert);
            }
        }
    }

    public async Task SendUpdatesToSvitlobot()
    {
        using var loggerscope = _logger.BeginScope("SendUpdatesToSvitlobot");
        try
        {
            _logger.LogTrace("Starting sending updates to svitlobot");
            await SendUpdatesToSvitlobotInternal();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during sending updates to svitlobot: {message}", ex.Message);
        }
        finally
        {
            _logger.LogTrace("Sending updates to svitlobot ended");
        }

        async Task SendUpdatesToSvitlobotInternal()
        {
            var scope = _serviceProvider.CreateScope();
            var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

            var svitlobots = await dbservice.GetAllSvitlobots();
            foreach (var svitlobot in svitlobots)
            {
                string data = string.Empty;

                if (svitlobot.LastSentData == svitlobot.Group.DataSnapshot)
                {
                    _logger.LogTrace("Svitlobot key {key} data snapshot not changed, skipping", svitlobot.SvitlobotKey);
                    continue;
                }

                data = await _svitlobotClient.GetTimetable(svitlobot.SvitlobotKey);

                if (data == $@"Channel ""{svitlobot.SvitlobotKey}"" is not found! Insert correct key from chat bot!")
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

    public async Task DoCleanup()
    {
        using var loggerscope = _logger.BeginScope("DoCleanup");
        try
        {
            _logger.LogTrace("Starting cleanup process");
            await DoCleanupInternal();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error during cleanup: {message}", ex.Message);
        }
        finally
        {
            _logger.LogTrace("Cleanup ended");
        }

        async Task DoCleanupInternal()
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
            var removedAlerts = await dbservice.DeleteOldResolvedAlerts(cutoffDate);
            if(removedAlerts.Any())
            {
                _logger.LogInformation("Deleted {count} old resolved alerts", removedAlerts.Count());
            }
        }

    }

    private async Task ParseSite(IMonitorDataService dbservice, ElectricityLocation location)
    {
        var schedule = await new ScheduleParser(_configuationService).Parse(location.Url);
        //locationSchedules.Add(location.Id, schedule);

        location.LastChecked = DateTime.Now;

        var scheduleUpdateDate = schedule.Updated;

        if (location.LastUpdated >= scheduleUpdateDate)
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
        var filesInDb = await dbservice.GetLatestUpdateFiles(location.Id);
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

public class AddressParser(ISqlConfiguationService configuationService)
{

    private string? GetCookie(string url)
    {
        if (configuationService == null)
            return null;

        var location = LocationNameUtility.GetRegionByUrl(url);
        switch (location)
        {
            case "kem": return configuationService.SvitlobotSettings.KemCookie;
            case "krem": return configuationService.SvitlobotSettings.KremCookie;

            default:
                return null;
        }

    }

    public async Task<BuildingInfo> ParseAddress(AddressJob addressJob, DateTimeOffset date)
    {
        var responseContent = string.Empty;
        var requestContent = string.Empty;
        try
        {
            var url = addressJob.Location.Url.Replace("shutdowns", "ajax");
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var dtekCookie = GetCookie(url);
            if (!string.IsNullOrEmpty(dtekCookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", dtekCookie);
            }

            // Validate and trim city and street before adding to collection
            var validatedCity = ValidateAndTrimCyrillicText(addressJob.City, "City");
            var validatedStreet = ValidateAndTrimCyrillicText(addressJob.Street, "Street");

            var collection = new List<KeyValuePair<string, string>>
            {
                new("method", "getHomeNum"),
                new("data[0][name]", "city"),
                new("data[0][value]", validatedCity),
                new("data[1][name]", "street"),
                new("data[1][value]", validatedStreet),
                new("data[2][name]", "updateFact"),
                new("data[2][value]", date.ToString("dd.MM.yyyy HH:mm"))
            };
            var content = new FormUrlEncodedContent(collection);
            request.Content = content;
            requestContent = await content.ReadAsStringAsync();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            responseContent = await response.Content.ReadAsStringAsync();
            var addressResponse = JsonSerializer.Deserialize<AddressResponse>(responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Check if response is successful and has data
            if (addressResponse?.Result == true && addressResponse.Data != null)
            {
                var validatedBuilding = ValidateAndTrimCyrillicText(addressJob.Building, "Building");
                // Find the specific building in the data dictionary
                if (addressResponse.Data.TryGetValue(validatedBuilding, out var buildingInfo))
                {
                    return buildingInfo;
                }
                else
                {
                    // Building not found - you might want to log this
                    throw new ParseException($"Building '{addressJob.Building}' not found in response");
                }
            }

            throw new ParseException("Invalid response from server");
        }
        catch (Exception ex)
        {
            throw new ParseException($"Failed to fetch HTML: {ex.Message}. Request: {requestContent} Response content: {responseContent}");
        }
    }

    private static string ValidateAndTrimCyrillicText(string text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException($"{fieldName} cannot be null or empty", nameof(text));
        }

        var trimmedText = text.Trim();

        // Try to replace latin characters with cyrillic equivalents
        var convertedText = ConvertLatinToCyrillic(trimmedText);

        // Check if text contains only cyrillic letters, numbers, spaces, dots, hyphens, and common punctuation
        // This regex allows: cyrillic letters, numbers, spaces, dots, hyphens, apostrophes, and parentheses
        var cyrillicPattern = @"^[А-Яа-яІіЇїЄєʼ0-9\s\.\-\(\)№\/]+$";

        if (!Regex.IsMatch(convertedText, cyrillicPattern))
        {
            throw new ArgumentException($"{fieldName} contains invalid characters. Only cyrillic letters, numbers, spaces, dots, hyphens and basic punctuation are allowed. Original: '{trimmedText}', Converted: '{convertedText}'", nameof(text));
        }

        return convertedText;
    }

    private static string ConvertLatinToCyrillic(string text)
    {
        // Dictionary of latin to cyrillic character mappings for visually similar characters
        var latinToCyrillic = new Dictionary<char, char>
        {
            // Lowercase mappings
            { 'a', 'а' }, { 'e', 'е' }, { 'o', 'о' }, { 'p', 'р' }, { 'c', 'с' },
            { 'x', 'х' }, { 'y', 'у' }, { 'k', 'к' }, { 'h', 'н' }, { 'm', 'м' },
            { 'i', 'і' }, { 'b', 'б' }, { 't', 'т' }, { 'v', 'в' },
            
            // Uppercase mappings
            { 'A', 'А' }, { 'E', 'Е' }, { 'O', 'О' }, { 'P', 'Р' }, { 'C', 'С' },
            { 'X', 'Х' }, { 'Y', 'У' }, { 'K', 'К' }, { 'H', 'Н' }, { 'M', 'М' },
            { 'I', 'І' }, { 'B', 'В' }, { 'T', 'Т' }, { 'V', 'В' }
        };

        var result = new StringBuilder(text.Length);

        foreach (char c in text)
        {
            if (latinToCyrillic.TryGetValue(c, out char cyrillicChar))
            {
                result.Append(cyrillicChar);
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}
public class AddressResponse
{
    public bool Result { get; set; }
    public Dictionary<string, BuildingInfo>? Data { get; set; }
    // Add other properties if needed (showCurOutageParam, fact, preset, etc.)
}

public class BuildingInfo
{
    [JsonPropertyName("sub_type")]
    public string SubType { get; set; } = string.Empty;

    [JsonPropertyName("start_date")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("end_date")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("sub_type_reason")]
    public List<string> SubTypeReason { get; set; } = new();

    [JsonPropertyName("voluntarily")]
    public object? Voluntarily { get; set; }
}


