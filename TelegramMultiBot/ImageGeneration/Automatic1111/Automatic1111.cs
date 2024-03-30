using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.ImageGeneration.Exceptions;
using TelegramMultiBot.ImageGenerators.Automatic1111.Api;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    internal partial class Automatic1111 : Diffusor
    {
        private readonly TelegramClientWrapper _client;
        private readonly ILogger<Automatic1111> _logger;
        private readonly IImageDatabaseService _databaseService;
        private readonly ImageGeneationSettings _settings;
        private readonly Automatic1111Settings _automaticSettings;

        // private readonly IServiceProvider _serviceProvider;
        private const string _text2imagePath = "/sdapi/v1/txt2img";

        private const string _img2imgPath = "/sdapi/v1/img2img";
        private const string _extrasPath = "/sdapi/v1/extra-single-image";
        private const string _progressPath = "/sdapi/v1/progress?skip_current_image=true";

        public Automatic1111(TelegramClientWrapper client, IConfiguration configuration, ILogger<Automatic1111> logger, IImageDatabaseService databaseService) : base(logger, configuration)
        {
            _client = client;
            _logger = logger;
            _databaseService = databaseService;
            _settings = configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>() ?? throw new NullReferenceException(nameof(_settings));
            _automaticSettings = configuration.GetSection(Automatic1111Settings.Name).Get<Automatic1111Settings>() ?? throw new NullReferenceException(nameof(_automaticSettings));
        }

        protected override string PingPath => "/internal/sysinfo";

        public override string UI { get => nameof(Automatic1111); }

        public override bool ValidateConnection(string content)
        {
            return content.Contains("--api");
        }

        public override async Task<JobInfo> Run(JobInfo job)
        {
            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Завдання розпочато");

            Automatic1111Cache.InitCache(ActiveHost!.Uri);

            var baseDir = _settings.BaseOutputDirectory;
            var outputDir = _automaticSettings.OutputDirectory;

            var directory = Path.Combine(baseDir, outputDir, DateTime.Today.ToString("yyyyMMdd"));

            if (!Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            try
            {
                switch (job.Type)
                {
                    case JobType.Text2Image:
                        await TextToImage(job, directory);
                        break;

                    case JobType.Upscale:
                        _ = await PostProcessingUpscale(job, directory);
                        break;

                    case JobType.HiresFix:
                        _ = await Img2ImgUpscale(job, directory);
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
            if (job.PreviousJobResultId is null)
                throw new NullReferenceException(nameof(job.PreviousJobResultId));

            var jobResultInfo = _databaseService.GetJobResult(job.PreviousJobResultId);
            if (jobResultInfo is null)
                throw new NullReferenceException(nameof(jobResultInfo));

            var upscaleparams = new UpscaleParams(jobResultInfo);
            var payload = File.ReadAllText(Path.Combine(_automaticSettings.UpscalePath, "extras-single.json"));

            JObject json = JObject.Parse(payload);
            json["image"] = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(upscaleparams.FilePath));
            json["upscaling_resize"] = job.UpscaleModifyer;
            var jsonPayload = json.ToString();

            await StartAndMonitorJob(job, directory, _extrasPath, jsonPayload);

            return job;
        }

        private async Task<JobInfo> Img2ImgUpscale(JobInfo job, string directory)
        {
            if (job.PreviousJobResultId is null)
                throw new NullReferenceException(nameof(job.PreviousJobResultId));

            var previousResult = _databaseService.GetJobResult(job.PreviousJobResultId) ?? throw new NullReferenceException(nameof(job.PreviousJobResultId));
            var upscaleparams = new UpscaleParams(previousResult);

            var payload = File.ReadAllText(Path.Combine(_automaticSettings.UpscalePath, "img2img.json"));

            JObject json = JObject.Parse(payload);
            json["prompt"] = upscaleparams.Prompt;
            if (upscaleparams.NegativePrompt != null)
            {
                json["negative_prompt"] = upscaleparams.NegativePrompt;
            }
            json["width"] = upscaleparams.Width * _settings.UpscaleMultiplier;
            json["height"] = upscaleparams.Height * _settings.UpscaleMultiplier;

            IEnumerable<Sampler> samplers = Automatic1111Cache.GetSampler(ActiveHost!.Uri);
            //var sampler = samplers.First(x => x.name == upscaleparams.Sampler );

            json["sampler_name"] = samplers.Where(x => x.aliases.Contains(upscaleparams.Sampler) || x.name == upscaleparams.Sampler).First().name;
            json["steps"] = upscaleparams.Steps;
            json["cfg_scale"] = upscaleparams.CFGScale;
            json["denoising_strength"] = _settings.HiresFixDenoise;
            var model = _settings.Models.Single(x => Path.GetFileNameWithoutExtension(x.Path) == upscaleparams.Model);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            json["override_settings"]["sd_model_checkpoint"] = upscaleparams.Model;
            json["override_settings"]["CLIP_stop_at_last_layers"] = model.CLIPskip;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            json["init_images"] = new JArray(){
                    "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(upscaleparams.FilePath))
                };
            var jsonPayload = json.ToString();

            await StartAndMonitorJob(job, directory, _img2imgPath, jsonPayload);

            return job;
        }

        private async Task TextToImage(JobInfo job, string directory)
        {
            var genParams = new GenerationParams(job);

            var batchCount = _settings.BatchCount;
            if (string.IsNullOrEmpty(genParams.Model))
            {
                genParams.Model = _settings.DefaultModel;
            }
            if (genParams.BatchCount != 0)
            {
                batchCount = genParams.BatchCount;
            }
            string payload = File.ReadAllText(Path.Combine(_automaticSettings.PayloadPath, "sd-payload-sdxl.json"));

            JObject json = JObject.Parse(payload);
            json["width"] = genParams.Width;
            json["height"] = genParams.Height;
            json["prompt"] = genParams.Prompt;
            json["negative_prompt"] = genParams.NegativePrompt;
            json["seed"] = genParams.Seed;

            ModelSettings model;
            try
            {
                model = _settings.Models.Single(x => x.Name == genParams.Model);
            }
            catch (Exception)
            {
                throw new InputException("Невідома модель: " + genParams.Model);
            }
#pragma warning disable CS8602 // Dereference of a possibly null reference.

            json["override_settings"]["sd_model_checkpoint"] = model.Path;
            json["override_settings"]["CLIP_stop_at_last_layers"] = model.CLIPskip;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

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

            var samplers = Automatic1111Cache.GetSampler(ActiveHost!.Uri);
            var samplerName = samplers.Where(x => x.aliases.Contains(samplerAlias)).First().name;

            json["sampler_name"] = samplerName;

            var jsonPayload = json.ToString();
            await StartAndMonitorJob(job, directory, _text2imagePath, jsonPayload, batchCount);
        }

        private async Task StartAndMonitorJob(JobInfo job, string directory, string path, string json, int batch_count = 1)
        {
            using HttpClient httpClient = new()
            {
                BaseAddress = ActiveHost!.Uri,
                Timeout = TimeSpan.FromMinutes(60)
            };

            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Готую рендер");

            var progressPerItem = 100.0 / batch_count;
            for (int i = 0; i < batch_count; i++)
            {
                Stopwatch s = new();
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
                        var response = JsonConvert.DeserializeObject<UpscaleResponce>(str.Result) ?? throw new InvalidOperationException("Cannot deserialize response");
                        byte[] imageBytes = Convert.FromBase64String(response.image);
                        var fileName = $"{DateTime.Now:yyyyMMddhhmmssfff}_{job.Type}.png";
                        var filePath = Path.Combine(directory, fileName);

                        File.WriteAllBytes(filePath, imageBytes);

                        _databaseService.AddResult(job.Id, new JobResultInfoCreate()
                        {
                            FilePath = filePath,
                            Info = ParseInfoRegex().Replace(response.html_info, String.Empty),
                            RenderTime = s.Elapsed.Milliseconds
                        });
                    }
                    else
                    {
                        var response = JsonConvert.DeserializeObject<SdResponse>(str.Result) ?? throw new InvalidOperationException("Cannot deserialize response");
                        var info = JsonConvert.DeserializeObject<ResInfo>(response.info);
                        for (int j = 0; j < response.images.Length; j++)
                        {
                            string? item = response.images[j];
                            byte[] imageBytes = Convert.FromBase64String(item);
                            var fileName = $"{DateTime.Now:yyyyMMddhhmmssfff}_{info?.seed}_{job.Type}.png";
                            var filePath = Path.Combine(directory, fileName);

                            File.WriteAllBytes(filePath, imageBytes);

                            _databaseService.AddResult(job.Id, new JobResultInfoCreate()
                            {
                                FilePath = filePath,
                                Info = info?.infotexts?[j] ?? string.Empty,
                                RenderTime = s.Elapsed.TotalMilliseconds
                            });
                        }
                    }
                }
                else
                {
                    var text = "Error calliing API. Check SD console:" + taskResult.ReasonPhrase;
                    _logger.LogError("{error}", text);
                    throw new RenderFailedException(taskResult.ReasonPhrase);
                }
            }

            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Готово. Прогрес 100%. Збираю та відправляю зображення");
        }

        private async Task MonitorProgress(JobInfo job, int batch_count, double progressPerItem, int i, Task<HttpResponseMessage> result)
        {
            var oldProgress = 0.0;

            while (!result.IsCompleted)
            {
                await Task.Delay(5000);
                try
                {
                    using HttpClient progressHttpClient = new()
                    {
                        BaseAddress = ActiveHost!.Uri
                    };
                    var progressResponce = await progressHttpClient.GetAsync(_progressPath);
                    var progressobj = JsonConvert.DeserializeObject<ProgressResponse>(await progressResponce.Content.ReadAsStringAsync()) ?? throw new InvalidOperationException("Cannot deserialize responce");
                    double localProgress = progressobj.progress;
                    var eta = progressobj.eta_relative;

                    //var progress = (localProgress * 100 + i * 100) / batch_count;
                    localProgress = localProgress == 0 ? 1 : localProgress;
                    var progress = (progressPerItem * i) + (localProgress * 100 / batch_count);
                    _logger.LogTrace("{chatId} {botMId} - {progress}%", job.ChatId, job.BotMessageId, Math.Round(progress, 2));

                    if (progress != oldProgress)
                    {
                        var timespan = TimeSpan.FromSeconds(eta).ToString("hh\\:mm\\:ss");
                        await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"[{ActiveHost.UI}] {job.Type} - Працюю... {i + 1}/{batch_count} Прогресс: {Math.Round(progress, 2)}%");

                        _databaseService.PostProgress(job.Id, progress, "progress");

                        oldProgress = progress;
                    }
                }
                catch (Exception ex)
                {
                    var error = "Не можу оновити прогрес, чекай на результат";
                    _logger.LogTrace(ex, "{error}", error);
                    await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"Працюю... {error}");
                    result.Wait();
                }
            }
        }

        public override bool CanHandle(JobType type)
        {
            return type switch
            {
                JobType.Text2Image or JobType.HiresFix or JobType.Upscale => true,
                JobType.Vingette or JobType.Noise => false,
                _ => false,
            };
        }

        [GeneratedRegex("<.*?>")]
        private static partial Regex ParseInfoRegex();
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE1006 // Naming Styles

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

#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}