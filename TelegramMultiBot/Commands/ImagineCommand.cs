
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands;
using TelegramMultiBot.ImageGeneration;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    [ServiceKey("imagine")]

    internal class ImagineCommand : BaseCommand, ICallbackHandler
    {
        private readonly TelegramBotClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImagineCommand> _logger;
        private readonly ImageGenearatorQueue _imageGenearatorQueue;
        string activeHost = string.Empty;
        public ImagineCommand(TelegramBotClient client, IConfiguration configuration, ILogger<ImagineCommand> logger, ImageGenearatorQueue imageGenearatorQueue)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
            _imageGenearatorQueue = imageGenearatorQueue;
        }



        public override async Task Handle(Message message)
        {
            if (message.Text == "/imagine" || message.Text == $"/imagine@{BotService.BotName}")
            {
                var markup = new ForceReplyMarkup();
                markup.InputFieldPlaceholder = "/imagine cat driving a bike";
                markup.Selective = true;

                var reply =
@"Привіт, я бобер\-художник, і я сприймаю повідомлення в наступному форматі 
`/imagine cat driving a bike`
Доступні хештеги\: `\#sd` `\#file` `\#info`
Щоб дізнатися більше /help";

                using (var stream = new MemoryStream(Properties.Resources.artist))
                {
                    var photo = InputFile.FromStream(stream, "beaver.png");
                    await _client.SendPhotoAsync(message.Chat, photo, message.MessageThreadId, reply, ParseMode.MarkdownV2, replyMarkup: markup);
                }


                //await _client.SendTextMessageAsync(message.Chat.Id, reply, replyMarkup: markup, parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2, messageThreadId: message.MessageThreadId);
            }
            else
            {
                try
                {
                    var job = new GenerationJob(message);
                    _imageGenearatorQueue.AddJob(job);
                    job.BotMessage = await _client.SendTextMessageAsync(message.Chat.Id, $"Твій шедевр в черзі. Чекай", messageThreadId: message.MessageThreadId, replyToMessageId: message.MessageId);
                }
                catch (InvalidOperationException ex)
                {
                    await _client.SendTextMessageAsync(message.Chat.Id, ex.Message);
                }
            }
        }

        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            var callbackData = CallbackData.FromString(callbackQuery.Data);

            if (callbackData.Data.Contains("upscale"))
            {
                await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "DoingUpscale");
                _imageGenearatorQueue.AddJob(new UpscaleJob(callbackQuery));
            }
            else if (callbackData.Data.Contains("hiresfx"))
            {
                await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "DoingHiresfix");
                _imageGenearatorQueue.AddJob(new GenerationJob(callbackQuery));
            }
            else
            {
                await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Unknown command");
            }
        }


        internal async void JobFailed(IJob obj, Exception exception)
        {
            if (exception is SystemException)
            {
                using (var stream = new MemoryStream(Properties.Resources.asleep))
                {
                    var photo = InputFile.FromStream(stream, "beaver.png");
                    await _client.SendPhotoAsync(obj.OriginalChatId, photo, messageThreadId: obj.OriginalMessageThreadId, caption: exception.Message);
                }
                await _client.DeleteMessageAsync(obj.OriginalChatId, obj.BotMessage.MessageId);
            }
            else
            {
                await _client.EditMessageTextAsync(obj.BotMessage.Chat.Id, obj.BotMessage.MessageId, "Помилка: " + exception.Message);
                Directory.Delete(obj.TmpDir, true);
            }

        }

        internal async void JobFinished(IJob inputJob)
        {
            if (inputJob is GenerationJob)
            {
                var job = (GenerationJob)inputJob;

                if (!await OriginalMessageExists(job.OriginalChatId, job.OriginalMessageId))
                {
                    await _client.EditMessageTextAsync(job.BotMessage.Chat.Id, job.BotMessage.MessageId, "Запит було видалено");
                    return;
                }

                using (var streams = new StreamList(job.Results.Select(x => System.IO.File.OpenRead(x))))
                {
                    var media = new List<IAlbumInputMedia>();

                    foreach (var stream in streams)
                    {
                        dynamic mediaInputMedia;
                        if (job.AsFile)
                        {
                            mediaInputMedia = new InputMediaDocument(InputFile.FromStream(stream, Path.GetFileName(stream.Name)));
                        }
                        else
                        {
                            mediaInputMedia = new InputMediaPhoto(InputFile.FromStream(stream, Path.GetFileName(stream.Name)));
                        }

                        mediaInputMedia.Caption = Path.GetFileName(stream.Name);
                        media.Add(mediaInputMedia);

                        //    var keys = new List<InlineKeyboardButton>
                        //{
                        //    InlineKeyboardButton.WithCallbackData("Upscale", new CallbackData(Command, "upscale|" + Path.GetFileName(stream.Name)).DataString),
                        //    InlineKeyboardButton.WithCallbackData("Hires Fix", new CallbackData(Command, "hiresfx|" + Path.GetFileName(stream.Name)).DataString)
                        //};
                        //    var message = await _client.SendPhotoAsync(new ChatId(job.OriginalChatId), InputFile.FromStream(stream, Path.GetFileName(stream.Name)), messageThreadId: job.OriginalMessageThreadId, replyToMessageId: job.OriginalMessageId, replyMarkup: new InlineKeyboardMarkup(keys));

                    }

                    var message = await _client.SendMediaGroupAsync(new ChatId(job.OriginalChatId), media, messageThreadId: job.OriginalMessageThreadId, replyToMessageId: job.OriginalMessageId);

                    if (job.PostInfo)
                    {
                        var info = string.Join('\n', streams.Select(x => $"```{Path.GetFileName(x.Name)}\nRender time {job.Elapsed}\n{System.IO.File.ReadAllText(x.Name + ".txt")}```"));
                        await _client.SendTextMessageAsync(new ChatId(job.OriginalChatId), info, messageThreadId: job.OriginalMessageThreadId, replyToMessageId: job.OriginalMessageId, parseMode: ParseMode.MarkdownV2);
                    }
                }
                await _client.DeleteMessageAsync(job.BotMessage.Chat.Id, job.BotMessage.MessageId);
            }

            Directory.Delete(inputJob.TmpDir, true);
        }

        private async Task<bool> OriginalMessageExists(long chatId, int messageId)
        {
            var testChatId = _configuration.GetValue<long>("testChatId");
            try
            {
                var copied = await _client.CopyMessageAsync(testChatId, chatId, messageId);
                await _client.DeleteMessageAsync(testChatId, copied.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return false;
            }
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
}
