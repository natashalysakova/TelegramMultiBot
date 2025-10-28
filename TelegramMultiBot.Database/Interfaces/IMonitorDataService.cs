using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface IMonitorDataService
    {
        DbSet<MonitorJob> Jobs { get ; }
        public MonitorJob? this[int i] {  get; }

        IQueryable<ElectricityLocation> Locations { get; }
        public ElectricityLocation? this[string i] { get; }

        void SaveChanges();
        Task SaveChangesAsync();
        void AddLocation(ElectricityLocation location);
        //int AddDtekJob(long chatId, string region);
        //void AddJob(long chatId, string url);
        //void DisableJob(MonitorJob activeJob, string reason);
        //void DisableJob(long chatId, string reason);
        //IEnumerable<MonitorJob> GetActiveJobs();
        //IEnumerable<MonitorJob> GetJobs(long chatId);
        //void UpdateNextRun(MonitorJob activeJob, int minutes);
        //void UpdateNextRun(IGrouping<string, MonitorJob> jobs, int minutes);
    }

    public class MonitorDataService(BoberDbContext context) : IMonitorDataService
    {
        public DbSet<MonitorJob> Jobs { get => context.Monitor; }

        public IQueryable<ElectricityLocation> Locations => context.ElectricityLocations.Include(x=>x.History);

        public ElectricityLocation? this[string location] => context.ElectricityLocations.SingleOrDefault(x=>x.Location == location);

        public void SaveChanges()
        {
            context.SaveChanges();
        }

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

        //public void DisableJob(MonitorJob activeJob, string reason)
        //{
        //    var job = context.Monitor.Where(x=>x.Id == activeJob.Id);
        //    DisableJobs(job, reason);
        //}



        //public IEnumerable<MonitorJob> GetActiveJobs()
        //{
        //    var datetime = DateTime.Now;

        //    return context.Monitor.Where(x => x.IsActive);
        //}

        //public void AddJob(long chatId, string url)
        //{
        //    var job = new MonitorJob() { ChatId = chatId, Url = url, NextRun = DateTime.Now };
        //    context.Monitor.Add(job);
        //    context.SaveChanges();
        //}

        //public IEnumerable<MonitorJob> GetJobs(long chatId)
        //{
        //    return context.Monitor.Where(x=>x.ChatId == chatId).ToList();
        //}




    }
}
