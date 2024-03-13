using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TelegramMultiBot.Reminder
{
    internal abstract class Manager<T>(ILogger<Manager<T>> logger)
    {
        protected readonly ILogger _logger = logger;
        protected List<T> list = [];
        protected CancellationToken token;
        protected abstract string FileName { get; }

        protected List<T> Load()
        {
            var tmp = File.ReadAllText(FileName);
            return JsonConvert.DeserializeObject<List<T>>(tmp) ?? throw new InvalidOperationException("Cannot deserialize joblist");
        }

        protected void Save()
        {
            var tmp = JsonConvert.SerializeObject(list);
            File.WriteAllText(FileName, tmp);
            _logger.LogDebug("{file} saved", FileName);
        }
    }
}