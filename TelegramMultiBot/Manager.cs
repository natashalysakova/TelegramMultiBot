using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TelegramMultiBot
{
    internal abstract class Manager<T>
    {
        protected readonly ILogger _logger;
        protected List<T> list;
        protected CancellationToken token;
        abstract protected string fileName {  get; }

        public Manager(ILogger<Manager<T>> logger)
        {
            _logger = logger;
            list = new List<T>();
        }

        protected List<T>? Load()
        {
            var tmp = File.ReadAllText(fileName);
            return JsonConvert.DeserializeObject<List<T>>(tmp);
        }

        protected void Save()
        {
            var tmp = JsonConvert.SerializeObject(list);
            File.WriteAllText(fileName, tmp);
            _logger.LogDebug(fileName + " saved");
        }
    }
}
