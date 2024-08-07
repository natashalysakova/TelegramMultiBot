// See https://aka.ms/new-console-template for more information
using AutoMapper;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database.Models;
using TelegramMultiBot.Database.Services;
using TelegramMultiBot.Reminder;

internal class JobManager  //: Manager<ReminderJob>, IDisposable
{
    private readonly object _locker = new();
    private readonly ILogger<JobManager> _logger;
    private readonly IReminderDataService _dbservice;
    CancellationToken _cancellationToken;
    //private int NextId => list.Count != 0 ? list.Max(x => x.Id) + 1 : 0;

    public event Action<long, string, string> ReadyToSend = delegate { };

    public JobManager(ILogger<JobManager> logger, IReminderDataService dbservice) //: base(logger, dbservice, mapper)
    {
        _logger = logger;
        _dbservice = dbservice;
    }

    //public void Dispose()
    //{
    //    _dbservice.Save();
    //}

    public void Run(CancellationToken token)
    {
        _cancellationToken = token;
        _ = Task.Run(async () =>
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                //lock (_locker)
                //{
                    var jobsToSend = _dbservice.GetJobstForExecution(); 
                    foreach (var job in jobsToSend)
                    {
                        ReadyToSend(job.ChatId, job.Message, job.FileId);
                        _dbservice.JobSended(job);
                    }
                //}
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }, token);
    }

    internal DateTime AddJob(long chatid, string name, string cron, string? text, string? photoId)
    {
        var job = _dbservice.Add(chatid, name, text, cron, photoId);
        _logger.LogDebug("Job {name} {cron} added", name, cron);
        return job.NextExecution;
    }

    internal List<ReminderJob> GetJobsByChatId(long chatId)
    {
        return _dbservice.GetJobsbyChatId(chatId);
    }

    internal void DeleteJob(Guid id)
    {
        _dbservice.DeleteJob(id);
        _logger.LogDebug("Job {id} removed", id);
    }

    internal void DeleteJobsForChat(long chatId)
    {
        _dbservice.DeleteJobsForChat(chatId);
        _logger.LogDebug("Jobs for chat {chatId} removed", chatId);;
    }
}