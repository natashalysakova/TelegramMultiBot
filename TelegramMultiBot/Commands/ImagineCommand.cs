
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands;
using TelegramMultiBot.ImageGeneration;
using TelegramMultiBot.ImageGeneration.Exceptions;
using ServiceKeyAttribute = TelegramMultiBot.Commands.ServiceKeyAttribute;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    [ServiceKey("imagine")]

    internal class ImagineCommand : BaseCommand, ICallbackHandler
    {
        private readonly TelegramBotClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImagineCommand> _logger;
        private readonly ImageGenearatorQueue _imageGenearatorQueue;
        private readonly IServiceProvider _serviceProvider;

        public ImagineCommand(TelegramBotClient client, IConfiguration configuration, ILogger<ImagineCommand> logger, ImageGenearatorQueue imageGenearatorQueue, IServiceProvider serviceProvider)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
            _imageGenearatorQueue = imageGenearatorQueue;
            _serviceProvider = serviceProvider;
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
                    var botMessage = await _client.SendTextMessageAsync(message.Chat.Id, $"Твій шедевр в черзі. Чекай", messageThreadId: message.MessageThreadId, replyToMessageId: message.MessageId);
                    _imageGenearatorQueue.AddJob(message, botMessage.MessageId);

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

            if (callbackData.Data.ElementAt(0) == JobType.Seed.ToString())
            {
                var databaseService = _serviceProvider.GetService<ImageDatabaseService>();
                var result = databaseService.GetJobResult(callbackData.Data.ElementAt(1));


                var media = new InputMediaPhoto(InputFile.FromFileId(callbackQuery.Message.Photo.Last().FileId));
                if(result == null)
                {
                    media.Caption = "Це дуже стара картинка. Інформаця про неї загубилась";
                }
                else
                {
                    long seed = new UpscaleParams(result).Seed;
                    media.Caption = $"#seed:{seed}";
                }

                var keys = new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData("Original", new CallbackData(Command, $"{JobType.Original}|{result.Id}").DataString),
                    InlineKeyboardButton.WithCallbackData($"Hires Fix", new CallbackData(Command, $"{JobType.HiresFix}|{result.Id}").DataString),
                    InlineKeyboardButton.WithCallbackData("Upscale x2", new CallbackData(Command, $"{JobType.Upscale}|{result.Id}|2").DataString),
                    InlineKeyboardButton.WithCallbackData("Upscale x4", new CallbackData(Command, $"{JobType.Upscale}|{result.Id}|4").DataString)
                };

                await _client.EditMessageMediaAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, media, new InlineKeyboardMarkup(keys));
                return;
            }

            if(callbackData.Data.ElementAt(0) == JobType.Original.ToString())
            {
                var databaseService = _serviceProvider.GetService<ImageDatabaseService>();
                var result = databaseService.GetJobResult(callbackData.Data.ElementAt(1));
                var message = callbackQuery.Message;

                if(result == null)
                {
                    await _client.SendTextMessageAsync(message.Chat.Id, "Це дуже стара каринка, бобер загубив оригінал", message.MessageThreadId, replyToMessageId: message.MessageId);
                    return;
                }

                using(var stream = System.IO.File.OpenRead(result.FilePath))
                {
                    var media = InputFile.FromStream(stream, Path.GetFileName(result.FilePath));
                    await _client.SendDocumentAsync(message.Chat.Id, media, messageThreadId: message.MessageThreadId, replyToMessageId: message.MessageId);
                }

                return;
            }

            Message botMessage = default;
            try
            {
                botMessage = await _client.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Картинка в черзі. Чекай", messageThreadId: callbackQuery.Message.MessageThreadId, replyToMessageId: callbackQuery.Message.MessageId);
                _imageGenearatorQueue.AddJob(callbackQuery, botMessage.MessageId);
            }
            catch (Exception ex)
            {
                if (botMessage != null)
                {
                    await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, ex.Message);
                }
                throw;
            }

            //else
            //{
            //    await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Unknown command");
            //}
        }


        internal async void JobFailed(ImageJob job, Exception exception)
        {
            switch (exception)
            {
                case SdNotAvailableException:
                    {
                        using (var stream = new MemoryStream(Properties.Resources.asleep))
                        {
                            var photo = InputFile.FromStream(stream, "beaver.png");
                            await _client.SendPhotoAsync(job.ChatId, photo, messageThreadId: job.MessageThreadId, caption: exception.Message);
                        }
                        await _client.DeleteMessageAsync(job.ChatId, job.BotMessageId);
                        break;
                    }
                case InputException:
                    await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Помилка в запиті. " + exception.Message + ". Перевір свій запит та спробуй ще");
                    break;
                case RenderFailedException:
                    {
                        await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Рендер невдалий. Спробуйте ще");
                        break;
                    }
                case AlreadyRunningException:
                    {
                        await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, exception.Message);
                        break;
                    }
                default:
                    await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Невідома помилка");
                    _logger.LogError(exception, "JobFailed Exception");
                    //Directory.Delete(obj.TmpDir, true);
                    break;
            }

        }

        internal async void JobFinished(ImageJob inputJob)
        {

            var job = inputJob;

            if (!await OriginalMessageExists(job.ChatId, job.MessageId))
            {
                await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Запит було видалено");
                return;
            }

            using (var streams = new StreamList(job.Results.Select(x => System.IO.File.OpenRead(x.FilePath))))
            {
                foreach (var stream in streams)
                {
                    var media = InputFile.FromStream(stream, Path.GetFileName(stream.Name));
                    var item = job.Results.Single(x => x.FilePath == stream.Name);

                    string? info = null;
                    if (job.PostInfo)
                    {
                        info = item.Info;
                    }

                    switch (job.Type)
                    {
                        case JobType.Upscale:
                            {
                                
                                //var keys = new List<InlineKeyboardButton>
                                //{
                                //    //InlineKeyboardButton.WithCallbackData("Original", new CallbackData(Command, $"{JobType.Original}|{item.Id}").DataString)
                                //};

                                //await _client.SendPhotoAsync(job.ChatId, media, messageThreadId: job.MessageThreadId, caption: info, replyToMessageId: job.MessageId, replyMarkup: new InlineKeyboardMarkup(keys));
                                await _client.SendDocumentAsync(job.ChatId, media, messageThreadId: job.MessageThreadId, caption: info, replyToMessageId: job.MessageId);

                                break;
                            }
                        case JobType.HiresFix:
                            {
                                var keys = new List<InlineKeyboardButton>
                                {
                                    //InlineKeyboardButton.WithCallbackData("Original", new CallbackData(Command, $"{JobType.Original}|{item.Id}").DataString),
                                    InlineKeyboardButton.WithCallbackData("Upscale x2", new CallbackData(Command, $"{JobType.Upscale}|{item.Id}|2").DataString),
                                    //InlineKeyboardButton.WithCallbackData("Upscale x4", new CallbackData(Command, $"{JobType.Upscale}|{item.Id}|4").DataString)

                                };
                                //await _client.SendPhotoAsync(job.ChatId, media, messageThreadId: job.MessageThreadId, caption: info, replyToMessageId: job.MessageId, replyMarkup: new InlineKeyboardMarkup(keys));

                                await _client.SendDocumentAsync(job.ChatId, media, messageThreadId: job.MessageThreadId, caption: info, replyToMessageId: job.MessageId, replyMarkup: new InlineKeyboardMarkup(keys));
                                break;
                            }

                        case JobType.Text2Image:
                            {

                                var keys = new List<List<InlineKeyboardButton>>
                                {
                                    new List<InlineKeyboardButton>
                                    {
                                         InlineKeyboardButton.WithCallbackData("Original", new CallbackData(Command, $"{JobType.Original}|{item.Id}").DataString),
                                    },
                                    new List<InlineKeyboardButton>
                                    {
                                        InlineKeyboardButton.WithCallbackData($"Hires Fix", new CallbackData(Command, $"{JobType.HiresFix}|{item.Id}").DataString),
                                        InlineKeyboardButton.WithCallbackData("Upscale x2", new CallbackData(Command, $"{JobType.Upscale}|{item.Id}|2").DataString),
                                        InlineKeyboardButton.WithCallbackData("Upscale x4", new CallbackData(Command, $"{JobType.Upscale}|{item.Id}|4").DataString)
                                    }
                                };

                                if (!job.PostInfo)
                                {
                                    keys[0].Insert(0,InlineKeyboardButton.WithCallbackData("Seed", new CallbackData(Command, $"{JobType.Seed}|{item.Id}").DataString));
                                }


                                await _client.SendPhotoAsync(job.ChatId, media, messageThreadId: job.MessageThreadId, caption: info, replyToMessageId: job.MessageId, replyMarkup: new InlineKeyboardMarkup(keys));
                                break;
                            }
                    }
                }
            }

            await _client.DeleteMessageAsync(job.ChatId, job.BotMessageId);
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
