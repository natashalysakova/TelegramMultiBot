using Newtonsoft.Json;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    public class Automatic1111Cache
    {
        private const string _samplerPath = "/sdapi/v1/samplers";
        private const string _checkpointsInfoPath = "/sdapi/v1/sd-models";

        private record SamplerChache(string Server, IEnumerable<Sampler> Samplers);

        private record CheckpointsCache(string Server, IEnumerable<CheckpointsInfo> Checkpoints);

        private static readonly ICollection<SamplerChache> _samplers = [];
        private static readonly ICollection<CheckpointsCache> _checkpointsInfo = [];

        public static void InitCache(Uri server)
        {
            if (!_samplers.Any(x => x.Server == server.Host))
            {
                _samplers.Add(new SamplerChache(server.Host, LoadFromServer<Sampler>(server, _samplerPath).Result));
            }

            if (!_checkpointsInfo.Any(x => x.Server == server.Host))
            {
                _checkpointsInfo.Add(new CheckpointsCache(server.Host, LoadFromServer<CheckpointsInfo>(server, _checkpointsInfoPath).Result));
            }
        }

        public static IEnumerable<Sampler> GetSampler(Uri server)
        {
            return _samplers.Single(x => x.Server == server.Host).Samplers;
        }

        public static IEnumerable<CheckpointsInfo> GetCheckpoints(Uri server)
        {
            return _checkpointsInfo.Single(x => x.Server == server.Host).Checkpoints;
        }

        private static async Task<IEnumerable<T>> LoadFromServer<T>(Uri uri, string path)
        {
            using (var client = new HttpClient() { BaseAddress = uri })
            {
                var response = await client.GetAsync(path);
                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<IEnumerable<T>>(await response.Content.ReadAsStringAsync()) ?? [];
                }
            }

            return [];
        }
    }
}