using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.ImageGeneration.Exceptions;

namespace TelegramMultiBot.ImageGenerators.ComfyUI
{
    internal class ComfyUI : Diffusor
    {
        private readonly ComfyUISettings _settings;
        private readonly ImageGeneationSettings _generalSettings;

        protected override string PingPath => "/system_stats";

        private readonly string _clientId = Guid.NewGuid().ToString();
        private readonly ILogger<ComfyUI> _logger;
        private readonly IDatabaseService _databaseService;
        private readonly TelegramClientWrapper _client;

        public ComfyUI(ILogger<ComfyUI> logger, IConfiguration configuration, IDatabaseService databaseService, TelegramClientWrapper client) : base(logger, configuration)
        {
            _logger = logger;
            _databaseService = databaseService;
            _client = client;

            _settings = configuration.GetSection(ComfyUISettings.Name).Get<ComfyUISettings>() ?? throw new InvalidOperationException(nameof(_settings));
            _generalSettings = configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>() ?? throw new InvalidOperationException(nameof(_settings));
        }

        public override string UI { get => nameof(ComfyUI); }

        public override bool CanHandle(JobType type)
        {
            return type switch
            {
                JobType.HiresFix or JobType.Text2Image or JobType.Vingette or JobType.Noise => true,
                _ => false,
            };
        }

