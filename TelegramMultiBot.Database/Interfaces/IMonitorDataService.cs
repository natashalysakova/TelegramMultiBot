using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface IMonitorDataService
    {
        bool AddDtekJob(long chatId, string region);
        void AddJob(long chatId, string url);
        void DisableJob(MonitorJob activeJob, string reason);
        void DisableJob(long chatId, string reason);
        IEnumerable<MonitorJob> GetActiveJobs();
        IEnumerable<MonitorJob> GetJobs(long chatId);
        void UpdateNextRun(MonitorJob activeJob, int minutes);
        void UpdateNextRun(IGrouping<string, MonitorJob> jobs, int minutes);
    }

    public class MonitorDataService(BoberDbContext context) : IMonitorDataService
    {

        public void DisableJob(MonitorJob activeJob, string reason)
        {
            var job = context.Monitor.Where(x=>x.Id == activeJob.Id);
            DisableJobs(job, reason);
        }

        public void DisableJob(long chatId, string reason)
        {
            var jobs = context.Monitor.Where(x => x.ChatId == chatId);
            DisableJobs(jobs, reason);
        }

        private void DisableJobs(IEnumerable<MonitorJob> jobs, string reason)
        {
            foreach (var job in jobs)
            {
                job.IsActive = false;
                job.DeactivationReason = reason;
            }

            context.SaveChanges();
        }

        //return _dbContext.Reminders.Where(x => x.NextExecution < DateTime.Now).ToList();
        public IEnumerable<MonitorJob> GetActiveJobs()
        {
            var datetime = DateTime.Now;

            return context.Monitor.Where(x => x.IsActive && x.NextRun < datetime);
        }

        public void AddJob(long chatId, string url)
        {
            var job = new MonitorJob() { ChatId = chatId, Url = url, NextRun = DateTime.Now };
            context.Monitor.Add(job);
            context.SaveChanges();
        }

        public IEnumerable<MonitorJob> GetJobs(long chatId)
        {
            return context.Monitor.Where(x=>x.ChatId == chatId).ToList();
        }

        public bool AddDtekJob(long chatId, string region)
        {

            string url;
            switch (region)
            {
                case "krem": url = "https://www.dtek-krem.com.ua/ua/shutdowns";
                    break;

                default:
                    return false;

            }
            var exist = context.Monitor.Any(x => x.ChatId == chatId && x.Url == url);

            if (exist)
            {
                return false;
            }

            var job = new MonitorJob() { ChatId = chatId, Url = url, IsDtekJob = true, NextRun = DateTime.Now };
            context.Monitor.Add(job);
            context.SaveChanges();
            return true;
        }

        public void UpdateNextRun(MonitorJob activeJob, int minutes)
        {
            activeJob.NextRun = DateTime.Now.AddMinutes(minutes);
            context.SaveChanges();
        }

        public void UpdateNextRun(IGrouping<string, MonitorJob> jobs, int minutes)
        {
            foreach (var job in jobs)
            {
                UpdateNextRun(job, minutes);
            }
        }
    }
}
