
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Telegram.Bot;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.ImageGeneration.Exceptions;
using TelegramMultiBot.ImageGenerators.Automatic1111.Api;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    class Automatic1111 : IDiffusor
    {
        private readonly TelegramBotClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<Automatic1111> _logger;
        private readonly IServiceProvider _serviceProvider;
        string text2imagePath = "/sdapi/v1/txt2img";
        string img2imgPath = "/sdapi/v1/img2img";
        string extrasPath = "/sdapi/v1/extra-single-image";

        string progressPath = "/sdapi/v1/progress?skip_current_image=true";
        string pingPath = "/internal/sysinfo";
        HostSettings activeHost;
        public Automatic1111(TelegramBotClient client, IConfiguration configuration, ILogger<Automatic1111> logger, IServiceProvider serviceProvider)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public string UI { get => nameof(Automatic1111); }

        public bool isAvailable()
        {
            var hosts = _configuration.GetSection("Hosts").Get<IEnumerable<HostSettings>>().Where(x => x.UI == nameof(Automatic1111));
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

                    if (resp.Result.Content.ReadAsStringAsync().Result.Contains("--api"))
                    {
                        activeHost = host;
                        return true;
                    }
                    else
                    {
                        _logger.LogTrace($"{host.Uri} api disabled");
                    }
                }
                catch (Exception)
                {
                    _logger.LogTrace($"{host.Uri} not available");
                }
            }
            return false;
        }



        public async Task<ImageJob?> Run(ImageJob job, string directory)
        {
            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"Запущено на [{activeHost.UI}]");
            _logger.LogTrace(activeHost.Uri.ToString());

            switch (job.Type)
            {
                case JobType.Text2Image:
                    return await TextToImage(job, directory);
                case JobType.Upscale:
                    return await PostProcessingUpscale(job, directory);
                case JobType.HiresFix:
                    return await Img2ImgUpscale(job, directory);
                default:
                    return null;
            }
        }

        private async Task<ImageJob?> PostProcessingUpscale(ImageJob job, string directory)
        {
            var previousResult = _serviceProvider.GetService<ImageDatabaseService>().GetJobResult(job.PreviousJobResultId);
            var upscaleparams = new UpscaleParams(previousResult);

            var settings = _configuration.GetSection("ImageGeneation:Automatic1111").Get<Automatic1111Settings>();
            var payload = File.ReadAllText(Path.Combine(settings.UpscalePath, "extras-single.json"));

            JObject json = JObject.Parse(payload);
            json["image"] = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(upscaleparams.FilePath));
            json["upscaling_resize"] = job.UpscaleModifyer;
            var jsonPayload = json.ToString();

            await StartAndMonitorJob(job, directory, extrasPath, jsonPayload);

            return job;
        }

        private async Task<ImageJob> Img2ImgUpscale(ImageJob job, string directory)
        {
            var previousResult = _serviceProvider.GetService<ImageDatabaseService>().GetJobResult(job.PreviousJobResultId);
            var upscaleparams = new UpscaleParams(previousResult);

            var settings = _configuration.GetSection("ImageGeneation:Automatic1111").Get<Automatic1111Settings>();
            var payload = File.ReadAllText(Path.Combine(settings.UpscalePath, "img2img.json"));

            JObject json = JObject.Parse(payload);
            json["prompt"] = upscaleparams.Prompt;
            json["negative_prompt"] = upscaleparams.NegativePrompt;
            json["width"] = upscaleparams.Width * settings.UpscaleMultiplier;
            json["height"] = upscaleparams.Height * settings.UpscaleMultiplier;
            json["sampler_name"] = upscaleparams.Sampler;
            json["steps"] = upscaleparams.Steps;
            json["cfg_scale"] = upscaleparams.CFGScale;
            json["override_settings"]["sd_model_checkpoint"] = upscaleparams.Model;
            json["init_images"] = new JArray(){
                    "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(upscaleparams.FilePath))
                };
            var jsonPayload = json.ToString();

            await StartAndMonitorJob(job, directory, img2imgPath, jsonPayload);

            return job;
        }
        private async Task<ImageJob> TextToImage(ImageJob job, string directory)
        {
            var genParams = new GenerationParams(job);
            var settings = _configuration.GetSection("ImageGeneation:Automatic1111").Get<Automatic1111Settings>();

            var batchCount = settings.BatchCount;
            var confName = "sd-payload-xl";

            batchCount = settings.HiResBatchCount;

            if (!string.IsNullOrEmpty(genParams.Model))
            {
                confName += "-" + genParams.Model;
            }
            else
            {
                confName += "-" + settings.DefaultModel;
            }

            if (genParams.BatchCount != 0)
            {
                batchCount = genParams.BatchCount;
            }


            string payload;
            try
            {
                payload = File.ReadAllText(Path.Combine(settings.PayloadPath, confName + ".json"));
            }
            catch (Exception)
            {
                throw new InputException("Невідома модель: " + genParams.Model);
            }

            JObject json = JObject.Parse(payload);
            json["width"] = genParams.Width;
            json["height"] = genParams.Height;
            json["prompt"] = genParams.Prompt;
            json["negative_prompt"] = genParams.NegativePrompt;
            json["seed"] = genParams.Seed;


            var jsonPayload = json.ToString();
            await StartAndMonitorJob(job, directory, text2imagePath, jsonPayload, batchCount);

            return job;

        }


        private async Task StartAndMonitorJob(ImageJob job, string directory, string path, string json, int batch_count = 1)
        {
            using (HttpClient httpClient = new HttpClient()
            {
                BaseAddress = activeHost.Uri,
                Timeout = TimeSpan.FromMinutes(60)
            })
            {

                Stopwatch s = new Stopwatch();
                s.Start();

                var progressPerItem = 100.0 / batch_count;
                for (int i = 0; i < batch_count; i++)
                {

                    var result = httpClient.PostAsync(path, new StringContent(json, null, "application/json"));

                    await MonitorProgress(job, batch_count, progressPerItem, i, result);

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

                            job.Results.Add(new JobResult()
                            {
                                FilePath = filePath,
                                Info = Regex.Replace(response.html_info, "<.*?>", String.Empty),
                                Index = 1,
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

                                job.Results.Add(new JobResult()
                                {
                                    FilePath = filePath,
                                    Info = info.infotexts[j],
                                    Index = i * response.images.Length + j + 1,
                                });
                            }

                        }
                    }
                    else
                    {
                        _logger.LogError("Error calliing API. Check SD console:" + taskResult.ReasonPhrase);
                        throw new RenderFailedException(taskResult.ReasonPhrase);
                    }
                }

                s.Stop();
                job.RenderTime = s.Elapsed;

                await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Готово. Прогрес 100%. Збираю та відправляю зображення");
            }
        }

        private async Task MonitorProgress(ImageJob job, int batch_count, double progressPerItem, int i, Task<HttpResponseMessage> result)
        {
            var oldProgress = 0.0;

            while (!result.IsCompleted)
            {
                await Task.Delay(5000);
                try
                {

                    using (HttpClient progressHttpClient = new HttpClient()
                    {
                        BaseAddress = activeHost.Uri
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
                            await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, $"Працюю... {job.Type} {i + 1}/{batch_count} Прогресс: {Math.Round(progress, 2)}%");
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
    }
}
