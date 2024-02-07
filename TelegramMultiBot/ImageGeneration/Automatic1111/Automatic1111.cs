
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using Telegram.Bot;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.ImageGenerators.Automatic1111.Api;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    class Automatic1111 : IDiffusor
    {
        private readonly TelegramBotClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<Automatic1111> _logger;
        string path = "/sdapi/v1/txt2img";
        string progressPath = "/sdapi/v1/progress?skip_current_image=true";
        string pingPath = "/internal/sysinfo";
        HostSettings activeHost;
        public Automatic1111(TelegramBotClient client, IConfiguration configuration, ILogger<Automatic1111> logger)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
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
                httpClient.Timeout = TimeSpan.FromSeconds(5);
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

        public async Task<GenerationJob> Run(GenerationJob job, string directory)
        {
            var prompt = job.Prompt;
            var botMessage = job.BotMessage;
            job.TmpDir = Path.Combine(directory, job.TmpDirName);
            Directory.CreateDirectory(job.TmpDir);

            await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, $"Рендер запущено на хосту [{activeHost.UI}]");
            _logger.LogTrace(activeHost.Uri.ToString());
            var inputMedia = new List<string>();
             

            using (HttpClient httpClient = new HttpClient()
            {
                BaseAddress = activeHost.Uri,
                Timeout = TimeSpan.FromMinutes(60)
            })
            {
                var settings = _configuration.GetSection("ImageGeneation:Automatic1111").Get<Automatic1111Settings>();
                var batch_count = settings.BatchCount;
                var confName = "sd-payload";

                if (job.AsSDXL)
                {
                    confName += "-xl";
                    batch_count = settings.HiResBatchCount;
                }

                var payload = File.ReadAllText(Path.Combine(settings.PayloadPath, confName + ".json"));

                Stopwatch s = new Stopwatch();
                s.Start();


                for (int i = 0; i < batch_count; i++)
                {

                    var jsonPayload = payload.Replace("{USER_PROMPT}", prompt);
                    var result = httpClient.PostAsync(path, new StringContent(jsonPayload, null, "application/json"));

                    try
                    {
                        var localProgress = 0.0;
                        var oldProgress = 0.0;


                        while (!result.IsCompleted)
                        {
                            await Task.Delay(5000);

                            using (HttpClient progressHttpClient = new HttpClient()
                            {
                                BaseAddress = activeHost.Uri
                            })
                            {
                                var progressResponce = await progressHttpClient.GetAsync(progressPath);
                                var progressobj = JsonConvert.DeserializeObject<ProgressResponse>(await progressResponce.Content.ReadAsStringAsync());
                                localProgress = progressobj.progress;
                                var eta = progressobj.eta_relative;

                                var progress = (localProgress * 100 + i * 100) / batch_count;
                                _logger.LogTrace($"{botMessage.Chat.Id} {botMessage.MessageId} - {Math.Round(progress, 2)}%");

                                if (progress != oldProgress)
                                {
                                    var timespan = TimeSpan.FromSeconds(eta).ToString("hh\\:mm\\:ss");
                                    await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, $"Працюю... Прогресс: {Math.Round(progress, 2)}% ETA: {timespan}");
                                    oldProgress = progress;
                                }

                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        var error = "Не можу оновити прогрес, чекай на результат";
                        _logger.LogTrace(ex, error);
                        await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, $"Працюю... {error}");
                        result.Wait();
                    }

                    var taskResult = result.Result;

                    if (taskResult.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var str = taskResult.Content.ReadAsStringAsync();
                        var response = JsonConvert.DeserializeObject<SdResponse>(str.Result);
                        var info = JsonConvert.DeserializeObject<ResInfo>(response.info);
                        for (int j = 0; j < response.images.Length; j++)
                        {
                            string? item = response.images[j];
                            byte[] imageBytes = Convert.FromBase64String(item);
                            var fileName = $"{DateTime.Now.ToString("yyyyMMddhhmmssfff")}_{job.OriginalChatId}_{job.OriginalMessageId}.png";
                            var filePath = Path.Combine(job.TmpDir, fileName);

                            File.WriteAllBytes(filePath, imageBytes);
                            File.WriteAllText(filePath + ".txt", info.infotexts[j]);

                            inputMedia.Add(filePath);
                        }
                    }
                    else
                    {
                        throw new Exception(taskResult.ReasonPhrase);
                    }
                }

                s.Stop();
                job.Elapsed = s.Elapsed;

                await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, "Готово. Прогрес 100%. Збираю та відправляю зображення");
            }

            job.Images = inputMedia;

            return job;
        }
    }
}
