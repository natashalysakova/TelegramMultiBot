
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("imagine")]

    internal class StableDiffusionCommand : BaseCommand
    {
        private readonly TelegramBotClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StableDiffusionCommand> _logger;
        private readonly ImageGenearatorQueue _imageGenearatorQueue;
        string activeHost = string.Empty;
        public StableDiffusionCommand(TelegramBotClient client, IConfiguration configuration, ILogger<StableDiffusionCommand> logger, ImageGenearatorQueue imageGenearatorQueue)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
            _imageGenearatorQueue = imageGenearatorQueue;
        }



        public override async Task Handle(Message message)
        {
            //if(message.Chat.Type != Telegram.Bot.Types.Enums.ChatType.Private)
            //{
            //    await _client.SendTextMessageAsync(message.Chat.Id, "tag /imagine works only in private messages");
            //    return;
            //}





            if (message.Text == "/imagine" || message.Text == $"/imagine@{BotService.BotName}")
            {
                var markup = new ForceReplyMarkup();
                markup.InputFieldPlaceholder = "/imagine cat driving a bike";
                markup.Selective = true;

                await _client.SendTextMessageAsync(message.Chat.Id, "send message in following format `/imagine cat driving a bike`", replyMarkup: markup, parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2, messageThreadId: message.MessageThreadId);
            }
            else
            {
                var botMessage = await _client.SendTextMessageAsync(message.Chat.Id, $"Your job in the queue", messageThreadId: message.MessageThreadId, replyToMessageId: message.MessageId);
                var prompt = message.Text.Substring(message.Text.IndexOf(' '));
                _imageGenearatorQueue.AddJob(prompt, botMessage, message);

            }
        }

        internal async void JobFailed(GenerationJob obj, string error)
        {
            await _client.EditMessageTextAsync(obj.BotMessage.Chat.Id, obj.BotMessage.MessageId, "Job failed. Error: " + error);
            Directory.Delete(obj.TmpDir, true);
        }

        internal async void JobFinished(GenerationJob obj)
        {
            using (var streams = new StreamList(obj.Images.Select(x => System.IO.File.OpenRead(x))))
            {
                var photos = new List<InputMediaPhoto>();

                foreach (var stream in streams)
                {
                    var photo = new InputMediaPhoto(InputFile.FromStream(stream, Path.GetFileName(stream.Name)));
                    try
                    {
                        string info = $"Render time {obj.Elapsed}\n" + System.IO.File.ReadAllText(stream.Name + ".txt");
                        photo.Caption = info.Length > 1024 ? info.Substring(0, 1024) : info;
                    }
                    catch { }
                    photos.Add(photo);
                }
                await _client.SendMediaGroupAsync(new ChatId(obj.OriginalChatId), photos, messageThreadId: obj.OriginalMessageThreadId, replyToMessageId: obj.OriginalMessageId);
            }

            await _client.DeleteMessageAsync(obj.BotMessage.Chat.Id, obj.BotMessage.MessageId);
            Directory.Delete(obj.TmpDir, true);

        }

        class StreamList : IDisposable, IEnumerable<FileStream>
        {
            List<FileStream> _streams;

            public StreamList(IEnumerable<FileStream> streams)
            {
                _streams = new List<FileStream>(streams);
            }

            public void Dispose()
            {
                foreach (var item in _streams)
                {
                    item.Close();
                    item.Dispose();
                }
            }

            public IEnumerator<FileStream> GetEnumerator()
            {
                return _streams.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _streams.GetEnumerator();
            }
        }

    }

    class ImageGenearatorQueue
    {
        Queue<GenerationJob> _jobs = new Queue<GenerationJob>();
        private readonly ILogger<ImageGenearatorQueue> _logger;
        private readonly ImageGenerator _imageGenerator;

        public ImageGenearatorQueue(ILogger<ImageGenearatorQueue> logger, ImageGenerator imageGenerator)
        {
            _logger = logger;
            _imageGenerator = imageGenerator;
        }

        public void Run(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_jobs.TryDequeue(out var job))
                    {
                        _logger.LogDebug("Starting " + job.Prompt);

                        try
                        {
                            await _imageGenerator.Run(job);
                            JobFinished?.Invoke(job);
                            _logger.LogDebug("Finished " + job.Prompt);

                        }
                        catch (Exception ex)
                        {
                            JobFailed?.Invoke(job, ex.Message);
                            _logger.LogDebug("Failed " + job.Prompt);

                        }
                    }

                    Task.Delay(500);
                }

            }, cancellationToken);
        }

        internal void AddJob(string prompt, Message botMessage, Message message)
        {
            _jobs.Enqueue(new GenerationJob { BotMessage = botMessage, Prompt = prompt,  OriginalChatId = message.Chat.Id, OriginalMessageId = message.MessageId});
        }

        public event Action<GenerationJob> JobFinished;
        public event Action<GenerationJob, string> JobFailed;
    }

    class GenerationJob
    {
        public IEnumerable<string> Images { get; internal set; }

        public int OriginalMessageId { get; set; }
        public long OriginalChatId { get; set; }
        public int OriginalMessageThreadId { get; set; }

        public Message BotMessage { get; internal set; }
        public string Prompt { get; internal set; }
        public string TmpDir { get; internal set; }
        public TimeSpan Elapsed { get; internal set; }
    }


    class ImageGenerator
    {
        private readonly ILogger<ImageGenerator> _logger;
        private readonly TelegramBotClient _client;
        private readonly IConfiguration _configuration;
        string path = "/sdapi/v1/txt2img";
        string progressPath = "/sdapi/v1/progress?skip_current_image=true";
        string pingPath = "/internal/sysinfo";
        string directory;

        public ImageGenerator(ILogger<ImageGenerator> logger, IConfiguration configuration, TelegramBotClient client)
        {

            directory = Path.Combine(Directory.GetCurrentDirectory(), "tmp");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _logger = logger;
            _configuration = configuration;
            _client = client;
        }

        public async Task Run(GenerationJob job)
        {
            await RenderImages(job);
        }

        private async Task RenderImages(GenerationJob job)
        {
            string activeHost = string.Empty;
            var hosts = _configuration.GetSection("hosts").Get<string[]>();
            foreach (string host in hosts)
            {
                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(host);
                httpClient.Timeout = TimeSpan.FromSeconds(5);
                try
                {
                    var resp = httpClient.GetAsync(pingPath);
                    resp.Wait();

                    if (resp.Result.Content.ReadAsStringAsync().Result.Contains("--api"))
                    {
                        activeHost = host;
                        break;
                    }
                }
                catch (Exception)
                {

                }
            }

            if (activeHost == string.Empty)
            {
                throw new Exception("Сервіси StableDiffusion наразі не запущені - спробйте пізніше");
            }

            var prompt = job.Prompt;
            var botMessage = job.BotMessage;

            job.TmpDir = Path.Combine(directory, job.OriginalChatId.ToString() + job.OriginalMessageId.ToString());
            Directory.CreateDirectory(job.TmpDir);

            await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, $"Working on host[{Array.IndexOf(hosts, activeHost) + 1}]");

            var inputMedia = new List<string>();


            using (HttpClient httpClient = new HttpClient()
            {
                BaseAddress = new Uri(activeHost),
                Timeout = TimeSpan.FromMinutes(5)
            })
            {
                var batch_count = _configuration.GetValue<int>("BatchCount");
                var confName = "sd-payload";

                if (prompt.Contains("—xl"))
                {
                    prompt = prompt.Replace("—xl", "");
                    confName += "-xl";
                    batch_count = _configuration.GetValue<int>("SdxlBatchCount");
                }
                var payload = System.IO.File.ReadAllText(confName + ".json");

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
                                BaseAddress = new Uri(activeHost)
                            })
                            {
                                var progressResponce = await progressHttpClient.GetAsync(progressPath);
                                var progressobj = JsonConvert.DeserializeObject<ProgressResponse>(await progressResponce.Content.ReadAsStringAsync());
                                localProgress = progressobj.progress;
                                var eta = progressobj.eta_relative;

                                var progress = ((localProgress * 100) + (i * 100)) / batch_count;
                                _logger.LogTrace($"{botMessage.Chat.Id} {botMessage.MessageId} - {Math.Round(progress, 2)}%");

                                if (progress != oldProgress)
                                {
                                    var timespan = TimeSpan.FromSeconds(eta).ToString("hh\\:mm\\:ss");
                                    await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, $"Working... Progress: {Math.Round(progress, 2)}% ETA: {timespan}");
                                    oldProgress = progress;
                                }

                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        var error = "Cant get progress. Waiting for results";
                        _logger.LogTrace(ex, error);
                        await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, $"Working... {error}");
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

                            System.IO.File.WriteAllBytes(filePath, imageBytes);
                            System.IO.File.WriteAllText(filePath + ".txt", info.infotexts[j]);

                            inputMedia.Add(filePath);
                        }
                    }
                }

                s.Stop();
                job.Elapsed = s.Elapsed;

                await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, "Done. Progress: 100%. Sending images");
            }
            job.Images = inputMedia;
        }

    }


    public class ProgressResponse
    {
        public float progress { get; set; }
        public float eta_relative { get; set; }
        public State state { get; set; }
        public object current_image { get; set; }
        public object textinfo { get; set; }
    }

    public class State
    {
        public bool skipped { get; set; }
        public bool interrupted { get; set; }
        public string job { get; set; }
        public int job_count { get; set; }
        public string job_timestamp { get; set; }
        public int job_no { get; set; }
        public int sampling_step { get; set; }
        public int sampling_steps { get; set; }
    }



    public class ResInfo
    {
        public string prompt { get; set; }
        public string[] all_prompts { get; set; }
        public string negative_prompt { get; set; }
        public string[] all_negative_prompts { get; set; }
        public long seed { get; set; }
        public long[] all_seeds { get; set; }
        public long subseed { get; set; }
        public long[] all_subseeds { get; set; }
        public int? subseed_strength { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string sampler_name { get; set; }
        public double cfg_scale { get; set; }
        public int steps { get; set; }
        public int batch_size { get; set; }
        public bool restore_faces { get; set; }
        public object face_restoration_model { get; set; }
        public string sd_model_name { get; set; }
        public string sd_model_hash { get; set; }
        public object sd_vae_name { get; set; }
        public object sd_vae_hash { get; set; }
        public int? seed_resize_from_w { get; set; }
        public int? seed_resize_from_h { get; set; }
        public int? denoising_strength { get; set; }
        public Extra_Generation_Params extra_generation_params { get; set; }
        public int? index_of_first_image { get; set; }
        public string[] infotexts { get; set; }
        public object[] styles { get; set; }
        public string job_timestamp { get; set; }
        public int? clip_skip { get; set; }
        public bool is_using_inpainting_conditioning { get; set; }
    }

    public class Extra_Generation_Params
    {
    }



    public class SdResponse
    {
        public string[] images { get; set; }
        public Parameters parameters { get; set; }
        public string info { get; set; }

        public Detail[] detail { get; set; }
    }

    public class Parameters
    {
    }

    public class Detail
    {
        public object[] loc { get; set; }
        public string msg { get; set; }
        public string type { get; set; }
    }
}
