using Bober.Library.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Bober.Worker
{
    record Subscription(string topic, string callback);

    internal class WebhookService
    {
        public const string successTopic = "job.finished";
        public const string failureTopic = "job.failed";

        private readonly HttpClient _httpClient;
        private List<Subscription> _subscriptions;

        public void Subscripe(Subscription subscription)
        {
            _subscriptions.Add(subscription);
        }

        public async Task PublishMessage(string topic, JobInfo info)
        {
            var webhooks = _subscriptions.Where(x => x.topic == topic);

            foreach (var webhook in webhooks)
            {
                await _httpClient.PostAsJsonAsync(webhook.callback, info);
            }
        }
    }
}
