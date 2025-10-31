using ImageMagick;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Net.WebSockets;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands.CallbackDataTypes;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGeneration.Exceptions;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.Commands;

[ServiceKey("imagine", "Бобер-художник")]
internal class ImagineCommand(TelegramClientWrapper client, ISqlConfiguationService configuration, IConfiguration appSettings, ILogger<ImagineCommand> logger, ImageGenearatorQueue imageGenearatorQueue, IImageDatabaseService databaseService) : BaseCommand, ICallbackHandler, IInlineQueryHandler
{
    private static string imagineCommand = "imagine";

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
            await client.SendPhotoAsync(message, photo, reply, markup: markup, parseMode: ParseMode.MarkdownV2);
        }
        else
        {
            await AddJobToTheQueue(message, await CreateMessageData(message));
        }
    }

    private MessageData CreateMessageData(JobInfo job, long userId)
    {

        return new MessageData()
        {
            ChatId = job.ChatId,
            JobType = job.Type,
            MessageId = job.MessageId,
            MessageThreadId = job.MessageThreadId,
            Text = job.Text,
            UserId = userId
        };
    }

    private async Task<MessageData> CreateMessageData(Message message)
    {
        ArgumentNullException.ThrowIfNull(message.Text);
        ArgumentNullException.ThrowIfNull(message.From);

        var jobType = JobType.Text2Image;
        string? inputImage = default;
        if (message.ReplyToMessage != null && message.ReplyToMessage.Type == MessageType.Photo && message.Text.Contains("#face"))
        {
            jobType = JobType.Text2ImageFaceId;
            inputImage = await DownloadImage(message.ReplyToMessage.Photo.Last().FileId);
        }

        return new MessageData()
        {
            ChatId = message.Chat.Id,
            JobType = jobType,
            MessageId = message.MessageId,
            MessageThreadId = message.MessageThreadId,
            Text = message.Text[message.Text.IndexOf("/" + Command)..],
            UserId = message.From.Id,
            InputImage = inputImage
        };
    }

    private async Task<string> DownloadImage(string fileId)
    {
        var url = await client.GetFileUrl(fileId);
        var basedir = configuration.IGSettings.BaseImageDirectory;
        var downloadDir = configuration.IGSettings.DownloadDirectory;
        var dir = Path.Combine(basedir, downloadDir);
        var filename = $"{Guid.NewGuid()}{Path.GetExtension(url)}";
        var dest = Path.Combine(dir, filename);

        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await client.DownloadFile(url, dest);
        return dest;
    }

    private static CallbackData CreateCallbackData(CallbackQuery query, ImagineCallbackData data)
    {
        var message = query.Message as Message;
        ArgumentNullException.ThrowIfNull(message);

        _ = Guid.TryParse(data.JobId, out var guid);
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
        if (callbackData.JobId is null)
            throw new NullReferenceException(nameof(callbackData.JobId));

        return GetReplyMarkupForJob(callbackData.JobType, callbackData.JobId, callbackData.Upscale, prompt);
    }

    public static InlineKeyboardMarkup? GetReplyMarkupForJob(JobType type, string id, double? upscale, string? prompt)
    {
        if (Enum.TryParse<ImagineCommands>(type.ToString(), out var s))
        {
            return GetReplyMarkupForJob(s, id, upscale, prompt);
        }
        return null;
    }

    public static InlineKeyboardMarkup? GetOldJobMarkup(string id, string? prompt)
    {
        InlineKeyboardButton repeat = InlineKeyboardButton.WithCallbackData("🔄 Повторити", new ImagineCallbackData(imagineCommand, ImagineCommands.Repeat, id));
        InlineKeyboardButton copyPrompt = InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("📝 Змінити запит", prompt is null ? string.Empty : prompt);

        return new InlineKeyboardMarkup(new List<InlineKeyboardButton> { repeat, copyPrompt });
    }

    public static InlineKeyboardMarkup? GetDeletedMarkup(string id)
    {
        InlineKeyboardButton actions = InlineKeyboardButton.WithCallbackData("Кнопоцькі тиць", new ImagineCallbackData(imagineCommand, ImagineCommands.Actions, id));
        return new InlineKeyboardMarkup(new List<InlineKeyboardButton> { actions });
    }

    public static InlineKeyboardMarkup? GetReplyMarkupForJob(ImagineCommands type, string id, double? upscale, string? prompt = null)
    {
        InlineKeyboardButton repeat = InlineKeyboardButton.WithCallbackData("🔄 Повторити", new ImagineCallbackData(imagineCommand, ImagineCommands.Repeat, id));
        InlineKeyboardButton original = InlineKeyboardButton.WithCallbackData("⤵️ Оригінал", new ImagineCallbackData(imagineCommand, ImagineCommands.Original, id));
        InlineKeyboardButton hiresFix = InlineKeyboardButton.WithCallbackData($"🔧 HiresFix", new ImagineCallbackData(imagineCommand, ImagineCommands.HiresFix, id, 0));
        InlineKeyboardButton upscale2 = InlineKeyboardButton.WithCallbackData("Upscale ⬆️2️⃣", new ImagineCallbackData(imagineCommand, ImagineCommands.Upscale, id, 2));
        InlineKeyboardButton upscale4 = InlineKeyboardButton.WithCallbackData("Upscale ⬆️4️⃣", new ImagineCallbackData(imagineCommand, ImagineCommands.Upscale, id, 4));
        InlineKeyboardButton info = InlineKeyboardButton.WithCallbackData("ℹ️ Інфо", new ImagineCallbackData(imagineCommand, ImagineCommands.Info, id, upscale));
        //InlineKeyboardButton actions = InlineKeyboardButton.WithCallbackData("Кнопоцькі тиць", new ImagineCallbackData(imagineCommand, ImagineCommands.Actions, id));
        //InlineKeyboardButton noise = InlineKeyboardButton.WithCallbackData("Шум", new ImagineCallbackData(imagineCommand, ImagineCommands.Noise, id));
        //InlineKeyboardButton vingette = InlineKeyboardButton.WithCallbackData("Віньєтка", new ImagineCallbackData(imagineCommand, ImagineCommands.Vingette, id));
        InlineKeyboardButton? copyPrompt = prompt != null ? InlineKeyboardButton.WithSwitchInlineQueryCurrentChat("📝 Змінити", prompt) : null;

        switch (type)
        {
            case ImagineCommands.Text2Image:
                if (copyPrompt != null)
                {


                    return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
                    {
                        new() {
                             //info,
                             original,
                            copyPrompt,
                            repeat
                        },
                        new() {
                            hiresFix,
                            upscale2,
                            upscale4
                        },
                        //new() {
                        //     vingette, noise
                        //}
                    });
                }
                else
                {
                    return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
                    {
                        new() {
                             //info,
                             original                            },
                        new() {
                            hiresFix,
                            upscale2,
                            upscale4
                        },
                        //new() {
                        //     vingette, noise
                        //}
                    });
                }

            case ImagineCommands.HiresFix:
                return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>()
                {
                    new() {
                        info,
                        upscale2,
                    },
                    //new()
                    //{
                    //     vingette, noise
                    //}
                });

            case ImagineCommands.Upscale:
                {
                    return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>()
                    {
                        new() {
                            info
                        },
                        //new()
                        //{
                        //     vingette, noise
                        //}
                    });
                }
            case ImagineCommands.Info:
                {
                    if (upscale == null) //original render
                    {
                        if (copyPrompt != null)
                        {
                            return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>()
                        {
                            new()
                            {
                                original,
                                copyPrompt,
                                repeat
                            },
                            new()
                            {
                                hiresFix,
                                upscale2,
                                upscale4
                            },
                            //new()
                            //{
                            //     vingette, noise
                            //}
                        });
                        }
                        else
                        {
                            return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>()
                            {
                                new()
                                {
                                    original,
                                },
                                new()
                                {
                                    hiresFix,
                                    upscale2,
                                    upscale4
                                },
                                //new()
                                //{
                                //     vingette, noise
                                //}
                            });
                        }

                    }
                    else if (upscale == 0) // hires fix
                    {
                        return new InlineKeyboardMarkup(upscale2);
                    }

                    return null;
                }
            case ImagineCommands.Original:
                return null;

            //case ImagineCommands.Actions:
            //    if (copyPrompt != null)
            //    {


            //        return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
            //        {
            //            new() {
            //                 info,
            //                 original
            //            },
            //            new()
            //            {
            //                copyPrompt,
            //                repeat
            //            },
            //            new() {
            //                hiresFix,
            //                upscale2,
            //                upscale4
            //            },
            //            new() {
            //                 vingette, noise
            //            }
            //        });
            //    }
            //    else
            //    {
            //        return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>
            //        {
            //            new() {
            //                 info,
            //                 original                            },
            //            new() {
            //                hiresFix,
            //                upscale2,
            //                upscale4
            //            },
            //            new() {
            //                 vingette, noise
            //            }
            //        });
            //    }

            case ImagineCommands.Vingette:
            case ImagineCommands.Noise:
                return new InlineKeyboardMarkup(new List<List<InlineKeyboardButton>>()
                    {
                        new() {
                            info
                        },
                        //new()
                        //{
                        //     vingette, noise
                        //}
                    });

            default:
                return default;
        }
    }

    public async Task HandleCallback(CallbackQuery callbackQuery)
    {
        var callbackData = ImagineCallbackData.FromString(callbackQuery.Data);
        if (callbackData.JobId is null)
            throw new NullReferenceException(nameof(callbackData.JobId));

        var result = databaseService.GetJobResult(callbackData.JobId);
        if (result == null && callbackData.JobType != ImagineCommands.Repeat)
        {
            await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Це дуже стара каринка, бобер загубив усьо :(", true);
            await HandleOldJobMessage(callbackQuery, callbackData);
            return;
        }

        switch (callbackData.JobType)
        {
            case ImagineCommands.Info:
                {
                    await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Інформацію знайдено");

                    var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));

                    //var replyMessage = message.ReplyToMessage;
                    //InlineKeyboardMarkup? keys;
                    //if (replyMessage != null)
                    //{
                    //    var prompt = replyMessage.Text?[replyMessage.Text.IndexOf("/" + Command)..];
                    //    keys = GetReplyMarkupForJob(callbackData, prompt);

                    //}
                    //else
                    //{
                    var keys = GetReplyMarkupForJob(callbackData);
                    //}


                    InputMedia? media;
                    if (message.Type == MessageType.Photo)
                    {
                        var photo = (message.Photo ?? throw new NullReferenceException(nameof(message.Photo))).Last();
                        media = new InputMediaPhoto
                        {
                            Media = InputFile.FromFileId(photo.FileId),
                            Caption = $"#seed:{result.Seed}\nRender time: {TimeSpan.FromMilliseconds(result.RenderTime)}\n{result.Info}"
                        };
                    }
                    else if (message.Type == MessageType.Document)
                    {
                        var document = message.Document ?? throw new NullReferenceException(nameof(message.Document));
                        media = new InputMediaDocument
                        {
                            Media = InputFile.FromFileId(document.FileId),
                            Caption = $"Render time: {TimeSpan.FromMilliseconds(result.RenderTime)}\n{result.Info}"
                        };
                    }
                    else
                    {
                        throw new NullReferenceException(nameof(media));
                    }

                    if (media.Caption.Length > 1024)
                    {
                        await client.EditMessageReplyMarkupAsync(message, keys);
                        await client.SendMessageAsync(message, media.Caption, true, keys);
                    }
                    else
                    {
                        await client.EditMessageMediaAsync(message, media, keys);
                    }
                    return;
                }
            case ImagineCommands.Original:
                {
                    AddWatermark(result);
                    var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));
                    using (var stream = System.IO.File.OpenRead(result.FilePath))
                    {
                        var media = InputFile.FromStream(stream, Path.GetFileName(result.FilePath));
                        await client.SendDocumentAsync(message, media, reply: true);
                    }

                    await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завантажую оригінал");
                    return;
                }
            case ImagineCommands.Actions:
                {
                    await client.AnswerCallbackQueryAsync(callbackQuery.Id);

                    var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));
                    InlineKeyboardMarkup? keys;
                    if (message.ReplyToMessage is null)
                    {
                        keys = GetReplyMarkupForJob(callbackData);
                    }
                    else
                    {
                        var prompt = databaseService.GetJobByResultId(result.Id).Text;
                        keys = GetReplyMarkupForJob(callbackData, prompt);
                    }

                    await client.EditMessageReplyMarkupAsync(message, keys);
                    return;
                }
            case ImagineCommands.HiresFix:
            case ImagineCommands.Upscale:
            case ImagineCommands.Vingette:
            case ImagineCommands.Noise:
                {
                    var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));

                    await AddJobToTheQueue(message, CreateCallbackData(callbackQuery, callbackData));
                    await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Додано в чергу");

                    break;
                }
            case ImagineCommands.Repeat:
                {
                    var message = callbackQuery.Message as Message ?? throw new NullReferenceException(nameof(callbackQuery.Message));



                    var previousJob = databaseService.GetJobByResultId(callbackData.JobId);
                    if (previousJob is null && message.ReplyToMessage == null)
                    {
                        await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Цей во, я забув шо я там малював :(", true);
                        await HandleDeletedMessage(callbackQuery, callbackData);
                        break;
                    }
                    else if (message.ReplyToMessage != null)
                    {
                        await AddJobToTheQueue(message, await CreateMessageData(message.ReplyToMessage));
                    }
                    else
                    {
                        await AddJobToTheQueue(message, CreateMessageData(previousJob, callbackQuery.From.Id));
                    }
                    await client.AnswerCallbackQueryAsync(callbackQuery.Id, "Додано в чергу");
                    break;
                }
            default:
                break;
        }
    }

    private async Task HandleOldJobMessage(CallbackQuery callbackQuery, ImagineCallbackData callbackData)
    {
        if (callbackQuery.Message is Message message)
        {
            await client.EditMessageReplyMarkupAsync(message, GetOldJobMarkup(callbackData.JobId, message.ReplyToMessage?.Text));
        }
    }
    private async Task HandleDeletedMessage(CallbackQuery callbackQuery, ImagineCallbackData callbackData)
    {
        if (callbackQuery.Message is Message message && message.ReplyToMessage == null)
        {
            await client.EditMessageReplyMarkupAsync(message, GetDeletedMarkup(callbackData.JobId));
        }
    }

    private async Task AddJobToTheQueue(Message message, IInputData data)
    {
        Message botMessage;
        try
        {
            botMessage = await client.SendMessageAsync(message, "Відправляю", replyToMessage: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AddJobToTheQueue: {error}", ex.Message);
            throw;
        }

        data.BotMessageId = botMessage.MessageId;

        try
        {
            imageGenearatorQueue.AddJob(data);
            await client.EditMessageTextAsync(botMessage, "Твій шедевр в черзі. Чекай");
        }
        catch (OldJobException ex)
        {
            await client.EditMessageTextAsync(botMessage, ex.Message);
        }
        catch (AlreadyRunningException ex)
        {
            await client.EditMessageTextAsync(botMessage, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AddJobToTheQueue: {error}", ex.Message);
            await client.EditMessageTextAsync(botMessage, "Не можу додати в чергу - в мене лапки :(");
        }
    }

    internal async Task JobFailed(JobInfo job, Exception exception)
    {
        switch (exception)
        {
            case SdNotAvailableException:
                {
                    using var stream = new MemoryStream(Properties.Resources.asleep);
                    var photo = InputFile.FromStream(stream, "beaver.png");
                    await client.SendPhotoAsync(job, photo, exception.Message);
                    //await client.EditMessageMediaAsync(job.ChatId, job.BotMessageId, photo, null);
                    break;
                }
            case InputException:
                await client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Помилка в запиті. " + exception.Message + ". Перевір свій запит та спробуй ще");
                break;

            case RenderFailedException:
                {
                    var keys = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Спробувати ще", new ImagineCallbackData(Command, ImagineCommands.Repeat, job.Id)));
                    await client.DeleteMessageAsync(job.ChatId, job.BotMessageId);
                    await client.SendMessageAsync(job.ChatId, "Рендер невдалий. " + job.TextStatus, keys, job.MessageThreadId, job.MessageId);
                    break;
                }
            case AlreadyRunningException:
                {
                    await client.EditMessageTextAsync(job.ChatId, job.BotMessageId, exception.Message);
                    break;
                }
            case WebSocketException:
                {
                    await client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Сервер розірвав з'єднання. Можливо він був виключений. Спробуй пізніше");
                    break;
                }
            default:
                await client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Невідома помилка");
                //Directory.Delete(obj.TmpDir, true);
                break;
        }
        logger.LogError(exception, "JobFailed Exception");
    }

    internal async Task JobInQueue(JobInfo job)
    {
        string clock = getClock(DateTime.Now.Second % 10);
        string text = $"{clock} {DateTime.Now.ToString("HH:mm:ss")} Твій шедевр в черзі.\nЙого буде виконано як тільки знайдеться вільний [{job.Diffusor ?? "бобер"}]";
        await client.EditMessageTextAsync(job.ChatId, job.BotMessageId, text);

    }

    private string getClock(int number)
    {
        if (number < 0 || number > 12)
            throw new ArgumentOutOfRangeException(nameof(number));
        return char.ConvertFromUtf32(0x1F550 + number);
    }

    internal async Task JobFinished(JobInfo job)
    {
        if (!await OriginalMessageExists(job.ChatId, job.MessageId))
        {
            await client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Запит було видалено");
            return;
        }
        foreach (var item in job.Results)
        {
            AddWatermark(item);
        }

        //foreach (var item in job.Results)
        //{
        //    using var stream = System.IO.File.OpenRead(item.FilePath);
        //    var media = InputFile.FromStream(stream, Path.GetFileName(stream.Name));

        //    string? info = null;
        //    if (job.PostInfo)
        //    {
        //        info = item.Info;
        //    }

        //    var keys = GetReplyMarkupForJob(job.Type, item.Id.ToString(), job.UpscaleModifyer, prompt: job.Text);
        //    switch (job.Type)
        //    {
        //        case JobType.Vingette:
        //        case JobType.Noise:
        //        case JobType.Upscale:
        //        case JobType.HiresFix:
        //            {
        //                await client.SendDocumentAsync(job, media, info, true, keys);
        //                break;
        //            }

        //        case JobType.Text2Image:
        //            {
        //                await client.SendPhotoAsync(job, media, info, true, keys);
        //                break;
        //            }
        //        default:
        //            break;
        //    }
        //}

        using (var streamList = new StreamList(job.Results.Select(x => new StreamResultInfo(System.IO.File.OpenRead(x.FilePath), x.Id))))
        {

            //TODO: return job.PostInfo
            switch (job.Type)
            {
                case JobType.Vingette:
                case JobType.Noise:
                case JobType.Upscale:
                case JobType.HiresFix:
                    {
                        var file = streamList.Single();
                        var keys = GetReplyMarkupForJob(job.Type, file.jobResultId, job.UpscaleModifyer, prompt: job.Text);
                        var media = InputFileStream.FromStream(file.Stream, Path.GetFileName(file.Stream.Name));
                        var result = await client.SendDocumentAsync(job, media, reply: true, markup: keys);
                        databaseService.AddFile(file.jobResultId, result.Document.FileId);

                        break;
                    }

                case JobType.Text2Image or JobType.Text2ImageFaceId:
                    {
                        var media = streamList.Select(x => new InputMediaPhoto() { Media = InputFile.FromStream(x.Stream, Path.GetFileName(x.Stream.Name)) });
                        var result = await client.SendMediaAlbumAsync(job, media);
                        databaseService.AddFiles(streamList.Select(x => x.jobResultId), result.Select(x => x.Photo.Last().FileId));
                        //await client.SendPhotoAsync(job, media, reply: true);
                        break;
                    }
                default:
                    break;
            }
        }

        foreach (var item in job.Results)
        {
            System.IO.File.Delete(item.FilePath);
        }

        await client.DeleteMessageAsync(job.ChatId, job.BotMessageId);

    }

    private void AddWatermark(JobResultInfoView item)
    {
        if (configuration.IGSettings.Watermark)
        {

            using var image = new MagickImage(item.FilePath);
            var watermark = new MagickImage(Properties.Resources.watermark);
            watermark.Evaluate(Channels.Alpha, EvaluateOperator.Divide, 4);

            image.Composite(watermark, Gravity.Southeast, CompositeOperator.Over);
            var fileInfo = new FileInfo(item.FilePath);
            var directory = fileInfo.Directory?.FullName ?? string.Empty;
            var filename = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(fileInfo.Name)}_w{fileInfo.Extension}");
            image.Write(filename);
            item.FilePath = filename;
        }
    }

    private async Task<bool> OriginalMessageExists(long chatId, int messageId)
    {
        var testChatId = appSettings.GetValue<long>("TEST_CHAT_ID");
        try
        {
            var copied = await client.CopyMessageAsync(testChatId, chatId, messageId);
            await client.DeleteMessageAsync(testChatId, copied.Id);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in OriginalMessageExists: {error}", ex.Message);
            return false;
        }
    }

    public async Task HandleInlineQuery(InlineQuery inlineQuery)
    {
        await client.AnswerInlineQueryAsync(inlineQuery.Id, [
            new InlineQueryResultArticle() {
                Id = "1",
                InputMessageContent = new InputTextMessageContent() {
                    MessageText = inlineQuery.Query },
                Title = inlineQuery.Query }]
        );
    }

    record StreamResultInfo(FileStream Stream, string jobResultId);
    private class StreamList(IEnumerable<StreamResultInfo> streams) : IDisposable, IEnumerable<StreamResultInfo>
    {
        private readonly List<StreamResultInfo> _streams = new(streams);

        public void Dispose()
        {
            foreach (var item in _streams)
            {
                item.Stream.Close();
                item.Stream.Dispose();
            }
        }

        public IEnumerator<StreamResultInfo> GetEnumerator()
        {
            return _streams.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _streams.GetEnumerator();
        }
    }
}