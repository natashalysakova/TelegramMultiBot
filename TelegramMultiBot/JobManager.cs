// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;

internal class JobManager : IDisposable
{
    private List<Job> jobs;
    private CancellationToken token;
    private const string JobFile = "jobs.json";
    private object locker = new object();
    private int nextId => jobs.Any() ? jobs.Max(x => x.Id) + 1 : 0;

    //public delegate void SendNotificationHandler(long chatId, string message);
    public event Action<long, string> ReadyToSend = delegate { };

    public JobManager()
    {
        try
        {
            jobs = Load();
            LogUtil.Log($"Loadded {jobs.Count} jobs");
        }
        catch (Exception ex)
        {
            LogUtil.Log(ex.Message);
            jobs = new List<Job>();
            LogUtil.Log($"Created new job list");
        }
    }

    public void Dispose()
    {
        Save();
    }

    internal void Run(CancellationToken token)
    {
        this.token = token;
        Task.Run(async () =>
        {
            while (!this.token.IsCancellationRequested)
            {
                lock (locker)
                {
                    var jobsToSend = jobs.Where(x => x.NextExecution < DateTime.Now).ToList();
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

    private List<Job>? Load()
    {
        var tmp = File.ReadAllText(JobFile);
        return JsonConvert.DeserializeObject<List<Job>>(tmp);
    }

    private void Save()
    {
        var tmp = JsonConvert.SerializeObject(jobs);
        File.WriteAllText(JobFile, tmp);
        LogUtil.Log("jobs are saved");
    }

    internal DateTime AddJob(long chatid, string name, string cron, string text)
    {
        var job = new Job(nextId, chatid, name, text, cron);
        jobs.Add(job);
        LogUtil.Log($"Job {name} {cron} added");
        Save();
        return job.NextExecution;
    }

    internal List<Job> GetJobsByChatId(long id)
    {
        return jobs.Where(x => x.ChatId == id).ToList();
    }

    internal void DeleteJob(long id)
    {
        jobs.Remove(jobs.Single(x => x.Id == id));
        LogUtil.Log($"Job {id} removed");
        Save();
    }

    internal void DeleteJobsForChat(long chatId)
    {
        jobs.RemoveAll(x => x.ChatId == chatId);
        LogUtil.Log($"Jobs for chat {chatId} removed");
        Save();
    }
}
