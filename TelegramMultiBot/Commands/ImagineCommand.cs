
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
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    [ServiceKey("imagine")]

    internal class ImagineCommand : BaseCommand
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
Доступні хештеги\: `\#xl` `\#file` `\#info`
Щоб дізнатися більше /help";

                using (var stream = new MemoryStream(Properties.Resources.artist))
                {
                    var photo = InputFile.FromStream(stream, "beaver.png");
                    await _client.SendPhotoAsync(message.Chat, photo, message.MessageThreadId, reply, ParseMode.MarkdownV2,  replyMarkup: markup);
                }


                //await _client.SendTextMessageAsync(message.Chat.Id, reply, replyMarkup: markup, parseMode: Telegram.Bot.Types.Enums.ParseMode.MarkdownV2, messageThreadId: message.MessageThreadId);
            }
            else
            {
                try
                {
                    var job = _imageGenearatorQueue.AddJob(message);
                    job.BotMessage = await _client.SendTextMessageAsync(message.Chat.Id, $"Твій шедевр в черзі. Чекай", messageThreadId: message.MessageThreadId, replyToMessageId: message.MessageId);
                }
                catch (InvalidOperationException ex)
                {
                    await _client.SendTextMessageAsync(message.Chat.Id, ex.Message);
                }
            }
        }

        internal async void JobFailed(GenerationJob obj, string error)
        {
            await _client.EditMessageTextAsync(obj.BotMessage.Chat.Id, obj.BotMessage.MessageId, "Помилка: " + error);
            Directory.Delete(obj.TmpDir, true);
        }

        internal async void JobFinished(GenerationJob obj)
        {
            if (await OriginalMessageExists(obj.OriginalChatId, obj.OriginalMessageId))
            {
                using (var streams = new StreamList(obj.Images.Select(x => System.IO.File.OpenRead(x))))
                {
                    var media = new List<IAlbumInputMedia>();

                    foreach (var stream in streams)
                    {
                        dynamic mediaInputMedia;
                        if (obj.AsFile)
                        {
                            mediaInputMedia = new InputMediaDocument(InputFile.FromStream(stream, Path.GetFileName(stream.Name)));
                        }
                        else
                        {
                            mediaInputMedia = new InputMediaPhoto(InputFile.FromStream(stream, Path.GetFileName(stream.Name)));
                        }

                        mediaInputMedia.Caption = Path.GetFileName(stream.Name);

                        media.Add(mediaInputMedia);

                        
                    }

                    var message = await _client.SendMediaGroupAsync(new ChatId(obj.OriginalChatId), media, messageThreadId: obj.OriginalMessageThreadId, replyToMessageId: obj.OriginalMessageId);

                    if (obj.PostInfo)
                    {
                        var info = string.Join('\n', streams.Select(x => $"```{Path.GetFileName(x.Name)}\nRender time {obj.Elapsed}\n{System.IO.File.ReadAllText(x.Name + ".txt")}```"));
                        await _client.SendTextMessageAsync(new ChatId(obj.OriginalChatId), info, messageThreadId: obj.OriginalMessageThreadId, replyToMessageId: obj.OriginalMessageId, parseMode: ParseMode.MarkdownV2);    
                    }
                }
                await _client.DeleteMessageAsync(obj.BotMessage.Chat.Id, obj.BotMessage.MessageId);
            }
            else
            {
                await _client.EditMessageTextAsync(obj.BotMessage.Chat.Id, obj.BotMessage.MessageId, "Запит було видалено");
            }


            Directory.Delete(obj.TmpDir, true);
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
