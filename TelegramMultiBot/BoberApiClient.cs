using Bober.Library.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Configuration;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.Metrics;
using Newtonsoft.Json;
using System.Net.Http.Json;

namespace TelegramMultiBot
{
    public class BoberApiClient
    {
        private readonly HttpClient _httpClient;
        public const string successCallback = "/wh/job/finished";
        public const string failureCallback = "/wh/job/failed";
        public const string progressCallback = "/wh/job/progress";
        public const string successTopic = "job.finished";
        public const string failureTopic = "job.failed";
        public const string progressTopic = "job.progress";

        private List<Subscription> subscriptions;



        public BoberApiClient(IConfiguration configuration)
        {
            var settings = configuration.GetSection(ApiHostSettings.Name).Get<ApiHostSettings>();
            var basepath = $"https://{settings.host}:{settings.port}";
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(basepath)
            };

            subscriptions = new List<Subscription>() {
                new Subscription(successTopic, url(successCallback)),
                new Subscription(failureTopic, url(failureCallback)),
                new Subscription(progressTopic, url(progressCallback)),

            };
        }

        private string url(string path)
        {
            return "https://localhost:7279" + path;
        }

        public BoberApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        internal JobResultInfo GetJobResultInfo(string? id)
        {
            throw new NotImplementedException();
        }

        internal async Task<string> SendToQueue(MessageData messageData)
        {
            var response = await _httpClient.PostAsJsonAsync("Queue/AddFromMessage", messageData);
            return await response.Content.ReadAsStringAsync();
        }

        internal async Task<string> SendToQueue(CallbackData messageData)
        {
            var response = await _httpClient.PostAsJsonAsync("Queue/AddFromCallback", messageData);
            return await response.Content.ReadAsStringAsync();

        }

        internal void Subscribe()
        {
            foreach (var item in subscriptions)
            {
                var res = _httpClient.PostAsJsonAsync("/Subscription", item);
                res.Wait();
            }
        }
        //internal void Unsubscribe()
        //{
        //    foreach (var item in subscriptions)
        //    {
        //        _httpClient.PostAsJsonAsync("/unsubscribe", item);
        //    }
        //}

    }
}
