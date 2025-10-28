using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface IMonitorDataService
    {
        public MonitorJob? this[int i] { get; }

        public ElectricityLocation? this[string i] { get; }

        void AddLocation(ElectricityLocation location);
        Task<IEnumerable<ElectricityLocation>> GetLocations();
        Task<IEnumerable<MonitorJob>> GetActiveJobs();
        Task SaveChangesAsync();
        Task<IEnumerable<string>> GetImagesForJob(int id);
        Task<MonitorJob?> GetJobByChatIdAndUrl(long chatId, string url);
        Task<int> AddJob(MonitorJob job);
        Task<IEnumerable<MonitorJob>> GetJobByChatId(long chatId);

        Task DisableJobs(long chatId, string reason);
        Task DisableJobs(long chatId, string url, string reason);
        Task<IEnumerable<MonitorJob>> GetActiveJobs(long chatId);
        Task<Dictionary<string, bool>> IsSubscribed(long chatId, string region);
        Task<IEnumerable<string>> GetCurrentScheduleImagesForRegion(string region);
    }

    public class MonitorDataService(BoberDbContext context) : IMonitorDataService
    {
        public async Task<IEnumerable<ElectricityLocation>> GetLocations()
        {
            return await context.ElectricityLocations.Include(x => x.History).ToListAsync();
        }

        public ElectricityLocation? this[string location] => context.ElectricityLocations.SingleOrDefault(x => x.Url == location);

        public MonitorJob? this[int i]
        {
            get { return context.Monitor.Find(i); }
        }

        public async Task SaveChangesAsync()
        {
            await context.SaveChangesAsync();
        }

        public void AddLocation(ElectricityLocation location)
        {
            context.ElectricityLocations.Add(location);
            context.SaveChanges();
        }

        public async Task<IEnumerable<MonitorJob>> GetActiveJobs()
        {
            return await context.Monitor
                .Include(x => x.Location)
                    .ThenInclude(x => x.History)
                .Where(x => x.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<string>> GetImagesForJob(int id)
        {
            var job = await context.Monitor.Include(x => x.Location).ThenInclude(x=>x.History).FirstOrDefaultAsync();

            if(job.Group != null)
            {
                var groupImage = job.Location.History
                    .Where(x => x.Group == job.Group)
                    .OrderByDescending(x => x.Updated)
                    .FirstOrDefault();

                if (groupImage != null)
                    return [groupImage.ImagePath];

                return Array.Empty<string>();
            }
            else
            {
                var todayUnixTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();

                var locationImageGroups = job.Location.History
                    .Where(x => x.ScheduleDay != null && x.ScheduleDay >= todayUnixTime)
                    .GroupBy(x => x.ScheduleDay);

                var images = new List<string>();
                foreach (var group in locationImageGroups)
                {
                    var image = group.OrderByDescending(x => x.Updated)
                        .FirstOrDefault();

                    if (image != null)
                    {
                        images.Add(image.ImagePath);
                    }
                }

                return images;
            }
        }

        public async Task<MonitorJob?> GetJobByChatIdAndUrl(long chatId, string url)
        {
            return await context.Monitor
                .Include(x => x.Location)
                    .ThenInclude(x => x.History)
                .Where(x => x.ChatId == chatId && x.Location.Url == url)
                .FirstOrDefaultAsync();
        }

        public async Task<int> AddJob(MonitorJob job)
        {
            context.Monitor.Add(job);
            return await context.SaveChangesAsync();
        }

        public async Task<IEnumerable<MonitorJob>> GetJobByChatId(long chatId)
        {
            return await context.Monitor
                .Where(x => x.ChatId == chatId).ToListAsync();
        }

        public async Task DisableJobs(long chatId, string reason)
        {
            var jobs = context.Monitor.Where(x => x.ChatId == chatId);

            await DisableJobs(jobs, reason);
        }

        public async Task DisableJobs(long chatId, string url, string reason)
        {
            var jobs = context.Monitor.Include(x => x.Location).Where(x => x.ChatId == chatId && x.Location.Url == url);

            await DisableJobs(jobs, reason);
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
            return await context.Monitor
                .Include(x => x.Location)
                    .ThenInclude(x => x.History)
                .Where(x => x.IsActive  && x.ChatId == chatId).ToListAsync();
        }

        public async Task<Dictionary<string, bool>> IsSubscribed(long chatId, string region)
        {
            var groupList = await context.ElectricityHistory.Where(x=>x.Group != null).Select(x => x.Group).Distinct().ToListAsync();
            var subscriptions = await context.Monitor.Where(x=>x.ChatId == chatId && x.IsActive).Select(x=>x.Group).ToListAsync();

            var result = new Dictionary<string, bool>();
            foreach (var group in groupList)
            {
                result[group!] = subscriptions.Contains(group);
            }

            result["all"] = subscriptions.Contains(null);

            return result;
        }

        public async Task<IEnumerable<string>> GetCurrentScheduleImagesForRegion(string region)
        {
            var location = await context.ElectricityLocations.Include(x => x.History).SingleOrDefaultAsync(x => x.Location == region);
            var todayUnixTime = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();

            var latestImages = location.History
                .Where(x => x.ScheduleDay != 0 && x.ScheduleDay >= todayUnixTime && x.Group is null)
                .OrderByDescending(x => x.Updated)
                .Select(x => x.ImagePath);


            return latestImages;
        }
    }
}
