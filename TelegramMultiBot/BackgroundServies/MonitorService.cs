using System.Text;
using DtekParsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;
using TelegramMultiBot.ImageCompare;

namespace TelegramMultiBot.BackgroundServies;

public class MonitorService
{
    private readonly ILogger<MonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public event Action<SendInfo> ReadyToSend = delegate { };

    public MonitorService(ILogger<MonitorService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    public void Run(CancellationToken token)
    {
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpdates();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for updates: {message}", ex.Message);
                }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }

        }, token);
    }

    private async Task CheckForUpdates()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        var activeJobs = await dbservice.GetActiveJobs();
        _logger.LogTrace("Found {count} active jobs to check for updates", activeJobs.Count());
        var sendList = new List<SendInfo>();
        foreach (var job in activeJobs)
        {
            try
            {
                bool sendUpdate = DecideIfSendUpdate(job);
                if (!sendUpdate)
                {
                    _logger.LogTrace("Schedule for job {key} was not updated", job.Id);
                    continue;
                }

                job.LastScheduleUpdate = job.Location.LastUpdated;

                if (job.GroupId.HasValue && job.Group is null)
                {
                    _logger.LogInformation("Loading group data for job {id} with GroupId {groupId}", job.Id, job.GroupId.Value);
                    var groupfromDb = await dbservice.GetGroupById(job.GroupId.Value);
                    job.Group = groupfromDb;
                }

                if (job.Group != null)
                {
                    var oldSnapshot = job.LastSentGroupSnapsot;
                    var newSnapshot = job.Group.DataSnapshot;

                    _logger.LogInformation("Before Update - Job {id}: LastScheduleUpdate={lastSchedule}, LastSentGroupSnapsot='{oldSnapshot}' -> '{newSnapshot}'",
                        job.Id, job.LastScheduleUpdate, oldSnapshot, newSnapshot);

                    job.LastSentGroupSnapsot = job.Group.DataSnapshot;

                    _logger.LogInformation("After Assignment - Job {id}: LastSentGroupSnapsot='{snapshot}'",
                        job.Id, job.LastSentGroupSnapsot);
                }

                await dbservice.Update(job);

                if (job.GroupId.HasValue)
                {
                    // Verify what was actually persisted
                    var verifyJob = await dbservice.GetJobById(job.Id);
                    _logger.LogInformation("After DB Update - Job {id}: LastScheduleUpdate={lastSchedule}, LastSentGroupSnapsot='{snapshot}'",
                        verifyJob.Id, verifyJob.LastScheduleUpdate, verifyJob.LastSentGroupSnapsot);
                }

                await SendExisiting(job.Id);

            }
            catch (Exception ex)
            {
                _logger.LogError("{key} job error: {message}", job.Id, ex.Message);
            }
        }
    }

    private bool DecideIfSendUpdate(MonitorJob job)
    {
        // Check if schedule has been updated since last send
        bool hasScheduleUpdate = job.LastScheduleUpdate != job.Location.LastUpdated;

        // For non-group jobs, send if schedule updated
        if (!job.GroupId.HasValue)
        {
            return hasScheduleUpdate;
        }

        // For group jobs with no schedule update, skip
        if (!hasScheduleUpdate)
        {
            return false;
        }

        // For group jobs, also check if group data changed
        return HasGroupDataChanged(job);
    }

    private bool HasGroupDataChanged(MonitorJob job)
    {
        if (job.Group == null)
        {
            // No group associated with the job, nothing to compare. Actually should not happen. Ever.
            _logger.LogInformation("Job {key} has no group associated, treating as changed", job.Id);
            return true;
        }

        // New snapshot available when previously none existed
        if (string.IsNullOrWhiteSpace(job.LastSentGroupSnapsot) && !string.IsNullOrWhiteSpace(job.Group.DataSnapshot))
        {
            _logger.LogInformation("Job {key} group data snapshot created:\n{new}",
                job.Id, job.Group.DataSnapshot);
            return true;
        }

        // Snapshot length different, no need to compare content
        if (job.LastSentGroupSnapsot?.Length != job.Group.DataSnapshot?.Length)
        {
            _logger.LogInformation("Job {key} group data length changed:\nGroup(new)({newLength}): {new}\nJob(old)({oldLength}): {old}",
                job.Id,
                job.Group.DataSnapshot?.Length, job.Group.DataSnapshot,
                job.LastSentGroupSnapsot?.Length, job.LastSentGroupSnapsot);
            return true;
        }

        var length = job.LastSentGroupSnapsot?.Length ?? 0; // Length is same for both schedule snapshot and job snapshot
        // Snapshot content has changed
        for (int i = 0; i < length; i++)
        {
            if (job.LastSentGroupSnapsot![i] != job.Group.DataSnapshot![i])
            {
                _logger.LogInformation("Job {key} group data changed at index {index}\t{old}\t{new}",
                    job.Id, i, job.LastSentGroupSnapsot, job.Group.DataSnapshot);
                return true;
            }
        }
        return false;
    }

    internal async Task<Guid> AddDtekJob(long chatId, int? messageThreadId, string region, string? group)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        var existing = await dataService.GetJobBySubscriptionParameters(chatId, region, group);

        if (existing != null && existing.IsActive)
        {
            return existing.Id;
        }
        else if (existing != null && existing.IsActive == false)
        {
            await dataService.ReactivateJob(existing.Id, messageThreadId);
            return existing.Id;
        }


        var location = await dataService.GetLocationByRegion(region);

        if (location == null)
        {
            return Guid.Empty;
        }

        ElectricityGroup? dbGroup = default;

        if (!string.IsNullOrEmpty(group))
        {
            dbGroup = await dataService.GetGroupByCodeAndLocationRegion(region, group);
        }

        var job = new MonitorJob()
        {
            ChatId = chatId,
            LocationId = location.Id,
            IsDtekJob = true,
            MessageThreadId = messageThreadId,
            GroupId = dbGroup?.Id,
            Type = dbGroup is null ? ElectricityJobType.AllGroups : ElectricityJobType.SingleGroup,
        };
        await dataService.Add(job);
        return job.Id;
    }

    public async Task<bool> SendExisiting(long chatId, string region, int? messageThreadId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        _logger.LogTrace("Preparing to send existing schedule for region {region} to chat {chatId}", region, chatId);
        var existingInfo = await dataService.GetCurrentScheduleImagesForRegion(region);

        string caption = $"Актуальний графік {LocationNameUtility.GetLocationByRegion(region)} на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        var info = new SendInfo()
        {
            ChatId = chatId,
            Filenames = existingInfo.ToList(),
            Caption = caption,
            MessageThreadId = messageThreadId,
        };

        _logger.LogTrace("Sending existing schedule for region {region} to chat {chatId} with {count} files", region, chatId, info.Filenames.Count());

        ReadyToSend?.Invoke(info);
        return true;
    }

    public async Task<bool> SendExisiting(long chatId, string region, string group, ElectricityJobType jobType, int? messageThreadId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        var existingInfo = await dataService.GetCurrentScheduleImagesForGroupRegion(group, region, jobType);
        var groupFromDb = await dataService.GetGroupByCodeAndLocationRegion(region, group);
        string groupName = groupFromDb?.GroupName ?? "";

        string caption = $"Актуальний графік {LocationNameUtility.GetLocationByRegion(region)} {groupName} на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        var info = new SendInfo()
        {
            ChatId = chatId,
            Filenames = existingInfo.ToList(),
            Caption = caption,
            MessageThreadId = messageThreadId,
        };

        ReadyToSend?.Invoke(info);
        return true;
    }

    public async Task<bool> SendExisiting(Guid jobAdded)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        var job = await dataService.GetJobById(jobAdded);
        if (job is null)
        {
            return false;
        }

        var info = await GetInfo(job);

        if (info == default || !info.Filenames.Any())
            return false;

        job.LastScheduleUpdate = job.Location.LastUpdated;
        await dataService.Update(job);

        ReadyToSend?.Invoke(info);
        return true;
    }

    internal async Task<SendInfo> GetInfo(MonitorJob job)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        var images = await dataService.GetImagesForJob(job.Id);

        string caption = $"Актуальний графік {LocationNameUtility.GetLocationByRegion(job.Location.Region)} {job.Group?.GroupName ?? ""} на " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

        return new SendInfo()
        {
            ChatId = job.ChatId,
            Filenames = images.ToList(),
            Caption = caption,
            MessageThreadId = job.MessageThreadId,
        };
    }

    internal async Task<bool> DisableJob(long chatId, string region, string? group, string reason)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        await dataService.DisableJobs(chatId, region, group, reason);
        return true;
    }

    public async Task DisableJob(long chatId, string reason)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        await dataService.DisableJobs(chatId, reason);
    }


    internal async Task<IEnumerable<MonitorJob>> GetActiveJobs(long chatId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        return await dataService.GetActiveJobs(chatId);
    }

    internal async Task<Dictionary<string, bool>> IsSubscribed(long chatId, string region)
    {
        using var scope = _serviceProvider.CreateScope();
        var dataService = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();

        return await dataService.GetSubscriptionList(chatId, region);
    }
}