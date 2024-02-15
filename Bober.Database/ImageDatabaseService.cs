using Bober.Library.Contract;
using Bober.Library.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Logging;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Bober.Database.Services
{
    public class ImageDatabaseService
    {
        private readonly BoberDbContext _context;
        private readonly ILogger<ImageDatabaseService> _logger;

        public ImageDatabaseService(BoberDbContext context, ILogger<ImageDatabaseService> logger)
        {
            _context = context;
            _logger = logger;
        }



        public IEnumerable<ImageJob> ActiveJobs
        {
            get
            {
                return _context.Jobs.Where(x => x.Status == ImageJobStatus.Queued || x.Status == ImageJobStatus.Running);
            }
        }
        public int RunningJobs
        {
            get
            {
                return _context.Jobs.Where(x => x.Status == ImageJobStatus.Running).Count();
            }
        }

        public void CancelUnfinishedJobs()
        {
            var jobs = _context.Jobs.Where(x => x.Status == ImageJobStatus.Running || x.Status == ImageJobStatus.Queued);
            foreach (var item in jobs)
            {
                item.Status = ImageJobStatus.Failed;
                item.Finised = DateTime.Now;
            }
            _context.SaveChanges();

        }


        public void Enqueue(IInputData message)
        {
            var type = message.GetType();
            if(type == typeof(MessageData))
            {
                Enqueue(message as MessageData);
                return;
            }

            if(type == typeof(CallbackData))
            {
                Enqueue(message as MessageData);
                return;
            }

        }
        private void Enqueue(MessageData message)
        {
            var job = JobFromData(message);

            job.Text = message.Text;
            job.BotMessageId = message.BotMessageId;
            job.PostInfo = job.Text.Contains("#info");

            _context.Jobs.Add(job);
            _context.SaveChanges();
        }

        private void Enqueue(CallbackData data)
        {
            var job = JobFromData(data);
            job.UpscaleModifyer = data.Upscale;

            var result = _context.JobResult.Include(x => x.Job).Single(x => x.Id == data.PreviousJobResultId);

            job.PostInfo = result.Job.PostInfo;
            job.PreviousJobResultId = result.Id;

            if (_context.Jobs.Any(x =>
                x.PreviousJobResultId == job.PreviousJobResultId
                && (x.Status == ImageJobStatus.Queued || x.Status == ImageJobStatus.Running)
                && x.Type == job.Type
                && (x.UpscaleModifyer == job.UpscaleModifyer)
                ))
            {
                throw new AlreadyRunningException("Апскейл для цього зображення вже в роботі");
            }

            _context.Jobs.Add(job);

            _context.SaveChanges();

        }

        private ImageJob JobFromData(IInputData data)
        {
            var job = new ImageJob();
            job.UserId = data.UserId;
            job.ChatId = data.ChatId;
            job.MessageId = data.MessageId;
            job.MessageThreadId = data.MessageThreadId;
            job.Status = ImageJobStatus.Queued;
            job.Type = data.JobType;

            return job;
        }

        public JobResult? GetJobResult(string guid)
        {
            return GetJobResult(Guid.Parse(guid));
        }
        public JobResult? GetJobResult(Guid? guid)
        {

            var result = _context.JobResult.Find(guid);
            if (result == null)
            {
                return null;
            }

            _context.Entry(result).Reference(x => x.Job).Load();
            return result;
        }

        internal IEnumerable<ImageJob> GetJobsOlderThan(DateTime date)
        {
            return _context.Jobs.Include(x => x.Results).Where(x => x.Created < date);
        }

        public void JobFailed(ImageJob job)
        {
            job.Status = ImageJobStatus.Failed;
            job.Finised = DateTime.Now;

            _context.SaveChanges();
        }

        public void JobFinished(ImageJob job)
        {
            job.Status = ImageJobStatus.Succseeded;
            job.Finised = DateTime.Now;

            _context.SaveChanges();
        }

        internal void RemoveJobs(IEnumerable<ImageJob> jobsToDelete)
        {

            _context.Jobs.RemoveRange(jobsToDelete);
            _context.SaveChanges();
        }

        public bool TryDequeue(out ImageJob? job)
        {
            try
            {
                if (_context.Jobs.Any(x => x.Status == ImageJobStatus.Queued))
                {
                    var queued = _context.Jobs.Include(x => x.Results).Where(x => x.Status == ImageJobStatus.Queued);
                    var ordered = queued.OrderBy(x => x.Created);

                    job = queued.FirstOrDefault();

                    job.Started = DateTime.Now;
                    job.Status = ImageJobStatus.Running;
                    _context.SaveChanges();

                    return true;
                }
                else
                {
                    job = default;
                    return false;
                }

            }
            catch (Exception)
            {
                job = default;
                return false;
            }
        }

        public void PostProgress(ImageJob job, double progress, string text)
        {
            job.Progress = progress;
            job.TextStatus = text;
            _context.SaveChanges();
        }
    }
}
