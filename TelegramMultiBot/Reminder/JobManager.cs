// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Reminder;

internal class JobManager : Manager<Job>, IDisposable
{
    private readonly object _locker = new();
    private int NextId => list.Count != 0 ? list.Max(x => x.Id) + 1 : 0;

    protected override string FileName => "jobs.json";

    public event Action<long, string> ReadyToSend = delegate { };

    public JobManager(ILogger<JobManager> logger) : base(logger)
    {
        try
        {
            list = Load();
            _logger.LogDebug("Loadded {count} jobs", list.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("{error}", ex.Message);
            list = [];
            _logger.LogDebug("Created new job list");
        }
    }

    public void Dispose()
    {
        Save();
    }

    public void Run(CancellationToken token)
    {
        this.token = token;
        _ = Task.Run(async () =>
        {
            while (!this.token.IsCancellationRequested)
            {
                lock (_locker)
                {
                    var jobsToSend = list.Where(x => x.NextExecution < DateTime.Now).ToList();
                    foreach (var job in jobsToSend)
                    {
                        ReadyToSend(job.ChatId, job.Message);
                        job.Sended();
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }, token);
    }

    internal DateTime AddJob(long chatid, string name, string cron, string text)
    {
        var job = new Job(NextId, chatid, name, text, cron.ToString());
        list.Add(job);
        _logger.LogDebug("Job {name} {cron} added", name, cron);
        Save();
        return job.NextExecution;
    }

    internal List<Job> GetJobsByChatId(long id)
    {
        return list.Where(x => x.ChatId == id).ToList();
    }

    internal void DeleteJob(long id)
    {
        _ = list.Remove(list.Single(x => x.Id == id));
        _logger.LogDebug("Job {id} removed", id);
        Save();
    }

    internal void DeleteJobsForChat(long chatId)
    {
        _ = list.RemoveAll(x => x.ChatId == chatId);
        _logger.LogDebug("Jobs for chat {chatId} removed", chatId);
        Save();
    }
}