using AutoMapper;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using TelegramMultiBot.Database;

namespace TelegramMultiBot.Reminder
{
    internal abstract class Manager<T>(ILogger<Manager<T>> logger, BoberDbContext dbContext, IMapper mapper)
    {
        protected readonly ILogger<Manager<T>> _logger = logger;
        protected List<T> list = [];
        protected CancellationToken token;
        protected abstract string FileName { get; }
        protected void Load()
        {
            //var tmp = File.ReadAllText(FileName);
            //return JsonConvert.DeserializeObject<List<T>>(tmp) ?? throw new InvalidOperationException("Cannot deserialize joblist");

            list = dbContext.Reminders.Select(x => mapper.Map<T>(x)).ToList();
        }

        protected void Save()
        {


            //var tmp = JsonConvert.SerializeObject(list);
            //File.WriteAllText(FileName, tmp);
            //_logger.LogDebug("{file} saved", FileName);
        }
    }
}