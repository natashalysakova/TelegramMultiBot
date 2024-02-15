using Newtonsoft.Json;
using System.Web;
using TelegramMultiBot.Configuration;

namespace TelegramMultiBot.ImageGenerators.ComfyUI
{
    class ComfyUI : IDiffusor
    {
        public string UI => throw new NotImplementedException();

        public bool isAvailable()
        {
            return false;
        }

        public Task<ImageJob> Run(ImageJob job)
        {
            throw new NotImplementedException();
        }
    }

    class ComfyUiHttpClient
    {
        private readonly HttpClient _httpClient;
        private string propmt = "/prompt";
        private string view = "/view";
        private string history = "/history";


        public ComfyUiHttpClient(HostSettings host)
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = host.Uri;
        }

        public async Task<GetStatusResponce> GetStatus()
        {
            using (_httpClient)
            {
                var response = await _httpClient.GetAsync(propmt);
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GetStatusResponce>(content);
            }
        }

        public async Task<byte[]> GetImage(string filename, string subfolder, string type)
        {
            using (_httpClient)
            {
                var query = HttpUtility.ParseQueryString(view);
                query["filename"] = filename;
                query["type"] = type;
                query["subfolder"] = subfolder;

                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, query.ToString());
                var response = await _httpClient.SendAsync(message);
                return await response.Content.ReadAsByteArrayAsync();
            }
        }

        public async Task<StartJobResponse> StartJob(string workflow)
        {
            using (_httpClient)
            {
                HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, propmt);
                message.Content = new StringContent(workflow);

                var response = await _httpClient.SendAsync(message);
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<StartJobResponse>(content);
            }
        }

    }


    public class GetHistoryResponse
    {
        public Dictionary<string, JobItem> Items { get; set; }
    }

    public class JobItem
    {
        public object[] prompt { get; set; }
        public Output outputs { get; set; }
        public Status status { get; set; }
    }

    public class Output
    {
        public Dictionary<string, OutputItem> Items { get; set; }
    }

    public class OutputItem
    {
        public Image[] images { get; set; }
    }

    public class Image
    {
        public string filename { get; set; }
        public string subfolder { get; set; }
        public string type { get; set; }
    }

    public class Status
    {
        public string status_str { get; set; }
        public bool completed { get; set; }
        public object[][] messages { get; set; }
    }






    public class StartJobResponse
    {
        public string prompt_id { get; set; }
        public int number { get; set; }
        public Node_Errors node_errors { get; set; }
    }

    public class Node_Errors
    {
    }


    public class GetStatusResponce
    {
        public Exec_Info exec_info { get; set; }
    }

    public class Exec_Info
    {
        public int queue_remaining { get; set; }
    }

}
