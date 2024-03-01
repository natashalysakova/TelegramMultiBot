
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.ImageGeneration.Exceptions;
using TelegramMultiBot.ImageGenerators.Automatic1111.Api;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    class Automatic1111 : Diffusor
    {
        private readonly TelegramBotClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<Automatic1111> _logger;
        private readonly IDatabaseService _databaseService;
        // private readonly IServiceProvider _serviceProvider;
        const string text2imagePath = "/sdapi/v1/txt2img";
        const string img2imgPath = "/sdapi/v1/img2img";
        const string extrasPath = "/sdapi/v1/extra-single-image";

        const string progressPath = "/sdapi/v1/progress?skip_current_image=true";
        protected override string pingPath => "/internal/sysinfo";
        public Automatic1111(TelegramBotClient client, IConfiguration configuration, ILogger<Automatic1111> logger, IDatabaseService databaseService) : base(logger, configuration)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
            _databaseService = databaseService;
            //_serviceProvider = serviceProvider;
        }

        public override string UI { get => nameof(Automatic1111); }

        //public bool isAvailable()
        //{
        //    var hosts = _configuration.GetSection("Hosts").Get<IEnumerable<HostSettings>>().Where(x => x.UI == nameof(Automatic1111));
        //    foreach (var host in hosts)
        //    {
        //        if (!host.Enabled)
        //        {
        //            _logger.LogTrace($"{host.Uri} disabled");
        //            continue;
        //        }

        //        var httpClient = new HttpClient();
        //        httpClient.BaseAddress = host.Uri;
        //        httpClient.Timeout = TimeSpan.FromSeconds(10);
        //        try
        //        {
        //            var resp = httpClient.GetAsync(pingPath);
        //            resp.Wait();

        //            if (resp.Result.Content.ReadAsStringAsync().Result.Contains("--api"))
        //            {
        //                activeHost = host;
        //                return true;
        //            }
        //            else
        //            {
        //                _logger.LogTrace($"{host.Uri} api disabled");
        //            }
        //        }
        //        catch (Exception)
        //        {
        //            _logger.LogTrace($"{host.Uri} not available");
        //        }
        //    }
        //    return false;
        //}

        public override bool ValidateConnection(string content)
        {
            return content.Contains("--api");
        }

        public override async Task<JobInfo> Run(JobInfo job)
        {
            _logger.LogTrace(ActiveHost.Uri.ToString());
            _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Завдання розпочато");

            Automatic1111Cache.InitCache(ActiveHost.Uri);

            var baseDir = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().BaseOutputDirectory;
            var outputDir = _configuration.GetSection(Automatic1111Settings.Name).Get<Automatic1111Settings>().OutputDirectory;
            var directory = Path.Combine(baseDir, outputDir, DateTime.Today.ToString("yyyyMMdd"));

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                switch (job.Type)
                {
                    case JobType.Text2Image:
                        await TextToImage(job, directory);
                        break;
                    case JobType.Upscale:
                        await PostProcessingUpscale(job, directory);
                        break;
                    case JobType.HiresFix:
                        await Img2ImgUpscale(job, directory);
                        break;
                }
                _databaseService.PostProgress(job.Id, 100, "ok");
            }
            catch (Exception ex)
            {
                _databaseService.PostProgress(job.Id, -1, ex.Message);
                throw;
            }


            return _databaseService.GetJob(job.Id);
        }

        private async Task<JobInfo?> PostProcessingUpscale(JobInfo job, string directory)
        {
            var jobResultInfo = _databaseService.GetJobResult(job.PreviousJobResultId);
            var upscaleparams = new UpscaleParams(jobResultInfo);

            var settings = _configuration.GetSection(Automatic1111Settings.Name).Get<Automatic1111Settings>();
            var payload = File.ReadAllText(Path.Combine(settings.UpscalePath, "extras-single.json"));

            JObject json = JObject.Parse(payload);
            json["image"] = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(upscaleparams.FilePath));
            json["upscaling_resize"] = job.UpscaleModifyer;
            var jsonPayload = json.ToString();

            await StartAndMonitorJob(job, directory, extrasPath, jsonPayload);

            return job;
        }

        private async Task<JobInfo> Img2ImgUpscale(JobInfo job, string directory)
        {
            var previousResult = _databaseService.GetJobResult(job.PreviousJobResultId);
            var upscaleparams = new UpscaleParams(previousResult);

            var settings = _configuration.GetSection(Automatic1111Settings.Name).Get<Automatic1111Settings>();
            var generalSettings = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>();

            var payload = File.ReadAllText(Path.Combine(settings.UpscalePath, "img2img.json"));

            JObject json = JObject.Parse(payload);
            json["prompt"] = upscaleparams.Prompt;
            if (upscaleparams.NegativePrompt != null)
            {
                json["negative_prompt"] = upscaleparams.NegativePrompt;
            }
            json["width"] = upscaleparams.Width * generalSettings.UpscaleMultiplier;
            json["height"] = upscaleparams.Height * generalSettings.UpscaleMultiplier;

            var samplers = Automatic1111Cache.GetSampler(ActiveHost.Uri);
            var sampler = samplers.FirstOrDefault(x => x.name == upscaleparams.Sampler);
           
            json["sampler_name"] = samplers.Where(x => x.aliases.Contains(upscaleparams.Sampler) || x.name == upscaleparams.Sampler).FirstOrDefault().name;
            json["steps"] = upscaleparams.Steps;
            json["cfg_scale"] = upscaleparams.CFGScale;
            json["override_settings"]["sd_model_checkpoint"] = upscaleparams.Model;


            //if (Automatic1111Cache.CheckpointsInfo == null)
            //{
            //    using (var client = new HttpClient() { BaseAddress = activeHost.Uri })
            //    {
            //        var response = await client.GetAsync(checkpointsInfo);
            //        if (response.IsSuccessStatusCode)
            //        {
            //            Automatic1111Cache.CheckpointsInfo = JsonConvert.DeserializeObject<IEnumerable<CheckpointsInfo>>(await response.Content.ReadAsStringAsync());
            //        }
            //    }
            //}

            var model = generalSettings.Models.Single(x => Path.GetFileNameWithoutExtension(x.Path) == upscaleparams.Model);


            json["override_settings"]["CLIP_stop_at_last_layers"] = model.CLIPskip;
            json["init_images"] = new JArray(){
                    "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(upscaleparams.FilePath))
                };
            var jsonPayload = json.ToString();

            await StartAndMonitorJob(job, directory, img2imgPath, jsonPayload);

            return job;
        }
        private async Task TextToImage(JobInfo job, string directory)
        {
            var genParams = new GenerationParams(job);
            var settings = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>();

            var batchCount = settings.BatchCount;

            if (string.IsNullOrEmpty(genParams.Model))
            {
                genParams.Model = settings.DefaultModel;
            }

            if (genParams.BatchCount != 0)
            {
                batchCount = genParams.BatchCount;
            }

            var automaticSettings = _configuration.GetSection(Automatic1111Settings.Name).Get<Automatic1111Settings>();


            string payload = File.ReadAllText(Path.Combine(automaticSettings.PayloadPath, "sd-payload-sdxl.json"));




            JObject json = JObject.Parse(payload);
            json["width"] = genParams.Width;
            json["height"] = genParams.Height;
            json["prompt"] = genParams.Prompt;
            json["negative_prompt"] = genParams.NegativePrompt;
            json["seed"] = genParams.Seed;

            var modelSettings = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>();
            ModelSettings model;
            try
            {
                model = modelSettings.Models.Single(x => x.Name == genParams.Model);
            }
            catch (Exception)
            {
                throw new InputException("Невідома модель: " + genParams.Model);
            }

            json["override_settings"]["sd_model_checkpoint"] = model.Path;
            json["override_settings"]["CLIP_stop_at_last_layers"] = model.CLIPskip;
            json["steps"] = model.Steps;
            json["cfg_scale"] = model.CGF;
            var samplerAlias = "k_" + model.Sampler;
            if (model.Scheduler == "karras")
            {
                samplerAlias += "_ka";
            }
            if (model.Scheduler == "exponential")
            {
                samplerAlias += "_exp";
            }

            var samplers = Automatic1111Cache.GetSampler(ActiveHost.Uri);

            var samplerName = samplers.Where(x => x.aliases.Contains(samplerAlias)).FirstOrDefault().name;

            json["sampler_name"] = samplerName;


            var jsonPayload = json.ToString();
            await StartAndMonitorJob(job, directory, text2imagePath, jsonPayload, batchCount);
        }


        private async Task StartAndMonitorJob(JobInfo job, string directory, string path, string json, int batch_count = 1)
        {
            using (HttpClient httpClient = new HttpClient()
            {
                BaseAddress = ActiveHost.Uri,
                Timeout = TimeSpan.FromMinutes(60)
            })
            {
                _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Готую рендер");


                var progressPerItem = 100.0 / batch_count;
                for (int i = 0; i < batch_count; i++)
                {
                    Stopwatch s = new Stopwatch();
                    s.Start();

                    var result = httpClient.PostAsync(path, new StringContent(json, null, "application/json"));

                    await MonitorProgress(job, batch_count, progressPerItem, i, result);

                    s.Stop();
                    var taskResult = result.Result;



                    if (taskResult.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var str = taskResult.Content.ReadAsStringAsync();

                        if (job.Type == JobType.Upscale)
                        {
                            var response = JsonConvert.DeserializeObject<UpscaleResponce>(str.Result);
                            byte[] imageBytes = Convert.FromBase64String(response.image);
                            var fileName = $"{DateTime.Now.ToString("yyyyMMddhhmmssfff")}_{job.Type}.png";
                            var filePath = Path.Combine(directory, fileName);

                            File.WriteAllBytes(filePath, imageBytes);

                            _databaseService.AddResult(job.Id, new JobResultInfo()
                            {
                                FilePath = filePath,
                                Info = Regex.Replace(response.html_info, "<.*?>", String.Empty),
                                RenderTime = s.Elapsed.Milliseconds
                            });
                        }
                        else
                        {
                            var response = JsonConvert.DeserializeObject<SdResponse>(str.Result);
                            var info = JsonConvert.DeserializeObject<ResInfo>(response.info);
                            for (int j = 0; j < response.images.Length; j++)
                            {
                                string? item = response.images[j];
                                byte[] imageBytes = Convert.FromBase64String(item);
                                var fileName = $"{DateTime.Now.ToString("yyyyMMddhhmmssfff")}_{job.Type}.png";
                                var filePath = Path.Combine(directory, fileName);

                                File.WriteAllBytes(filePath, imageBytes);
                                //File.WriteAllText(filePath + ".txt", info.infotexts[j]);

                                //inputMedia.Add(filePath);

                                _databaseService.AddResult(job.Id, new JobResultInfo()
                                {
                                    FilePath = filePath,
                                    Info = info.infotexts[j],
                                    RenderTime = s.Elapsed.TotalMilliseconds
                                });
                            }

                        }
                    }
                    else
                    {
                        var text = "Error calliing API. Check SD console:" + taskResult.ReasonPhrase;
                        _logger.LogError(text);
                        throw new RenderFailedException(taskResult.ReasonPhrase);
                    }

                }

                await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Готово. Прогрес 100%. Збираю та відправляю зображення");
            }
        }

        private async Task MonitorProgress(JobInfo job, int batch_count, double progressPerItem, int i, Task<HttpResponseMessage> result)
        {
            var oldProgress = 0.0;

            while (!result.IsCompleted)
            {
                await Task.Delay(5000);
                try
                {

                    using (HttpClient progressHttpClient = new HttpClient()
                    {
                        BaseAddress = ActiveHost.Uri
                    })
                    {
                        var progressResponce = await progressHttpClient.GetAsync(progressPath);
                        var progressobj = JsonConvert.DeserializeObject<ProgressResponse>(await progressResponce.Content.ReadAsStringAsync());
                        double localProgress = progressobj.progress;
                        var eta = progressobj.eta_relative;

                        //var progress = (localProgress * 100 + i * 100) / batch_count;
                        localProgress = localProgress == 0 ? 1 : localProgress;
                        var progress = (progressPerItem * i) + (localProgress * 100 / batch_count);
                        _logger.LogTrace($"{job.ChatId} {job.BotMessageId} - {Math.Round(progress, 2)}%");

                        if (progress != oldProgress)
                        {
                            var timespan = TimeSpan.FromSeconds(eta).ToString("hh\\:mm\\:ss");
                            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"[{ActiveHost.UI}] {job.Type} - Працюю... {i + 1}/{batch_count} Прогресс: {Math.Round(progress, 2)}%");

                            _databaseService.PostProgress(job.Id, progress, "progress");

                            oldProgress = progress;
                        }

                    }
                }
                catch (Exception ex)
                {
                    var error = "Не можу оновити прогрес, чекай на результат";
                    _logger.LogTrace(ex, error);
                    await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"Працюю... {error}");
                    result.Wait();
                }
            }
        }

        public override bool CanHnadle(JobType type)
        {
            switch (type)
            {
                case JobType.Text2Image:
                case JobType.HiresFix:
                case JobType.Upscale:
                    return true;
                case JobType.Vingette:
                case JobType.Noise:
                    return false;
                default: return false;
            }
        }
    }

    public class Automatic1111Cache
    {
        const string samplerPath = "/sdapi/v1/samplers";
        const string checkpointsInfo = "/sdapi/v1/sd-models";

        record SamplerChache(string server, IEnumerable<Sampler> samplers);
        record CheckpointsCache(string server, IEnumerable<CheckpointsInfo> checkpoints);


        private static ICollection<SamplerChache> _samplers = new List<SamplerChache>();
        private static ICollection<CheckpointsCache> _checkpointsInfo = new List<CheckpointsCache>();

        public static void InitCache(Uri server)
        {
            if(!_samplers.Any(x=>x.server == server.Host))
            {
                _samplers.Add(new SamplerChache(server.Host, LoadFromServer<Sampler>(server, samplerPath).Result));
            }

            if (!_checkpointsInfo.Any(x => x.server == server.Host))
            {
                _checkpointsInfo.Add(new CheckpointsCache(server.Host, LoadFromServer<CheckpointsInfo>(server, checkpointsInfo).Result));
            }
        }
        
        public static IEnumerable<Sampler> GetSampler(Uri server)
        {
            return _samplers.Single(x => x.server == server.Host).samplers;
        }

        public static IEnumerable<CheckpointsInfo> GetCheckpoints(Uri server)
        {
            return _checkpointsInfo.Single(x => x.server == server.Host).checkpoints;
        }

        private static async Task<IEnumerable<T>> LoadFromServer<T>(Uri uri, string path)
        {
            using (var client = new HttpClient() { BaseAddress = uri })
            {
                var response = await client.GetAsync(path);
                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<IEnumerable<T>>(await response.Content.ReadAsStringAsync());
                }
            }

            return null;
        }
    }


    public class Sampler
    {
        public string name { get; set; }
        public string[] aliases { get; set; }
        public Options options { get; set; }
    }

    public class Options
    {
        public string? scheduler { get; set; }
        public string? second_order { get; set; }
        public string? brownian_noise { get; set; }
        public string? uses_ensd { get; set; }
        public string? discard_next_to_last_sigma { get; set; }
        public string? solver_type { get; set; }
    }


    public class CheckpointsInfo
    {
        public string title { get; set; }
        public string model_name { get; set; }
        public string? hash { get; set; }
        public string? sha256 { get; set; }
        public string filename { get; set; }
        public string? config { get; set; }
    }


}
