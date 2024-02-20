using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Web;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using Microsoft.Extensions.Configuration;
using TelegramMultiBot.Database.Interfaces;
using Newtonsoft.Json.Linq;
using System.IO;
using TelegramMultiBot.ImageGeneration;
using System.Net.WebSockets;
using System.Net.Http.Json;
using System;
using System.Text;
using Telegram.Bot;
using System.Diagnostics;
using System.Collections.Specialized;
using TelegramMultiBot.ImageGeneration.Exceptions;

namespace TelegramMultiBot.ImageGenerators.ComfyUI
{
    class ComfyUI : IDiffusor
    {
        private readonly ILogger<ComfyUI> _logger;
        private readonly IConfiguration _configuration;
        private readonly IDatabaseService _databaseService;
        private readonly TelegramBotClient _client;
        private string pingPath = "/system_stats";
        private string promptPath = "/prompt";
        private HostSettings activeHost;
        private string clientId = Guid.NewGuid().ToString();

        public ComfyUI(ILogger<ComfyUI> logger, IConfiguration configuration, IDatabaseService databaseService, TelegramBotClient client)
        {
            _logger = logger;
            _configuration = configuration;
            _databaseService = databaseService;
            _client = client;
        }

        public string UI { get => nameof(ComfyUI); }

        public bool CanHnadle(JobType type)
        {
            switch (type)
            {
                case JobType.HiresFix:
                case JobType.Upscale:
                    return false;
                case JobType.Text2Image:
                case JobType.Vingette:
                case JobType.Noise:
                    return true;
                default: return false;
            }

        }

        public bool isAvailable()
        {
            var hosts = _configuration.GetSection("Hosts").Get<IEnumerable<HostSettings>>().Where(x => x.UI == nameof(ComfyUI));
            foreach (var host in hosts)
            {
                if (!host.Enabled)
                {
                    _logger.LogTrace($"{host.Uri} disabled");
                    continue;
                }
                var httpClient = new HttpClient();
                httpClient.BaseAddress = host.Uri;
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                try
                {
                    var resp = httpClient.GetAsync(pingPath);
                    resp.Wait();

                    activeHost = host;
                    return true;

                }
                catch (Exception)
                {
                    _logger.LogTrace($"{host.Uri} not available");
                }
            }
            return false;
        }


        public async Task<JobInfo> Run(JobInfo job)
        {
            _logger.LogTrace(activeHost.Uri.ToString());

            _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Job started");

            var baseDir = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().BaseOutputDirectory;
            var comfySettings = _configuration.GetSection(ComfyUISettings.Name).Get<ComfyUISettings>();
            var outputDir = comfySettings.OutputDirectory;
            var directory = Path.Combine(baseDir, outputDir, DateTime.Today.ToString("yyyyMMdd"));

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                switch (job.Type)
                {
                    case JobType.Vingette:
                    case JobType.Noise:
                        await RunNoiseVingetteWorkflow(job, directory);
                        break;
                    case JobType.Text2Image:
                        await RunTextToImage(job, directory);
                        break;
                    default:
                        break;
                }
                _databaseService.PostProgress(job.Id, 100, "ok");
                await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Готово. Прогрес 100%. Збираю та відправляю зображення");

            }
            catch (Exception ex)
            {
                _databaseService.PostProgress(job.Id, -1, ex.Message);
                throw;
            }

            return _databaseService.GetJob(job.Id);
        }