        public override async Task<JobInfo> Run(JobInfo job)
        {
            _logger.LogTrace("{uri}", ActiveHost!.Uri);

            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Завдання розпочато");

            var baseDir = _generalSettings.BaseOutputDirectory;
            var outputDir = _settings.OutputDirectory;
            var directory = Path.Combine(baseDir, outputDir, DateTime.Today.ToString("yyyyMMdd"));

            if (!Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
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

                    case JobType.HiresFix:
                        await RunHiresFix(job, directory);
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

        private async Task RunHiresFix(JobInfo job, string directory)
        {

            var payload = File.ReadAllText(Path.Combine(_settings.PayloadPath, "hiresFix.json"));
            var previousJob = _databaseService.GetJobResult(job.PreviousJobResultId!)!;
            var upscaleParams = new UpscaleParams(previousJob);

            var json = JObject.Parse(payload);
            var modelLoaderNodeName = FindNodeNameByClassType(json, "CheckpointLoaderSimple");
            var imageLoadNodeName = FindNodeNameByClassType(json, "LoadImage");
            var upscalerModelNodeName = FindNodeNameByClassType(json, "LoadImage");
            var positivePromptNode = FindNodeNameByMeta(json, "CLIPTextEncode", "positive");
            var negativePromptNode = FindNodeNameByMeta(json, "CLIPTextEncode", "negative");
            var clipSkipNodeName = FindNodeNameByClassType(json, "CLIPSetLastLayer");
            var upscaleNodeName = FindNodeNameByClassType(json, "UltimateSDUpscale");
            var upscaleModelNodeName = FindNodeNameByClassType(json, "UpscaleModelLoader");
            string outputNode = FindNodeNameByClassType(json, "PreviewImage");

            ModelSettings model;
            try
            {
                model = _generalSettings.Models.Single(x => x.Path.Contains(upscaleParams.Model));
            }
            catch (Exception)
            {
                throw new InputException("Невідома модель: " + upscaleParams.Model);
            }

            json[modelLoaderNodeName]!["inputs"]!["ckpt_name"] = model.Path;

            var genNode = json[upscalerModelNodeName]!["inputs"]!;
            genNode["seed"] = upscaleParams.Seed;
            genNode["steps"] = model.Steps;
            genNode["cfg"] = model.CGF;
            genNode["sampler_name"] = model.Sampler;
            genNode["scheduler"] = model.Scheduler;
            genNode["denoise"] = _generalSettings.HiresFixDenoise;
            genNode["upscale_by"] = _generalSettings.UpscaleMultiplier;

            json[imageLoadNodeName]!["inputs"]!["image"] = Path.GetFileName(previousJob.FilePath);

            var dest = Path.Combine(_settings.InputDirectory, Path.GetFileName(previousJob.FilePath));

            if (!File.Exists(dest))
            {
                File.Copy(previousJob.FilePath, dest, true);
            }

            json[upscaleModelNodeName]!["inputs"]!["model_name"] = _generalSettings.UpscaleModel;

            json[clipSkipNodeName]!["inputs"]!["stop_at_clip_layer"] = -model.CLIPskip;

            json[positivePromptNode]!["inputs"]!["text"] = upscaleParams.Prompt;
            json[negativePromptNode]!["inputs"]!["text"] = upscaleParams.NegativePrompt;

            var info = GetInfos(job, _settings);
            int tiles = (int)(Math.Ceiling(upscaleParams.Width * _generalSettings.UpscaleMultiplier / 512.0) * Math.Ceiling(upscaleParams.Height * _generalSettings.UpscaleMultiplier / 512.0)) + 1;
            await StartAndMonitorJob(job, directory, [json], [info], outputNode, tiles);

            File.Delete(dest);
        }

        private async Task RunTextToImage(JobInfo job, string directory)
        {

            var payload = File.ReadAllText(Path.Combine(_settings.PayloadPath, "text2image.json"));

            var genParams = new GenerationParams(job);
            if (genParams.Seed == -1)
            {
                genParams.Seed = new Random().NextInt64();
            }
            if (genParams.BatchCount == 0)
            {
                genParams.BatchCount = _generalSettings.BatchCount;
            }
            if (string.IsNullOrEmpty(genParams.Model))
            {
                genParams.Model = _generalSettings.DefaultModel;
            }

            ModelSettings model;
            try
            {
                model = _generalSettings.Models.Single(x => x.Name == genParams.Model);
            }
            catch (Exception)
            {
                throw new InputException("Невідома модель: " + genParams.Model);
            }

            List<JObject> jsons = [];

            var json = JObject.Parse(payload);

            var modelLoaderNodeName = FindNodeNameByClassType(json, "CheckpointLoaderSimple");
            var genNodeName = FindNodeNameByClassType(json, "KSampler");
            var latentNodeName = FindNodeNameByClassType(json, "EmptyLatentImage");
            var positivePromptNode = FindNodeNameByMeta(json, "CLIPTextEncode", "positive");
            var negativePromptNode = FindNodeNameByMeta(json, "CLIPTextEncode", "negative");
            var clipSkipNodeName = FindNodeNameByClassType(json, "CLIPSetLastLayer");
            string outputNode = FindNodeNameByClassType(json, "SaveImage");

            for (int i = 0; i < genParams.BatchCount; i++)
            {
                json = JObject.Parse(payload);

                json[modelLoaderNodeName]!["inputs"]!["ckpt_name"] = model.Path;

                var genNode = json[genNodeName]!["inputs"]!;
                genNode["seed"] = genParams.Seed + i;
                genNode["steps"] = model.Steps;
                genNode["cfg"] = model.CGF;
                genNode["sampler_name"] = model.Sampler;
                genNode["scheduler"] = model.Scheduler;

                var latentNode = json[latentNodeName]!["inputs"]!;
                latentNode["width"] = genParams.Width;
                latentNode["height"] = genParams.Height;
                latentNode["batch_size"] = 1;

                json[clipSkipNodeName]!["inputs"]!["stop_at_clip_layer"] = -model.CLIPskip;

                json[positivePromptNode]!["inputs"]!["text"] = genParams.Prompt;
                json[negativePromptNode]!["inputs"]!["text"] = genParams.NegativePrompt;

                jsons.Add(json);
            }

            await StartAndMonitorJob(job, directory, jsons, GetInfos(genParams, model, json.Count), outputNode, model.Steps);
        }

        private static IEnumerable<string> GetInfos(GenerationParams genParams, ModelSettings model, int count)
        {
            for (int i = 0; i < count; i++)
            {
                StringBuilder builder = new();

                //big pink kids cake with five candles and flowers
                //Steps: 5, Sampler: DPM++ SDE Karras, CFG scale: 2.5, Seed: 552689683, Size: 1024x1024, Model hash: 4726d3bab1, Model: dreamshaperXL_v2TurboDpmppSDE, Version: v1.7.0

                _ = builder.AppendLine(genParams.Prompt);
                if (!string.IsNullOrEmpty(genParams.NegativePrompt))
                {
                    _ = builder.Append("Negative: " + genParams.NegativePrompt + ", ");
                }

                var samplerAlias = "k_" + model.Sampler;
                if (model.Scheduler == "karras")
                {
                    samplerAlias += "_ka";
                }
                if (model.Scheduler == "exponential")
                {
                    samplerAlias += "_exp";
                }

                _ = builder.Append("Steps: " + model.Steps + ", ");
                _ = builder.Append("Sampler: " + samplerAlias + ", ");
                _ = builder.Append("CFG scale: " + model.CGF.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ", ");
                _ = builder.Append("Seed: " + (genParams.Seed + i) + ", ");
                _ = builder.Append("Size: " + genParams.Width + "x" + genParams.Height + ", ");
                _ = builder.Append("Model: " + Path.GetFileNameWithoutExtension(model.Path));

                yield return builder.ToString();
            }
        }

        private static string GetInfos(JobInfo job, ComfyUISettings settings)
        {
            if (job.Type == JobType.Vingette)
            {
                return "ProPostFilmGrain: " + settings.NoiseStrength;
            }

            if (job.Type == JobType.Noise)
            {
                return "ProPostVignette: " + settings.VegnietteIntensity;
            }

            return string.Empty;
        }

        private static string FindNodeNameByClassType(JObject obj, string classType)
        {
            var tokens = obj.Children().SelectMany(x => x.Children());

            foreach (var item in tokens)
            {
                var value = item["class_type"]!.Value<string>();

                if (value == classType)
                {
                    return ((JProperty)item.Parent!).Name;
                }
            }

            throw new Exception("Token not found");
        }

        private static string FindNodeNameByMeta(JObject obj, string classType, string metaTitle)
        {
            var tokens = obj.Children().SelectMany(x => x.Children());

            foreach (var item in tokens)
            {
                var type = item["class_type"]!.Value<string>();
                var title = item["_meta"]!["title"]!.Value<string>();

                if (type == classType && title == metaTitle)
                {
                    return ((JProperty)item.Parent!).Name;
                }
            }

            throw new Exception("Token not found");
        }

        private async Task RunNoiseVingetteWorkflow(JobInfo job, string directory)
        {
            if (job.PreviousJobResultId is null)
                throw new NullReferenceException(nameof(job.PreviousJobResultId));

            var jobResultInfo = _databaseService.GetJobResult(job.PreviousJobResultId) ?? throw new NullReferenceException("jobResultInfo");

            string payload;
            if (job.Type == JobType.Noise)
            {
                payload = File.ReadAllText(Path.Combine(_settings.PayloadPath, "noise.json"));
            }
            else if (job.Type == JobType.Vingette)
            {
                payload = File.ReadAllText(Path.Combine(_settings.PayloadPath, "vignette.json"));
            }
            else
            {
                throw new Exception("unsupported job");
            }

            JObject json = JObject.Parse(payload);

            if (job.Type == JobType.Noise)
            {
                var nosieNode = FindNodeNameByClassType(json, "ProPostFilmGrain");
                json[nosieNode]!["inputs"]!["grain_power"] = _settings.NoiseStrength;
            }
            else
            {
                var vingetteNode = FindNodeNameByClassType(json, "ProPostVignette");
                json[vingetteNode]!["inputs"]!["intensity"] = _settings.VegnietteIntensity;
            }

            var inputImageNode = FindNodeNameByClassType(json, "LoadImage");
            json[inputImageNode]!["inputs"]!["image"] = Path.GetFileName(jobResultInfo.FilePath);

            string outputNode = FindNodeNameByClassType(json, "SaveImage");

            var dest = Path.Combine(_settings.InputDirectory, Path.GetFileName(jobResultInfo.FilePath));

            if (!File.Exists(dest))
            {
                File.Copy(jobResultInfo.FilePath, dest, true);
            }

            var info = GetInfos(job, _settings);
            await StartAndMonitorJob(job, directory, [json], [info], outputNode, 1);

            File.Delete(dest);
        }

        private async Task StartAndMonitorJob(JobInfo job, string directory, IEnumerable<JObject> jsons, IEnumerable<string> infos, string outputNode, int maxTiles)
        {
            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"[{ActiveHost!.UI}] {job.Type} - Працюю... 1/{jsons.Count()} Прогресс: 0%");

            using var comfyClient = new ComfyUiClient(ActiveHost);
            for (int i = 0; i < jsons.Count(); i++)
            {
                var webcocketClient = new ClientWebSocket();
                var uri = new Uri($"ws://{ActiveHost.Host}:{ActiveHost.Port}/ws?clientId={_clientId}");
                await webcocketClient.ConnectAsync(uri, CancellationToken.None);
                _logger.LogDebug("{client} - {state}", webcocketClient, webcocketClient.State);

                var json = jsons.ElementAt(i);
                var info = infos.ElementAt(i);

                var req = JsonConvert.SerializeObject(new { prompt = json, client_id = _clientId });
                _logger.LogTrace("requestJson: {json}", req);

                Stopwatch s = new();
                s.Start();
                var response = await comfyClient.StartJob(req) ?? throw new HttpRequestException("Invalid request to comfyUI");
                var jobId = response.prompt_id;
                var buffer = new byte[1024];
                var tile = 0;
                var fraction = 100.0 / jsons.Count() / maxTiles;
                while (true)
                {
                    var res = await webcocketClient.ReceiveAsync(buffer, CancellationToken.None);

                    if (res.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogDebug("{client} - {state}", webcocketClient, webcocketClient.State);
                        break;
                    }

                    var data = Encoding.UTF8.GetString(buffer, 0, res.Count);
                    _logger.LogDebug("{data}", data);
                    var obj = JsonConvert.DeserializeObject<WebsocketResponce>(data) ?? throw new InvalidOperationException("Cannot deserialize progress");

                    if (obj.type == "progress")
                    {
                        if (obj.data.value == obj.data.max)
                        {
                            tile += 1;
                            var progress = (i * fraction * maxTiles) + tile * fraction;

                            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"[{ActiveHost.UI}] {job.Type} - Працюю... {i + 1}/{jsons.Count()} Прогресс: {Math.Round(progress, 2)}%");
                            _databaseService.PostProgress(job.Id, progress, "working");
                        }
                    }

                    if (obj.type == "executing" && obj.data.node == null && obj.data.prompt_id == jobId)
                    {
                        _logger.LogDebug("{jobId} - finished", jobId);
                        break;
                    }
                    await Task.Delay(500);
                }
                s.Stop();

                await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"Завантажую результат {i + 1}/{jsons.Count()}");

                var str = await comfyClient.GetHistory(jobId);

                var history = JObject.Parse(str);

                var imageArray = history[jobId]!["outputs"]![outputNode]!["images"]!;

                foreach (var item in imageArray)
                {
                    var filename = item["filename"]!.ToString();
                    var subfolder = item["subfolder"]!.ToString();
                    var type = item["type"]!.ToString();

                    var img = await comfyClient.GetImage(filename, subfolder, type);

                    var fileName = $"{DateTime.Now:yyyyMMddhhmmssfff}_{job.Type}.png";
                    var filePath = Path.Combine(directory, fileName);

                    File.WriteAllBytes(filePath, img);
                    //File.WriteAllText(filePath + ".txt", info.infotexts[j]);

                    //inputMedia.Add(filePath);

                    _databaseService.AddResult(job.Id, new JobResultInfoCreate()
                    {
                        FilePath = filePath,
                        Info = info,
                        RenderTime = s.Elapsed.TotalMilliseconds
                    });
                }
            }
        }

