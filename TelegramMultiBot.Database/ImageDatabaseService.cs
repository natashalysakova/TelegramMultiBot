using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Bober.Database.Services
{
    public class ImageDatabaseService : IDatabaseService
    {
        private readonly BoberDbContext _context;
        private readonly ILogger<ImageDatabaseService> _logger;
        private readonly IMapper _mapper;

        public ImageDatabaseService(BoberDbContext context, ILogger<ImageDatabaseService> logger, IMapper mapper)
        {
            _context = context;
            _logger = logger;
            _mapper = mapper;

            _context.Database.Migrate();
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


        public Guid Enqueue(IInputData message)
        {
            var type = message.GetType();
            if (type == typeof(MessageData))
            {
                return Enqueue(message as MessageData);
            }

            if (type == typeof(CallbackData))
            {
                return Enqueue(message as CallbackData);
            }
            return Guid.Empty;
        }
        private Guid Enqueue(MessageData message)
        {
            var job = JobFromData(message);

            job.Text = message.Text;
            job.BotMessageId = message.BotMessageId.Value;
            job.PostInfo = job.Text.Contains("#info");
            if(job.Text.Contains("#auto"))
            {
                job.Diffusor = "Automatic1111";
            }
            if (job.Text.Contains("#comfy"))
            {
                job.Diffusor = "ComfyUI";
            }

            _context.Jobs.Add(job);
            _context.SaveChanges();
            _logger.LogDebug(job.Id + " added to the queue");

            return job.Id;
        }

        private Guid Enqueue(CallbackData data)
        {
            var job = JobFromData(data);
            job.UpscaleModifyer = data.Upscale;

            var result = _context.JobResult.Include(x => x.Job).Single(x => x.Id == data.PreviousJobResultId);

            job.PostInfo = result.Job.PostInfo;
            job.PreviousJobResultId = result.Id;
            job.TextStatus = "added";

            if (_context.Jobs.Any(x =>
                x.PreviousJobResultId == job.PreviousJobResultId
                && (x.Status == ImageJobStatus.Queued || x.Status == ImageJobStatus.Running)
                && x.Type == job.Type
                && (x.UpscaleModifyer == job.UpscaleModifyer)
                ))
            {
                return Guid.Empty;
            }

            _context.Jobs.Add(job);
            _context.SaveChanges();
            _logger.LogDebug(job.Id + " added to the queue");

            return job.Id;
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
            job.TextStatus = "added";
            job.BotMessageId = data.BotMessageId.Value;
            return job;
        }

        public IEnumerable<JobInfo> GetJobsOlderThan(DateTime date)
        {
            return _context.Jobs.Include(x => x.Results).Where(x => x.Finised != default && x.Finised < date).Select(x => _mapper.Map<JobInfo>(x));
        }

        public void RemoveJobs(IEnumerable<string> jobsToDelete)
        {
            var jobs = _context.Jobs.Where(x => jobsToDelete.Contains(x.Id.ToString()));

            _context.Jobs.RemoveRange(jobs);
            _context.SaveChanges();
        }

        public bool TryDequeue(out JobInfo? job)
        {
            try
            {
                if (_context.Jobs.Any(x => x.Status == ImageJobStatus.Queued))
                {
                    var queued = _context.Jobs.Include(x => x.Results).Where(x => x.Status == ImageJobStatus.Queued);
                    var ordered = queued.OrderBy(x => x.Created);
                    var imageJob = queued.First();
                    imageJob.Started = DateTime.Now;
                    imageJob.Status = ImageJobStatus.Running;
                    _context.SaveChanges();

                    job = _mapper.Map<JobInfo>(imageJob);


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

        public void PostProgress(string jobId, double progress, string text)
        {
            var job = _context.Jobs.Single(x => x.Id == Guid.Parse(jobId));

            if (progress == 100)
            {
                job.Status = ImageJobStatus.Succseeded;
                job.Finised = DateTime.Now;
            }

            if (progress == -1)
            {
                job.Status = ImageJobStatus.Failed;
                job.Finised = DateTime.Now;
            }

            job.Progress = progress;
            job.TextStatus = text;
            _context.SaveChanges();
        }

        public JobInfo? GetJob(string jobId)
        {
            if (Guid.TryParse(jobId, out var id))
            {
                var job = _context.Jobs.FirstOrDefault(x => x.Id == Guid.Parse(jobId));

                if (job == null)
                    return null;

                return _mapper.Map<JobInfo>(job);

            }

            return null;
        }




        public JobResultInfo? GetJobResult(string jobResultId)
        {
            var job = _context.JobResult.FirstOrDefault(x => x.Id == Guid.Parse(jobResultId));

            if (job == null)
                return null;

            return _mapper.Map<JobResultInfo>(job);
        }

        public int ActiveJobsCount(long userId)
        {
            return ActiveJobs.Where(x => x.UserId == userId).Count();
        }

        public void AddResult(string id, JobResultInfo jobResultInfo)
        {
            var jobResult = _mapper.Map<JobResult>(jobResultInfo);
            var job = _context.Jobs.FirstOrDefault(x => x.Id.ToString() == id);

            job.Results.Add(jobResult);
            _context.SaveChanges();

        }

        public void Update(JobInfo job)
        {
            var entity = _mapper.Map<ImageJob>(job);

            _context.Entry(entity).State  = EntityState.Modified;
            _context.SaveChanges();
        }

        public void PushBotId(string jobId, int messageId)
        {
            var job = _context.Jobs.Single(x => x.Id == Guid.Parse(jobId));

            job.BotMessageId = messageId;
            _context.SaveChanges();
        }

        public void ReturnToQueue(JobInfo job)
        {
            var jobentity = _context.Jobs.Single(x => x.Id == Guid.Parse(job.Id));
            jobentity.Status = ImageJobStatus.Queued;
            jobentity.Started = default;

            _context.SaveChanges();
        }
    }
}
