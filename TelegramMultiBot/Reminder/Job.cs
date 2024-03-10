// See https://aka.ms/new-console-template for more information
using TelegramMultiBot;

[Serializable]
public class Job
{
    private bool sended = false;
    private DateTime nextExecution;
    public int Id { get; }
    public string Name { get; }
    public string Message { get; }
    public string Config { get; }
    public long ChatId { get; }
    public DateTime NextExecution
    {
        get
        {
            if (nextExecution == default || sended)
            {
                try
                {
                    nextExecution = CronUtil.ParseNext(Config);
                    sended = false;
                }
                catch
                {
                    throw new Exception($"Failed to get next execution time for job ({Id}) {Name}");
                }
                //LogUtil.Log($"Job {Name} in {ChatId} has new execution time: {nextExecution}");
            }
            return nextExecution;
        }
    }

    public Job(int id, long chatId, string Name, string message, string config)
    {
        this.Id = id;
        this.ChatId = chatId;
        this.Name = Name;
        this.Message = message;
        this.Config = config;
    }

    public void Sended()
    {
        sended = true;
    }
}
