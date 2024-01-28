using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TelegramMultiBot
{
    internal abstract class Manager<T>
    {
        protected readonly ILogger _logger;
        protected List<T> list;
        protected CancellationToken token;

        public Manager(ILogger<Manager<Job>> logger)
        {
            _logger = logger;
            list = new List<T>();
        }

        protected List<T>? Load(string fileName)
        {
            var tmp = File.ReadAllText(fileName);
            return JsonConvert.DeserializeObject<List<T>>(tmp);
        }

        protected void Save(string fileName)
        {
            var tmp = JsonConvert.SerializeObject(list);
            File.WriteAllText(fileName, tmp);
            _logger.LogDebug(fileName + " saved");
        }
    }
}
