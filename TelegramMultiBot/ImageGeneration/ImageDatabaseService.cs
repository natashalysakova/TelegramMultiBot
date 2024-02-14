using AngleSharp.Html;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TelegramMultiBot.Commands.CallbackDataTypes;
using TelegramMultiBot.Database;
using TelegramMultiBot.ImageGeneration.Exceptions;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.ImageGeneration
{
    internal class ImageDatabaseService
    {
        private readonly BoberDbContext _context;
        private readonly ILogger<ImageDatabaseService> _logger;
        object locker= new object();

        public ImageDatabaseService(BoberDbContext context, ILogger<ImageDatabaseService> logger)
        {
            _context = context;
            _logger = logger;
        }



        public IEnumerable<ImageJob> ActiveJobs
        {
            get
            {
                lock (locker)
                {
                    return _context.Jobs.Where(x => x.Status == ImageJobStatus.Queued || x.Status == ImageJobStatus.Running);
                }
            }
        }
        public int RunningJobs
        {
            get
            {
                lock (locker)
                {
                    return _context.Jobs.Where(x => x.Status == ImageJobStatus.Running).Count();
                }
            }
        }

        internal void CancelUnfinishedJobs()
        {
            lock (locker)
            {
                var jobs = _context.Jobs.Where(x => x.Status == ImageJobStatus.Running || x.Status == ImageJobStatus.Queued);
                foreach (var item in jobs)
                {
                    item.Status = ImageJobStatus.Failed;
                    item.Finised = DateTime.Now;
                }
                _context.SaveChanges();

            }
        }

        internal void CleanupOldJobs(int jobAge, bool removeFiles)
        {
            lock (locker)
            {
                var date = DateTime.Now.Subtract(TimeSpan.FromSeconds(jobAge));
                var jobsToDelete = _context.Jobs.Include(x => x.Results).Where(x => x.Created < date);
                _logger.LogDebug("Jobs to cleanup:" + jobsToDelete.Count());
                if (removeFiles)
                {
                    foreach (var item in jobsToDelete)
                    {
                        foreach (var res in item.Results)
                        {
                            try
                            {
                                _logger.LogDebug("Removing file:" + res.FilePath);
                                System.IO.File.Delete(res.FilePath);

                                var directory = Path.GetDirectoryName(res.FilePath);

                                if (!Directory.GetFiles(directory).Any())
                                {
                                    Directory.Delete(directory);
                                }
                            }
                            catch (Exception)
                            {
                                _logger.LogError("Failed to remove:" + res.FilePath);
                            }

                        }
                    }
                }

                _context.Jobs.RemoveRange(jobsToDelete);
                _context.SaveChanges();
            }
        }

        internal void Enqueue(Message message, int botMessageId)
        {
            var job = new ImageJob();
            job.UserId = message.From.Id;
            job.Text = message.Text;
            job.ChatId = message.Chat.Id;
            job.MessageId = message.MessageId;
            job.MessageThreadId = message.MessageThreadId;
            job.BotMessageId = botMessageId;
            job.PostInfo = job.Text.Contains("#info");
            job.Status = ImageJobStatus.Queued;
            job.Type = JobType.Text2Image;

            lock (locker)
            {
                _context.Jobs.Add(job);
                _context.SaveChanges();
            }
        }

        internal void Enqueue(CallbackQuery query)
        {
            var job = new ImageJob();
            job.UserId = query.Message.From.Id;
            job.ChatId = query.Message.Chat.Id;
            job.MessageId = query.Message.MessageId;
            job.MessageThreadId = query.Message.MessageThreadId;
            //job.BotMessageId = botMessageId;
            job.Status = ImageJobStatus.Queued;


            var data = ImagineCallbackData.FromString(query.Data);
            if (data.Upscale != null)
            {
                job.UpscaleModifyer = data.Upscale.Value;
            }

            job.Type = data.JobType;


            lock (locker)
            {
                var result = _context.JobResult.Include(x=>x.Job).Single(x => x.Id.ToString() == data.Id);

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
            }

            SaveChanges();

        }

        internal JobResult? GetJobResult(string guid)
        {
            return GetJobResult(Guid.Parse(guid));
        }
        internal JobResult? GetJobResult(Guid? guid)
        {
            lock (locker)
            {
                
                var result = _context.JobResult.Find(guid);
                if (result == null)
                {
                    return null;
                }

                _context.Entry(result).Reference(x => x.Job).Load();
                return result;
            }
        }

        internal void JobFailed(ImageJob job)
        {
            job.Status = ImageJobStatus.Failed;
            job.Finised = DateTime.Now;

            SaveChanges();
        }

        internal void JobFinished(ImageJob job)
        {
            job.Status = ImageJobStatus.Succseeded;
            job.Finised = DateTime.Now;

            SaveChanges();
        }

        internal void SaveChanges()
        {
            lock (locker)
            {
                _context.SaveChanges();
            }
        }

        internal bool TryDequeue(out ImageJob? job)
        {
            lock (locker)
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
        }
    }


}
