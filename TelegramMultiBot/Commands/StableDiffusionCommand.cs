using AngleSharp.Media.Dom;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
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
    [Command("imagine")]

    internal class StableDiffusionCommand : BaseCommand
    {
        private readonly TelegramBotClient _client;
        private readonly string[]? hosts;
        string directory;
        string activeHost = string.Empty;
        public StableDiffusionCommand(TelegramBotClient client, IConfiguration configuration)
        {
            _client = client;

            hosts = configuration.GetSection("hosts").Get<string[]>();

            directory = Path.Combine(Directory.GetCurrentDirectory(), "tmp");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            
        }


        string path = "/sdapi/v1/txt2img";
        string progressPath = "/sdapi/v1/progress?skip_current_image=true";
        string pingPath = "/internal/sysinfo";

        public override async Task Handle(Message message)
        {
            //if(message.Chat.Type != Telegram.Bot.Types.Enums.ChatType.Private)
            //{
            //    await _client.SendTextMessageAsync(message.Chat.Id, "tag /imagine works only in private messages");
            //    return;
            //}
            foreach (string host in hosts)
            {
                var httpClient = new HttpClient();
                httpClient.BaseAddress = new Uri(host);

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
                await _client.SendTextMessageAsync(message.Chat.Id, "Can't find working SD");
                return;
            }


            if (message.Text == "/imagine")
            {
                var markup = new ForceReplyMarkup();
                markup.InputFieldPlaceholder = "/imagine cat driving a bike";
                markup.Selective = true;

                await _client.SendTextMessageAsync(message.Chat.Id, "send message in following format */imagine cat driving a bike*", replyMarkup: markup, parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2);
            }
            else
            {
                var tmpDir = Path.Combine(directory, message.Chat.Id.ToString() + message.MessageId.ToString());
                Directory.CreateDirectory(tmpDir);

                try
                {

                    var botMessage = await _client.SendTextMessageAsync(message.Chat.Id, $"Working host {Array.IndexOf(hosts, activeHost)}");

                    var prompt = message.Text.Substring(message.Text.IndexOf(' '));
                    var images = await RenderImages(prompt, botMessage, tmpDir);

                    using (var streams = new StreamList(images.Select(x => System.IO.File.OpenRead(x))))
                    {
                        var photos = new List<InputMediaPhoto>();

                        foreach (var stream in streams)
                        {
                            photos.Add(new InputMediaPhoto(InputFile.FromStream(stream, Path.GetFileName(stream.Name))));
                        }
                        await _client.SendMediaGroupAsync(new ChatId(message.Chat.Id), photos);

                    }

                    await _client.DeleteMessageAsync(botMessage.Chat.Id, botMessage.MessageId);
                    Directory.Delete(tmpDir, true);

                }
                catch (Exception ex)
                {
                    await _client.SendTextMessageAsync(message.Chat.Id, "Error: " + ex.Message);
                }



            }



            //var markup = new ForceReplyMarkup();
            //markup.InputFieldPlaceholder = "/imagine cat driving a bike";
            //markup.Selective = true;

            //var markup2 = new ReplyKeyboardMarkup(new[]
            //{
            //    new KeyboardButton(BotService.BotName)
            //});

            //var markup3 = new InlineKeyboardMarkup(new[]
            //    {
            //        InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("WithSwitchInlineQueryCurrentChat", $"/imagine:{message.Chat.Id}:")
            //    });
            //    await _client.SendTextMessageAsync(message.Chat.Id, "press button below and enter what you want to see", replyMarkup: markup3);




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


        private async Task<IEnumerable<string>> RenderImages(string prompt, Message message, string tmpdir)
        {
            HttpClient httpClient = new HttpClient()
            {
                BaseAddress = new Uri(activeHost)
            };


            var batch_count = 2;

            var inputMedia = new List<string>();
            for (int i = 0; i < batch_count; i++)
            {
                var payload = new Payload()
                {
                    prompt = prompt + " masterpiece, high quality, highres , artwork, 3 d render , hyper realism, high detail, octane render, 8k",
                    steps = 20,
                    batch_size = 1,
                    negative_prompt = "(bad_prompt:0.8), blurry eyes, extra hands, extra ears, extra legs, creepy, distortion",
                    seed = -1
                };
                var json = JsonConvert.SerializeObject(payload);

                var result = httpClient.PostAsync(path, new StringContent(json, null, "application/json"));

                var localProgress = 0.0;

                while (localProgress < 1)
                {
                    var progressResponce = await httpClient.GetAsync(progressPath);

                    //if (!progressResponce.IsSuccessStatusCode)
                    //{
                    //    result.Wait();
                    //    continue;
                    //}

                    var progressobj = JsonConvert.DeserializeObject<ProgressResponse>(await progressResponce.Content.ReadAsStringAsync());
                    localProgress = progressobj.progress;

                    if (progressobj.state.job_count == 0)
                    {
                        break;
                    }

                    var progress = ((localProgress * 100) + (i * 100)) / batch_count;
                    await _client.EditMessageTextAsync(message.Chat.Id, message.MessageId, $"Working... Progress: {Math.Round(progress, 0)}%");
                    await Task.Delay(500);
                }

                var taskResult = result.Result;

                if (taskResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var str = taskResult.Content.ReadAsStringAsync();
                    var response = JsonConvert.DeserializeObject<SdResponse>(str.Result);

                    foreach (var item in response.images)
                    {
                        byte[] imageBytes = Convert.FromBase64String(item);
                        var fileName = $"{DateTime.Now.ToString("yyyyMMddhhmmssfff")}_{message.Chat.Id}_{message.MessageId}.jpeg";
                        var filePath = Path.Combine(tmpdir, fileName);

                        System.IO.File.WriteAllBytes(filePath, imageBytes);

                        inputMedia.Add(filePath);
                    }
                }
            }
            await _client.EditMessageTextAsync(message.Chat.Id, message.MessageId, "Done. Progress: 100%. Preparing images");
            return inputMedia;

            //await _client.SendPhotoAsync(new ChatId(message.Chat.Id), inputFle);
            //try
            //{
            //    await _client.SendMediaGroupAsync(new ChatId(chatId), inputMedia);
            //}
            //catch (Exception ex)
            //{

            //    await _client.AnswerInlineQueryAsync(inlineQuery.Id, new InlineQueryResult[] { }, button: new InlineQueryResultsButton(ex.Message));

            //}
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



    public class Payload
    {
        public string prompt { get; set; }
        public string negative_prompt { get; set; }
        //public string[] styles { get; set; }
        public int seed { get; set; }
        //public int subseed { get; set; }
        //public int subseed_strength { get; set; }
        //public int seed_resize_from_h { get; set; }
        //public int seed_resize_from_w { get; set; }
        //public string sampler_name { get; set; }
        public int batch_size { get; set; }
        //public int n_iter { get; set; }
        public int steps { get; set; }
        //public int cfg_scale { get; set; }
        //public int width { get; set; }
        //public int height { get; set; }
        //public bool restore_faces { get; set; }
        //public bool tiling { get; set; }
        //public bool do_not_save_samples { get; set; }
        //public bool do_not_save_grid { get; set; }
        //public int eta { get; set; }
        //public int denoising_strength { get; set; }
        //public int s_min_uncond { get; set; }
        //public int s_churn { get; set; }
        //public int s_tmax { get; set; }
        //public int s_tmin { get; set; }
        //public int s_noise { get; set; }
        //public Override_Settings override_settings { get; set; }
        //public bool override_settings_restore_afterwards { get; set; }
        //public string refiner_checkpoint { get; set; }
        //public int refiner_switch_at { get; set; }
        //public bool disable_extra_networks { get; set; }
        //public Comments comments { get; set; }
        public bool enable_hr { get; set; }
        //public int firstphase_width { get; set; }
        //public int firstphase_height { get; set; }
        public double hr_scale { get; set; }
        public string hr_upscaler { get; set; }
        public int hr_second_pass_steps { get; set; }
        //public int hr_resize_x { get; set; }
        //public int hr_resize_y { get; set; }
        //public string hr_checkpoint_name { get; set; }
        //public string hr_sampler_name { get; set; }
        //public string hr_prompt { get; set; }
        //public string hr_negative_prompt { get; set; }
        //public string sampler_index { get; set; }
        //public string script_name { get; set; }
        //public object[] script_args { get; set; }
        //public bool send_images { get; set; }
        //public bool save_images { get; set; }
        //public Alwayson_Scripts alwayson_scripts { get; set; }
    }

    public class Override_Settings : Payload
    {
        public int CLIP_stop_at_last_layers { get; set; }
        public string sd_model_checkpoint { get; set; }
    }

    public class Comments
    {
    }

    public class Alwayson_Scripts
    {
    }

}