        private async Task RunTextToImage(JobInfo job, string directory)
        {
            var settings = _configuration.GetSection(ComfyUISettings.Name).Get<ComfyUISettings>();
            var generalSettings = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>();

            var payload = File.ReadAllText(Path.Combine(settings.PayloadPath, "text2image.json"));

            var json = JObject.Parse(payload);

            var genParams = new GenerationParams(job);
            ModelSettings model;
            try
            {
                model = generalSettings.Models.Single(x => x.Name == genParams.Model);
            }
            catch (Exception)
            {
                throw new InputException("Невідома модель: " + genParams.Model);
            }

            var modelLoaderNodeName = FindNodeNameByClassType(json, "CheckpointLoaderSimple");
            json[modelLoaderNodeName]["inputs"]["ckpt_name"] = model.Path;


            var genNodeName = FindNodeNameByClassType(json, "KSampler");
            var genNode = json[genNodeName]["inputs"];

            if(genParams.Seed == -1)
            {
                genParams.Seed = new Random().NextInt64();
            }

            genNode["seed"] = genParams.Seed;
            genNode["steps"] = model.Steps;
            genNode["cfg"] = model.CGF;
            genNode["sampler_name"] = model.Sampler;
            genNode["scheduler"] = model.Scheduler;

            var latentNodeName = FindNodeNameByClassType(json, "EmptyLatentImage");
            var latentNode = json[latentNodeName]["inputs"];
            latentNode["width"] = genParams.Width;
            latentNode["height"] = genParams.Height;
            latentNode["batch_size"] = 1;


            var positivePromptNode = FindNodeNameByMeta(json, "CLIPTextEncode", "positive");
            json[positivePromptNode]["inputs"]["text"] = genParams.Prompt;


            var negativePromptNode = FindNodeNameByMeta(json, "CLIPTextEncode", "negative");
            json[negativePromptNode]["inputs"]["text"] = genParams.NegativePrompt;


            var requ = new { prompt = json, client_id = clientId };

            string outputNode = FindNodeNameByClassType(json, "SaveImage");

            await StartAndMonitorJob(job, directory, JsonConvert.SerializeObject(requ), outputNode);
        }

        private static string FindNodeNameByClassType(JObject obj, string classType)
        {
            var tokens = obj.Children().SelectMany(x => x.Children());

            foreach (var item in tokens)
            {
                var value = item["class_type"].Value<string>();

                if (value == classType)
                {
                    return ((JProperty)item.Parent).Name;
                }
            }

            throw new Exception("Token not found");
        }

        private static string FindNodeNameByMeta(JObject obj, string classType, string metaTitle)
        {
            var tokens = obj.Children().SelectMany(x => x.Children());

            foreach (var item in tokens)
            {
                var type = item["class_type"].Value<string>();
                var title  = item["_meta"]["title"].Value<string>();

                if (type == classType && title == metaTitle)
                {
                    return ((JProperty)item.Parent).Name;
                }
            }

            throw new Exception("Token not found");
        }

        private async Task RunNoiseVingetteWorkflow(JobInfo job, string directory)
        {
            var jobResultInfo = _databaseService.GetJobResult(job.PreviousJobResultId);

            var settings = _configuration.GetSection(ComfyUISettings.Name).Get<ComfyUISettings>();
            string payload = string.Empty;

            if(job.Type == JobType.Noise)
            {
                payload = File.ReadAllText(Path.Combine(settings.PayloadPath, "noise.json"));
            }else if(job.Type == JobType.Vingette)
            {
                payload = File.ReadAllText(Path.Combine(settings.PayloadPath, "vignette.json"));
            }
            else
            {
                throw new Exception("unsupported job");
            }
            
            JObject json = JObject.Parse(payload);

            if (job.Type == JobType.Noise)
            {
                var nosieNode = FindNodeNameByClassType(json, "ProPostFilmGrain");
                json[nosieNode]["inputs"]["grain_power"] = 0.3;
            }
            else
            {
                var vingetteNode = FindNodeNameByClassType(json, "ProPostVignette");
                json[vingetteNode]["inputs"]["intensity"] = 0.3;
            }


            var inputImageNode = FindNodeNameByClassType(json, "LoadImage");
            json[inputImageNode]["inputs"]["image"] = Path.GetFileName(jobResultInfo.FilePath);

            string outputNode = FindNodeNameByClassType(json, "SaveImage");

            var requ = new { prompt = json, client_id = clientId };

            var dest = $"\\\\{activeHost.Host}\\{settings.InputDirectory}\\{Path.GetFileName(jobResultInfo.FilePath)}";

            if (!File.Exists(dest))
            {
                File.Copy(jobResultInfo.FilePath, dest, true);
            }


            await StartAndMonitorJob(job, directory, JsonConvert.SerializeObject(requ),outputNode);

            File.Delete(dest);
        }

