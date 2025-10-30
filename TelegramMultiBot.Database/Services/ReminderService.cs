using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Services
{
    public interface IReminderDataService
    {
        IEnumerable<ReminderJob> GetJobstForExecution();
        void JobSended(ReminderJob job);
        ReminderJob Add(long chatid, int? threadId, string name, string? text, string cron, string? fileId);
        List<ReminderJob> GetJobsbyChatId(long chatId);
        void DeleteJob(Guid id);
        void DeleteJobsForChat(long chatId);
    }

    public class ReminderService : IReminderDataService
    {
        private readonly BoberDbContext _dbContext;

        public ReminderService(BoberDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IEnumerable<ReminderJob> GetJobstForExecution()
        {
            return _dbContext.Reminders.Where(x => x.NextExecution < DateTime.Now).ToList();
        }

        public ReminderJob Add(long chatid, int? threadId, string name, string? text, string cron, string? fileId)
        {
            ReminderJob job = new ReminderJob
            {
                ChatId = chatid,
                Name = name,
                Config = cron,
                Message = text,
                NextExecution = ParseNext(cron),
                FileId = fileId,
                MessageThreadId = threadId
            };

            _dbContext.Reminders.Add(job);
            _dbContext.SaveChanges();   
            return job;
        }

        public void JobSended(ReminderJob job)
        {
            try
            {
                job.NextExecution = ParseNext(job.Config);
                _dbContext.Entry(job).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                _dbContext.SaveChanges();
            }
            catch
            {
                throw new Exception($"Failed to get next execution time for job ({job.Id}) {job.Name}");
            }
            //LogUtil.Log($"Job {Name} in {ChatId} has new execution time: {nextExecution}");

        }
        public static DateTime ParseNext(string cron)
        {
            if (Cronos.CronExpression.TryParse(cron, out var exp))
            {
                var next = exp.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                if (next.HasValue)
                    return next.Value.DateTime;
            }

            throw new InvalidDataException("cannot parse CRON");
        }

        public List<ReminderJob> GetJobsbyChatId(long chatId)
        {
            return _dbContext.Reminders.Where(x => x.ChatId == chatId).ToList();
        }

        public void DeleteJob(Guid id)
        {
            var jobToDelete = _dbContext.Reminders.Find(id);
            if (jobToDelete != null)
            {
                _dbContext.Reminders.Remove(jobToDelete);
                _dbContext.SaveChanges();
            }
        }

        public void DeleteJobsForChat(long chatId)
        {
            var jobToDelete = _dbContext.Reminders.Where(x => x.ChatId == chatId);
            if(jobToDelete.Any())
            {
                _dbContext.Reminders.RemoveRange(jobToDelete);
                _dbContext.SaveChanges();
            }
        }
    }    
}

