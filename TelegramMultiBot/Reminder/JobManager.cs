// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Reminder;

internal class JobManager : Manager<Job>, IDisposable
{
    private object locker = new();
    private int nextId => list.Any() ? list.Max(x => x.Id) + 1 : 0;

    protected override string fileName => "jobs.json";

    public event Action<long, string> ReadyToSend = delegate { };

    public JobManager(ILogger<JobManager> logger) : base(logger)
    {
        try
        {
            list = Load();
            _logger.LogDebug($"Loadded {list.Count} jobs");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex.Message);
            list = [];
            _logger.LogDebug($"Created new job list");
        }
    }

    public void Dispose()
    {
        Save();
    }

    public void Run(CancellationToken token)
    {
        this.token = token;
        Task.Run(async () =>
        {
            while (!this.token.IsCancellationRequested)
            {
                lock (locker)
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
        });
    }

    internal DateTime AddJob(long chatid, string name, string cron, string text)
    {
        var job = new Job(nextId, chatid, name, text, cron.ToString());
        list.Add(job);
        _logger.LogDebug($"Job {name} {cron} added");
        Save();
        return job.NextExecution;
    }

    internal List<Job> GetJobsByChatId(long id)
    {
        return list.Where(x => x.ChatId == id).ToList();
    }

    internal void DeleteJob(long id)
    {
        list.Remove(list.Single(x => x.Id == id));
        _logger.LogDebug($"Job {id} removed");
        Save();
    }

    internal void DeleteJobsForChat(long chatId)
    {
        list.RemoveAll(x => x.ChatId == chatId);
        _logger.LogDebug($"Jobs for chat {chatId} removed");
        Save();
    }
}