        private async Task StartAndMonitorJob(JobInfo job, string directory, string jsonPayload, string outputNode)
        {
            using (var client = new ComfyUiClient(activeHost))
            {
                Stopwatch s = new Stopwatch();
                s.Start();

                var response = await client.StartJob(jsonPayload);
                var buffer = new byte[1024 * 12];

                if (response is null)
                {
                    throw new HttpRequestException("Invalid request to comfyUI");
                }


                var jobId = response.prompt_id;
                var webcocketClient = new ClientWebSocket();
                var uri = new Uri($"ws://{activeHost.Host}:{activeHost.Port}/ws?clientId={clientId}");
                await webcocketClient.ConnectAsync(uri, CancellationToken.None);
                _logger.LogDebug($"{webcocketClient} - {webcocketClient.State}");

                await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Render started");

                var oldProgress = 0;

                while (true)
                {
                    var res = await webcocketClient.ReceiveAsync(buffer, CancellationToken.None);

                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogDebug($"{webcocketClient} - {webcocketClient.State}");
                        break;
                    }

                    var data = Encoding.UTF8.GetString(buffer, 0, res.Count);
                    await Console.Out.WriteLineAsync(data);

                    var obj = JsonConvert.DeserializeObject<WebsocketResponce>(data);

                    if(obj.type == "progress")
                    {
                        var progress = 100 / obj.data.max * (obj.data.value - 1);
                        if(progress != oldProgress)
                        {
                            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"Working... {progress}%");
                            oldProgress = progress;
                        }
                    }


                    if (obj.type == "executing" && obj.data.node == null && obj.data.prompt_id == jobId)
                    {
                        _logger.LogDebug($"{jobId} - finished");
                        break;
                    }
                }
                s.Stop();
                await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Render finished");

                var str = await client.GetHistory(jobId);

                var history = JObject.Parse(str);

                var imageArray = history[jobId]["outputs"][outputNode]["images"];

                foreach (var item in imageArray)
                {

                    var filename = item["filename"].ToString();
                    var subfolder = item["subfolder"].ToString();
                    var type = item["type"].ToString();

                    var img = await client.GetImage(filename, subfolder, type);

                    var fileName = $"{DateTime.Now.ToString("yyyyMMddhhmmssfff")}_{job.Type}.png";
                    var filePath = Path.Combine(directory, fileName);

                    File.WriteAllBytes(filePath, img);
                    //File.WriteAllText(filePath + ".txt", info.infotexts[j]);

                    //inputMedia.Add(filePath);

                    _databaseService.AddResult(job.Id, new JobResultInfo()
                    {
                        FilePath = filePath,
                        Info = string.Empty,
                        RenderTime = s.Elapsed.Milliseconds
                    });
                }
                await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Files saved");
            }
        }
    }

    class ComfyUiClient : IDisposable
    {
        HttpClient _httpClient;
        private string propmt = "/prompt";
        private string view = "/view";
        private string history = "/history";
        private readonly HostSettings _host;
        private readonly ILogger<ComfyUiClient> _logger;

        public ComfyUiClient(HostSettings host)
        {
            _host = host;


            _httpClient = new HttpClient();
            _httpClient.BaseAddress = _host.Uri;
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
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, $"{view}?filename={filename}&type={type}&subfolder={subfolder}");
            var response = await _httpClient.SendAsync(message);
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<StartJobResponse> StartJob(string workflow)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, propmt);
            message.Content = new StringContent(workflow);

            var response = await _httpClient.SendAsync(message);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<StartJobResponse>(content);
            }
            return null;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        public async Task<string> GetHistory(string jobId)
        {
            var paht = $"{history}/{jobId}";

            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, paht);
            var response = await _httpClient.SendAsync(message);
            var str = await response.Content.ReadAsStringAsync();
            return str;
        }
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











    public class WebsocketResponce
    {
        public string type { get; set; }
        public Data data { get; set; }
    }

    public class Data
    {
        public string node;
        public string prompt_id;
        public int value { get; set; }
        public int max { get; set; }

        public string[] nodes { get; set; }

        public Status status { get; set; }
        public string sid { get; set; }
        public Output output { get; set; }
    }

}
