
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Commands.CallbackDataTypes;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGeneration.Exceptions;
using ServiceKeyAttribute = TelegramMultiBot.Commands.ServiceKeyAttribute;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    [ServiceKey("imagine")]
    internal class ImagineCommand : BaseCommand, ICallbackHandler, IInlineQueryHandler
    {
        private readonly TelegramClientWrapper _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImagineCommand> _logger;
        private readonly ImageGenearatorQueue _imageGenearatorQueue;
        private readonly IDatabaseService _databaseService;

        //private readonly IServiceProvider _serviceProvider;

        public ImagineCommand(TelegramClientWrapper client, IConfiguration configuration, ILogger<ImagineCommand> logger, ImageGenearatorQueue imageGenearatorQueue, IDatabaseService databaseService)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
            _imageGenearatorQueue = imageGenearatorQueue;
            _databaseService = databaseService;
            //_serviceProvider = serviceProvider;
        }

        public override async Task Handle(Message message)
        {
            if (message.Text == "/imagine" || message.Text == $"/imagine@{BotService.BotName}")
            {
                var markup = new ForceReplyMarkup
                {
                    InputFieldPlaceholder = "/imagine cat driving a bike",
                    Selective = true
                };

                var reply =
@"Привіт, я бобер\-художник, і я сприймаю повідомлення в наступному форматі 
`/imagine cat driving a bike`
Щоб дізнатися більше /help";

                using var stream = new MemoryStream(Properties.Resources.artist);
                var photo = InputFile.FromStream(stream, "beaver.png");
                await _client.SendPhotoAsync(message, photo, reply, markup: markup, parseMode: ParseMode.MarkdownV2);
            }
            else
            {
                await AddJobToTheQueue(message, CreateMessageData(message));
            }
        }

        private MessageData CreateMessageData(Message message)
        {
            ArgumentNullException.ThrowIfNull(message.Text);
            ArgumentNullException.ThrowIfNull(message.From);

            return new MessageData()
            {
                ChatId = message.Chat.Id,
                JobType = JobType.Text2Image,
                MessageId = message.MessageId,
                MessageThreadId = message.MessageThreadId,
                Text = message.Text[message.Text.IndexOf("/" + Command)..],
                UserId = message.From.Id
            };
        }

        private static CallbackData CreateCallbackData(CallbackQuery query, ImagineCallbackData data)
        {
            var message = query.Message as Message;
            ArgumentNullException.ThrowIfNull(message);

            _ = Guid.TryParse(data.Id, out var guid);
            return new CallbackData()
            {
                ChatId = message.Chat.Id,
                JobType = Enum.Parse<JobType>(data.JobType.ToString()),
                MessageId = message.MessageId,
                MessageThreadId = message.MessageThreadId,
                UserId = query.From.Id,
                PreviousJobResultId = guid,
                Upscale = data.Upscale,
            };
        }

        private InlineKeyboardMarkup? GetReplyMarkupForJob(ImagineCallbackData callbackData, string? prompt = null)
        {
            if (callbackData.Id is null)
                throw new NullReferenceException(nameof(callbackData.Id));

            return GetReplyMarkupForJob(callbackData.JobType, callbackData.Id, callbackData.Upscale, prompt);
        }

        private InlineKeyboardMarkup? GetReplyMarkupForJob(JobType type, string id, double? upscale, string prompt)
        {
            if (Enum.TryParse<ImagineCommands>(type.ToString(), out var s))
            {
                return GetReplyMarkupForJob(s, id, upscale, prompt);
            }
            return null;
        }

        private InlineKeyboardMarkup? GetReplyMarkupForJob(ImagineCommands type, string id, double? upscale, string? prompt = null)
        {
            //InlineKeyboardButton repeat = InlineKeyboardButton.WithCallbackData("Повторити", new ImagineCallbackData(Command, ImagineCommands.Repeat));
            InlineKeyboardButton original = InlineKeyboardButton.WithCallbackData("Оригінал", new ImagineCallbackData(Command, ImagineCommands.Original, id));
            InlineKeyboardButton hiresFix = InlineKeyboardButton.WithCallbackData($"Hires Fix", new ImagineCallbackData(Command, ImagineCommands.HiresFix, id, 0));
            InlineKeyboardButton upscale2 = InlineKeyboardButton.WithCallbackData("Upscale x2", new ImagineCallbackData(Command, ImagineCommands.Upscale, id, 2));
            InlineKeyboardButton upscale4 = InlineKeyboardButton.WithCallbackData("Upscale x4", new ImagineCallbackData(Command, ImagineCommands.Upscale, id, 4));
            InlineKeyboardButton info = InlineKeyboardButton.WithCallbackData("Інфо", new ImagineCallbackData(Command, ImagineCommands.Info, id, upscale));
            InlineKeyboardButton actions = InlineKeyboardButton.WithCallbackData("Кнопоцькі тиць", new ImagineCallbackData(Command, ImagineCommands.Actions, id));
            InlineKeyboardButton noise = InlineKeyboardButton.WithCallbackData("Шум", new ImagineCallbackData(Command, ImagineCommands.Noise, id));
            InlineKeyboardButton vingette = InlineKeyboardButton.WithCallbackData("Віньєтка", new ImagineCallbackData(Command, ImagineCommands.Vingette, id));
            InlineKeyboardButton copyPrompt = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("Змінити запит", prompt is null ? string.Empty : prompt);

            switch (type)
            {
                case ImagineCommands.Text2Image:
                    return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
                    {
                        actions, copyPrompt
                    });
                case ImagineCommands.HiresFix:
                    return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>()
                    {
                        new List<InlineKeyboardButton>
                        {
                            info,
                            upscale2,
                        },
                        new List<InlineKeyboardButton>()
                        {
                             vingette, noise
                        }
                    });
                case ImagineCommands.Upscale:
                    {
                        return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>()
                        {
                            new List<InlineKeyboardButton>
                            {
                                info
                            },
                            new List<InlineKeyboardButton>()
                            {
                                 vingette, noise
                            }
                        });
                    }
                case ImagineCommands.Info:
                    {
                        if (upscale == null) //original render
                        {
                            return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>()
                            {
                                new List<InlineKeyboardButton>()
                                {
                                    original,
                                    copyPrompt
                                },
                                new List<InlineKeyboardButton>()
                                {
                                    hiresFix,
                                    upscale2,
                                    upscale4
                                },
                                new List<InlineKeyboardButton>()
                                {
                                     vingette, noise
                                }
                            });
                        }
                        else if (upscale == 0) // hires fix
                        {
                            return new InlineKeyboardMarkup(upscale2);
                        }

                        return null;
                    }
                case ImagineCommands.Original:
                    return null;
                case ImagineCommands.Actions:
                    return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
                    {
                        new List<InlineKeyboardButton>
                        {
                             info,
                             original,
                             copyPrompt,
                        },
                        new List<InlineKeyboardButton>
                        {
                            hiresFix,
                            upscale2,
                            upscale4
                        },
                        new List<InlineKeyboardButton>
                        {
                             vingette, noise
                        }
                    });
                case ImagineCommands.Vingette:
                case ImagineCommands.Noise:
                    return new InlineKeyboardMarkup(new List<InlineKeyboardButton>()
                    {
                        vingette, noise
                    });
                default:
                    return default;
            }

        }


        



        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            var callbackData = ImagineCallbackData.FromString(callbackQuery.Data);
            if (callbackData.Id is null)
                throw new NullReferenceException(nameof(callbackData.Id));


            switch (callbackData.JobType)
            {
                case ImagineCommands.Info:
                    {
                        var result = _databaseService.GetJobResult(callbackData.Id);

                        if (result == null)
                        {
                            await _client.AnswerCallbackQueryAsync(callbackData.Id, "Це дуже стара картинка. Інформаця про неї загубилась", true);
                            //await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Це дуже стара картинка. Інформаця про неї загубилась", showAlert: true);
                            return;
                        }
                        else
                        {
                            await _client.AnswerCallbackQueryAsync(callbackData.Id, "Інформацію знайдено", true);

                            var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));
                            var replyMessage = message.ReplyToMessage ?? throw new NullReferenceException(nameof(message.ReplyToMessage));

                            if (replyMessage.Text is null)
                                throw new NullReferenceException(nameof(replyMessage.Text));


                            var prompt = replyMessage.Text[replyMessage.Text.IndexOf("/" + Command)..];
                            var keys = GetReplyMarkupForJob(callbackData, prompt: prompt);
                            InputMedia? media = default;
                            if (message.Type == MessageType.Photo)
                            {
                                var photo = (message.Photo ?? throw new NullReferenceException(nameof(message.Photo))).Last();
                                media = new InputMediaPhoto() { Media = InputFile.FromFileId(photo.FileId) };
                                media.Caption = $"#seed:{result.Seed}\nRender time: {TimeSpan.FromMilliseconds(result.RenderTime)}\n{result.Info}";
                            }
                            else if (message.Type == MessageType.Document)
                            {
                                var document = message.Document ?? throw new NullReferenceException(nameof(message.Document));
                                media = new InputMediaDocument() { Media = InputFile.FromFileId(document.FileId) };
                                media.Caption = $"Render time: {TimeSpan.FromMilliseconds(result.RenderTime)}\n{result.Info}";
                            }
                            else
                            {
                                throw new NullReferenceException(nameof(media));
                            }

                            if (media.Caption.Length > 1024)
                            {
                                
                                await _client.EditMessageReplyMarkupAsync(message, keys);

                                //await _client.EditMessageReplyMarkupAsync(message.Chat.Id, message.MessageId, keys);

                                await _client.SendMessageAsync(message, media.Caption, true, keys);
                                //await _client.SendTextMessageAsync(message.Chat.Id, media.Caption, replyMarkup: keys, replyToMessageId: message.MessageId);
                            }
                            else
                            {
                                var editRequest = new EditMessageMediaRequest()
                                {
                                    ChatId = message.Chat,
                                    MessageId = message.MessageId,
                                    ReplyMarkup = keys,
                                    Media = media
                                };
                                await _client.EditMessageMediaAsync(message, media, keys);
                            }
                        }
                        return;

                    }
                case ImagineCommands.Original:
                    {
                        if (callbackData.Id is null)
                            throw new ArgumentException("id");

                        var result = _databaseService.GetJobResult(callbackData.Id);
                        var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));

                        if (result == null)
                        {
                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Це дуже стара каринка, бобер загубив оригінал", true);
                            //await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Це дуже стара каринка, бобер загубив оригінал", showAlert: true);
                            return;
                        }

                        using (var stream = System.IO.File.OpenRead(result.FilePath))
                        {
                            var media = InputFile.FromStream(stream, Path.GetFileName(result.FilePath));
                            await _client.SendDocumentAsync(message, media, reply: true);
                        }

                        await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завантажую оригінал");
                        return;
                    }
                case ImagineCommands.Actions:
                    {
                        if (callbackData.Id is null)
                            throw new ArgumentException("id");

                        await _client.AnswerCallbackQueryAsync(callbackQuery.Id);

                        var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));
                        var replyMessage = message.ReplyToMessage ?? throw new NullReferenceException(nameof(message.ReplyToMessage));
                        if (replyMessage.Text is null)
                            throw new NullReferenceException(nameof(replyMessage.Text));


                        var prompt = replyMessage.Text[replyMessage.Text.IndexOf("/" + Command)..];
                        var keys = GetReplyMarkupForJob(callbackData, prompt);
                        var request = new EditMessageReplyMarkupRequest()
                        {
                            ChatId = message.Chat,
                            MessageId = message.MessageId,
                            ReplyMarkup = keys
                        };

                        return;
                    }
                case ImagineCommands.HiresFix:
                case ImagineCommands.Upscale:
                case ImagineCommands.Vingette:
                case ImagineCommands.Noise:

                    {
                        var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));

                        await _client.AnswerCallbackQueryAsync(callbackQuery.Id);
                        await AddJobToTheQueue(message, CreateCallbackData(callbackQuery, callbackData));

                        break;
                    }
                case ImagineCommands.Repeat:
                    {
                        var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));
                        var replyMessage = message.ReplyToMessage ?? throw new NullReferenceException(nameof(message.ReplyToMessage));

                        await AddJobToTheQueue(replyMessage, CreateMessageData(replyMessage));
                        await _client.AnswerCallbackQueryAsync(callbackQuery.Id);

                        break;
                    }
                default:
                    break;
            }

        }

        private async Task AddJobToTheQueue(Message message, IInputData data)
        {
            Message botMessage;
            try
            {
                botMessage = await _client.SendMessageAsync(message, "Відправляю", replyToMessage: true);
            }
            catch (Exception ex)
            {
                _logger.LogError("AddJobToTheQueue" + ex.Message, ex);
                throw;
            }

            data.BotMessageId = botMessage.MessageId;

            try
            {
                _imageGenearatorQueue.AddJob(data);
                await _client.EditMessageTextAsync(botMessage, "Твій шедевр в черзі. Чекай");
            }
            catch (OldJobException ex)
            {
                await _client.EditMessageTextAsync(botMessage, ex.Message);
            }
            catch (AlreadyRunningException ex)
            {
                await _client.EditMessageTextAsync(botMessage, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Adding in the queue:" + ex.Message);
                await _client.EditMessageTextAsync(botMessage, "Не можу додати в чергу - в мене лапки :(");

            }
        }


        internal async Task JobFailed(JobInfo job, Exception exception)
        {
            switch (exception)
            {
                case SdNotAvailableException:
                    {
                        using (var stream = new MemoryStream(Properties.Resources.asleep))
                        {
                            var photo = InputFile.FromStream(stream, "beaver.png");
                            await _client.SendPhotoAsync(job, photo, exception.Message);
                        }
                        await _client.DeleteMessageAsync(job.ChatId, job.BotMessageId);
                        break;
                    }
                case InputException:
                    await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Помилка в запиті. " + exception.Message + ". Перевір свій запит та спробуй ще");
                    break;
                case RenderFailedException:
                    {
                        var keys = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Спробувати ще", new ImagineCallbackData(Command, ImagineCommands.Repeat)));

                        await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Рендер невдалий. Спробуйте ще", keys);
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



        internal async Task JobFinished(JobInfo inputJob)
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
                    var item = job.Results.Single(x => stream.Name.Contains(x.FilePath));

                    string? info = null;
                    if (job.PostInfo)
                    {
                        info = item.Info;
                    }

                    var keys = GetReplyMarkupForJob(job.Type, item.Id.ToString(), job.UpscaleModifyer, prompt: job.Text);

                    switch (job.Type)
                    {
                        case JobType.Vingette:
                        case JobType.Noise:
                        case JobType.Upscale:
                        case JobType.HiresFix:
                            {
                                await _client.SendDocumentAsync(job, media, info, true, keys);
                                //await _client.SendDocumentAsync(job.ChatId, media, caption: info, replyParameters: new ReplyParameters() { MessageId = job.MessageId }, replyMarkup: keys);
                                break;
                            }

                        case JobType.Text2Image:
                            {
                                await _client.SendPhotoAsync(job, media, info, true, keys);
                                //await _client.SendPhotoAsync(job.ChatId, media, caption: info, replyToMessageId: job.MessageId, replyMarkup: keys);
                                break;
                            }
                        default:
                            break;
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

        public async Task HandleInlineQuery(InlineQuery inlineQuery)
        {
            await _client.AnswerInlineQueryAsync(inlineQuery.Id);
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