        public override bool ValidateConnection(string content)
        {
            return !string.IsNullOrEmpty(content);
        }
    }

    internal class ComfyUiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _propmt = "/prompt";
        private readonly string _view = "/view";
        private readonly string _history = "/history";
        private readonly HostSettings _host;

        public ComfyUiClient(HostSettings host)
        {
            _host = host;

            _httpClient = new HttpClient
            {
                BaseAddress = _host.Uri
            };
        }

        public async Task<GetStatusResponce> GetStatus()
        {
            using (_httpClient)
            {
                var response = await _httpClient.GetAsync(_propmt);
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GetStatusResponce>(content) ?? throw new InvalidOperationException("Cannot deserialize content");
            }
        }

        public async Task<byte[]> GetImage(string filename, string subfolder, string type)
        {
            HttpRequestMessage message = new(HttpMethod.Get, $"{_view}?filename={filename}&type={type}&subfolder={subfolder}");
            var response = await _httpClient.SendAsync(message);
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<StartJobResponse> StartJob(string workflow)
        {
            HttpRequestMessage message = new(HttpMethod.Post, _propmt)
            {
                Content = new StringContent(workflow)
            };

            var response = await _httpClient.SendAsync(message);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<StartJobResponse>(content) ?? throw new InvalidOperationException("Cannot deserialize content");
            }
            throw new Exception(response.ReasonPhrase);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        public async Task<string> GetHistory(string jobId)
        {
            var paht = $"{_history}/{jobId}";

            HttpRequestMessage message = new(HttpMethod.Get, paht);
            var response = await _httpClient.SendAsync(message);
            var str = await response.Content.ReadAsStringAsync();
            return str;
        }
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE1006 // Naming Styles

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