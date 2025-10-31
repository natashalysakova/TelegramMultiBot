// See https://aka.ms/new-console-template for more information

// See https://aka.ms/new-console-template for more information
namespace TelegramMultiBot.Reminder;

[Serializable]
public class Job(int id, long chatId, string Name, string message, string config)
{
    private bool _sended = false;
    private DateTime _nextExecution;
    public int Id { get; } = id;
    public string Name { get; } = Name;
    public string Message { get; } = message;
    public string Config { get; } = config;
    public long ChatId { get; } = chatId;

    public DateTime NextExecution
    {
        get
        {
            if (_nextExecution == default || _sended)
            {
                try
                {
                    _nextExecution = CronUtil.ParseNext(Config);
                    _sended = false;
                }
                catch
                {
                    throw new Exception($"Failed to get next execution time for job ({Id}) {Name}");
                }
                //LogUtil.Log($"Job {Name} in {ChatId} has new execution time: {nextExecution}");
            }
            return _nextExecution;
        }
    }

    public void Sended()
    {
        _sended = true;
    }
}