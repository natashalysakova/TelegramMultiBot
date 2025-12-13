using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Interfaces;

public interface IMonitorDataService
{
    Task<IEnumerable<ElectricityLocation>> GetLocations();
    Task<ElectricityLocation?> GetLocationByRegion(string region);

    Task<IEnumerable<MonitorJob>> GetActiveJobs();
    Task<MonitorJob?> GetJobBySubscriptionParameters(long chatId, string region, string? group);
    Task<MonitorJob?> GetJobById(Guid jobAdded);
    Task<IEnumerable<MonitorJob>> GetJobByChatId(long chatId);
    Task<IEnumerable<MonitorJob>> GetActiveJobs(long chatId);
    Task DisableJobs(long chatId, string reason);
    Task DisableJobs(long chatId, string region, string? group, string reason);
    Task ReactivateJob(Guid id, int? messageThreadId);

    Task<IEnumerable<string>> GetImagesForJob(Guid id);
    Task<IEnumerable<string>> GetCurrentScheduleImagesForRegion(string region);
    Task<IEnumerable<string>> GetCurrentScheduleImagesForGroupRegion(string group, string region, ElectricityJobType jobType);
    Task<Dictionary<string, bool>> GetSubscriptionList(long chatId, string region);

    Task<IEnumerable<ElectricityGroup>> GetAllGroups();
    Task<ElectricityGroup?> GetGroupByCodeAndLocationRegion(string region, string code, bool partialMatch = false);

    Task<int> Add<T>(T entity) where T : class;
    Task Update<T>(T entity) where T : class;
    Task DeleteOldHistory(DateTime cutoffDate);
    Task<IEnumerable<string>> GetAllHistoryImagePaths();
    Task DeleteHistoryWithMissingFiles(IEnumerable<string> missingFiles);
    Task<SvitlobotData> AddSvitlobotKey(string key, Guid id);
    Task<bool> RemoveSvitlobotKey(string key, Guid id);
    Task<IEnumerable<SvitlobotData>> GetAllSvitlobots();
    Task DeleteAllHistory();
}

public class MonitorDataService(BoberDbContext context) : IMonitorDataService
{
    public async Task<IEnumerable<ElectricityLocation>> GetLocations()
    {
        return await context.ElectricityLocations.Include(x => x.History).ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<MonitorJob>> GetActiveJobs()
    {
        return await GetJobsInternal()
            .Where(x => x.IsActive).ToListAsync();
    }



    public async Task<MonitorJob?> GetJobBySubscriptionParameters(long chatId, string region, string? group)
    {
        return await GetJobsInternal()
            .Where(x => x.ChatId == chatId && x.Location.Region == region && x.Group.GroupCode == group)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<MonitorJob>> GetJobByChatId(long chatId)
    {
        return await GetJobsInternal()
            .Where(x => x.ChatId == chatId).ToListAsync();
    }

    private IQueryable<MonitorJob> GetJobsInternal(Guid? id = default, bool disableTracking = true)
    {
        var jobs = context.Monitor
            .Include(x => x.Location)
                .ThenInclude(x => x.History)
                    .ThenInclude(x => x.Group)
            .Include(x => x.Group)
            .AsQueryable();

        if (disableTracking)
        {
            jobs = jobs.AsNoTracking();
        }

        if (id.HasValue)
        {
            jobs = jobs.Where(x => x.Id == id.Value);
        }

        return jobs;
    }

    public async Task DisableJobs(long chatId, string reason)
    {
        var jobs = context.Monitor.Where(x => x.ChatId == chatId);

        await DisableJobs(jobs, reason);
    }

    public async Task DisableJobs(long chatId, string region, string? group, string reason)
    {
        var jobs = GetJobsInternal(disableTracking: false).Where(x => x.ChatId == chatId && x.Location.Region == region && x.Group.GroupCode == group);

        await DisableJobs(jobs, reason);
    }
    private async Task DisableJob(MonitorJob job, string reason)
    {
        await DisableJobs([job], reason);
    }
    private async Task DisableJobs(IEnumerable<MonitorJob> jobs, string reason)
    {
        foreach (var job in jobs)
        {
            job.IsActive = false;
            job.DeactivationReason = reason;
        }

        await SaveChangesAsync();
    }

    public async Task<IEnumerable<MonitorJob>> GetActiveJobs(long chatId)
    {
        return await GetJobsInternal()
            .Where(x => x.IsActive && x.ChatId == chatId).ToListAsync();
    }

    public async Task<Dictionary<string, bool>> GetSubscriptionList(long chatId, string region)
    {
        var groupList = await context.ElectricityGroups.Select(x => x.GroupCode).ToListAsync();
        var subscriptions = await GetJobsInternal().Where(x => x.ChatId == chatId && x.Location.Region == region && x.IsActive).Select(x => x.Group).ToListAsync();

        var result = new Dictionary<string, bool>();
        foreach (var group in groupList)
        {
            result[group] = subscriptions.Any(x => x?.GroupCode == group);
        }

        result["all"] = subscriptions.Any(x => x is null);

        return result;
    }



    public async Task<MonitorJob?> GetJobById(Guid jobAdded)
    {
        return await context.Monitor
            .Include(x => x.Location)
                .ThenInclude(x => x.History)
            .FirstOrDefaultAsync(x => x.Id == jobAdded);
    }

    public async Task<ElectricityLocation?> GetLocationByRegion(string region)
    {
        return await context.ElectricityLocations
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Region == region);
    }

    public async Task ReactivateJob(Guid id, int? messageThreadId)
    {
        var job = context.Monitor.Find(id);
        job.IsActive = true;
        job.DeactivationReason = null;
        job.MessageThreadId = messageThreadId;
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ElectricityGroup>> GetAllGroups()
    {
        return await context.ElectricityGroups.AsNoTracking().ToListAsync();
    }


    public async Task Update<T>(T entity) where T : class
    {
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public Task<int> Add<T>(T entity) where T : class
    {
        context.Add(entity);
        return context.SaveChangesAsync();
    }

    public async Task<ElectricityGroup?> GetGroupByCodeAndLocationRegion(string region, string code, bool partialMatch = false)
    {
        var query = context.ElectricityGroups.Where(x => x.LocationRegion == region);

        if(partialMatch)
        {
            query = query.Where(x => EF.Functions.Like(x.GroupCode, $"%{code}%"));
        }
        else
        {
            query = query.Where(x => x.GroupCode == code);
        }

        return await query.SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<string>> GetCurrentScheduleImagesForRegion(string region)
    {
        var location = await context.ElectricityLocations
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.Region == region);

        if (location == null)
            return Array.Empty<string>();

        return GetLatestFromHistory(ElectricityJobType.AllGroups, location.History);
    }

    public async Task<IEnumerable<string>> GetCurrentScheduleImagesForGroupRegion(string group, string region, ElectricityJobType jobType)
    {
        var groupDb = await context.ElectricityGroups
            .Include(x => x.History)
            .FirstOrDefaultAsync(x => x.GroupCode == group && x.LocationRegion == region);

        return GetLatestFromHistory(jobType, groupDb.History);
    }

    public async Task<IEnumerable<string>> GetImagesForJob(Guid id)
    {
        var job = await GetJobsInternal(id).FirstOrDefaultAsync();
        var todayUnixTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();

        ICollection<ElectricityHistory> history;

        switch (job.Type)
        {
            case ElectricityJobType.AllGroups:
                history = job.Location.History;
                break;
            case ElectricityJobType.SingleGroupPlan:
            case ElectricityJobType.SingleGroup:

                if (job.GroupId is null)
                {
                    await DisableJob(job, "GroupId missing for job type + " + job.Type);
                    return Enumerable.Empty<string>();
                }

                history = job.Group.History;
                break;
            default:
                return Enumerable.Empty<string>();
        }

        return GetLatestFromHistory(job.Type, history);

    }

    private static IEnumerable<string> GetLatestFromHistory(ElectricityJobType jobType, ICollection<ElectricityHistory> history)
    {
        var todayUnixTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
        var toSend = new List<string>();

        var filteredHistory = history
            .Where(x => x.JobType == jobType);


        if (jobType == ElectricityJobType.SingleGroupPlan)
        {
            toSend.AddRange(filteredHistory.Where(x=>x.Updated == filteredHistory.Max(x=>x.Updated)).Select(x=>x.ImagePath).Distinct());
        }
        else
        {
            filteredHistory = filteredHistory
                .Where(x => x.ScheduleDay >= todayUnixTime);

            foreach (var day in filteredHistory.GroupBy(x => x.ScheduleDay))
            {
                var latestImage = day.OrderByDescending(x => x.Updated).FirstOrDefault();
                if (latestImage != null)
                {
                    toSend.Add(latestImage.ImagePath);
                }
            }
        }

        return toSend.Distinct();
    }

    public async Task DeleteOldHistory(DateTime cutoffDate)
    {
        var toDelete = context.ElectricityHistory
            .Where(x => x.Updated < cutoffDate);
        context.ElectricityHistory.RemoveRange(toDelete);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<string>> GetAllHistoryImagePaths()
    {
        return await context.ElectricityHistory.Select(x => x.ImagePath).ToListAsync();
    }

    public async Task DeleteHistoryWithMissingFiles(IEnumerable<string> missingFiles)
    {
        var recordsToDelete = context.ElectricityHistory
            .Where(x => missingFiles.Contains(x.ImagePath));

        context.ElectricityHistory.RemoveRange(recordsToDelete);
        await context.SaveChangesAsync();
    }

    public async Task<SvitlobotData> AddSvitlobotKey(string key, Guid id)
    {
        var existingRecord = await context.Svitlobot.FirstOrDefaultAsync(x => x.GroupId == id && x.SvitlobotKey == key);
        if (existingRecord != null)
        {
            return existingRecord;
        }

        var record = new SvitlobotData
        {
            GroupId = id,
            SvitlobotKey = key
        };

        context.Svitlobot.Add(record);
        await context.SaveChangesAsync();

        return record;
    }

    public async Task<bool> RemoveSvitlobotKey(string key, Guid id)
    {
        var record = context.Svitlobot.FirstOrDefault(x => x.GroupId == id && x.SvitlobotKey == key);
        if (record != null)
        {
            context.Svitlobot.Remove(record);
            await context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task<IEnumerable<SvitlobotData>> GetAllSvitlobots()
    {
        var records = context.Svitlobot
            .Include(x=>x.Group);

        return await records.ToListAsync();
    }
    
    public Task DeleteAllHistory()
    {
        var allRecords = context.ElectricityHistory;
        context.ElectricityHistory.RemoveRange(allRecords);
        return context.SaveChangesAsync();
    }
}
