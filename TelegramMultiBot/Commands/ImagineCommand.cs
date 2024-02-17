
using Bober.Library.Contract;
using Bober.Library.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Security.Cryptography;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Commands.CallbackDataTypes;
using ServiceKeyAttribute = TelegramMultiBot.Commands.ServiceKeyAttribute;

namespace TelegramMultiBot.ImageGenerators.Automatic1111
{
    [ServiceKey("imagine")]

    internal class ImagineCommand : BaseCommand, ICallbackHandler
    {
        private readonly TelegramBotClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImagineCommand> _logger;
        private readonly BoberApiClient _apiClient;

        public ImagineCommand(TelegramBotClient client, IConfiguration configuration, ILogger<ImagineCommand> logger, BoberApiClient apiClient)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
            _apiClient = apiClient;
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
                var botMessage = await _client.SendTextMessageAsync(message.Chat.Id, $"Відправляю запит", messageThreadId: message.MessageThreadId, replyToMessageId: message.MessageId);

                try
                {
                    var jobId = await _apiClient.SendToQueue(CreateMessageData(message, botMessage));
                    await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, $"Твій шедевр в черзі. Чекай" + jobId);

                }
                catch (InvalidOperationException ex)
                {
                    await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                    await _client.EditMessageTextAsync(botMessage.Chat.Id, botMessage.MessageId, "Йойкі, шось пішло не так");
                }
            }
        }

        private MessageData CreateMessageData(Message message, Message botMessage)
        {
            return new MessageData()
            {
                BotMessageId = botMessage.MessageId,
                ChatId = message.Chat.Id,
                JobType = ImagineCommands.Text2Image,
                MessageId = message.MessageId,
                MessageThreadId = message.MessageId,
                Text = message.Text,
                UserId = message.From.Id
            };
        }

        private CallbackData CreateCallbackData(CallbackQuery query, ImagineCallbackData data)
        {
            return new CallbackData()
            {
                ChatId = query.Message.Chat.Id,
                JobType = data.JobType,
                MessageId = query.Message.MessageId,
                MessageThreadId = query.Message.MessageId,
                UserId = query.From.Id,
                PreviousJobResultId = Guid.Parse(data.Id),
                Upscale = data.Upscale,
            };
        }

        private InlineKeyboardMarkup? GetReplyMarkupForJob(ImagineCallbackData callbackData)
        {
            return GetReplyMarkupForJob(callbackData.JobType, callbackData.Id, callbackData.Upscale);
        }

        private InlineKeyboardMarkup? GetReplyMarkupForJob(ImagineCommands type, string id, double? upscale)
        {
            InlineKeyboardButton repeat = InlineKeyboardButton.WithCallbackData("Повторити", new ImagineCallbackData(Command, ImagineCommands.Repeat));
            InlineKeyboardButton original = InlineKeyboardButton.WithCallbackData("Оригінал", new ImagineCallbackData(Command, ImagineCommands.Original, id));
            InlineKeyboardButton hiresFix = InlineKeyboardButton.WithCallbackData($"Hires Fix", new ImagineCallbackData(Command, ImagineCommands.HiresFix, id, 0));
            InlineKeyboardButton upscale2 = InlineKeyboardButton.WithCallbackData("Upscale x2", new ImagineCallbackData(Command, ImagineCommands.Upscale, id, 2));
            InlineKeyboardButton upscale4 = InlineKeyboardButton.WithCallbackData("Upscale x4", new ImagineCallbackData(Command, ImagineCommands.Upscale, id, 4));
            InlineKeyboardButton info = InlineKeyboardButton.WithCallbackData("Інфо", new ImagineCallbackData(Command, ImagineCommands.Info, id, upscale));
            InlineKeyboardButton actions = InlineKeyboardButton.WithCallbackData("Кнопоцькі тиць", new ImagineCallbackData(Command, ImagineCommands.Actions, id));

            switch (type)
            {
                case ImagineCommands.Text2Image:
                    return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
                    {
                        actions,
                    });
                case ImagineCommands.HiresFix:
                    return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
                    {
                        info,
                        upscale2,
                    });
                case ImagineCommands.Upscale:
                    {
                        return new InlineKeyboardMarkup(new List<InlineKeyboardButton>
                        {
                            info
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
                                    repeat,
                                    original
                                },
                                    new List<InlineKeyboardButton>()
                                {
                                    hiresFix,
                                    upscale2,
                                    upscale4
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
                             repeat
                        },
                        new List<InlineKeyboardButton>
                        {
                            hiresFix,
                            upscale2,
                            upscale4
                        }
                    });
                default:
                    return default;
            }

        }

        public async Task HandleCallback(CallbackQuery callbackQuery)
        {
            var callbackData = ImagineCallbackData.FromString(callbackQuery.Data);

            switch (callbackData.JobType)
            {
                case ImagineCommands.Info:
                    {
                        var result = _apiClient.GetJobResultInfo(callbackData.Id);

                        if (result == null)
                        {
                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Це дуже стара картинка. Інформаця про неї загубилась", showAlert: true);
                            return;
                        }
                        else
                        {


                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Інформацію знайдено");
                            var keys = GetReplyMarkupForJob(callbackData);

                            InputMedia media = default;
                            if (callbackQuery.Message.Type == MessageType.Photo)
                            {
                                media = new InputMediaPhoto(InputFile.FromFileId(callbackQuery.Message.Photo.Last().FileId));
                                media.Caption = $"#seed:{result.Seed}\nRender time: {result.RenderTime}ms\n{result.Info}";
                            }
                            if (callbackQuery.Message.Type == MessageType.Document)
                            {
                                media = new InputMediaDocument(InputFile.FromFileId(callbackQuery.Message.Document.FileId));
                                media.Caption = $"Render time: {result.RenderTime}ms\n{result.Info}";
                            }


                            if (media.Caption.Length > 1024)
                            {
                                await _client.EditMessageReplyMarkupAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, keys);
                                await _client.SendTextMessageAsync(callbackQuery.Message.Chat.Id, media.Caption, replyMarkup: keys, replyToMessageId: callbackQuery.Message.MessageId);
                            }
                            else
                            {
                                await _client.EditMessageMediaAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, media, keys);
                            }




                        }
                        return;
                    }
                case ImagineCommands.Original:
                    {
                        if (callbackData.Id is null)
                            throw new ArgumentException("id");


                        var result = _apiClient.GetJobResultInfo(callbackData.Id);

                        var message = callbackQuery.Message;

                        if (result == null)
                        {
                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Це дуже стара каринка, бобер загубив оригінал", showAlert: true);
                            return;
                        }

                        await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Завантажую оригінал");

                        using (var stream = System.IO.File.OpenRead(result.FilePath))
                        {
                            var media = InputFile.FromStream(stream, Path.GetFileName(result.FilePath));
                            await _client.SendDocumentAsync(message.Chat.Id, media, messageThreadId: message.MessageThreadId, replyToMessageId: message.MessageId);
                        }

                        return;
                    }
                case ImagineCommands.Actions:
                    {

                        if (callbackData.Id is null)
                            throw new ArgumentException("id");

                        await _client.AnswerCallbackQueryAsync(callbackQuery.Id);

                        var keys = GetReplyMarkupForJob(callbackData);
                        await _client.EditMessageReplyMarkupAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, keys);

                        return;
                    }
                case ImagineCommands.HiresFix:
                case ImagineCommands.Upscale:
                    {
                        try
                        {
                            _apiClient.SendToQueue(CreateCallbackData(callbackQuery, callbackData));
                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Картинка в черзі. Чекай", showAlert: true);

                        }
                        catch (AlreadyRunningException ex)
                        {
                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, ex.Message, showAlert: true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Adding in the queue:" + ex.Message);
                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Не можу додати в чергу - в мене лапки :(", showAlert: true);
                        }
                        break;
                    }
                case ImagineCommands.Repeat:
                    {
                        var message = callbackQuery.Message.ReplyToMessage;

                        try
                        {
                            var botMessage = await _client.SendTextMessageAsync(message.Chat.Id, $"Твій шедевр в черзі. Чекай", messageThreadId: message.MessageThreadId, replyToMessageId: message.MessageId);
                            _apiClient.SendToQueue(CreateMessageData(message, botMessage));

                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id);

                        }
                        catch (AlreadyRunningException ex)
                        {
                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, ex.Message, showAlert: true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Adding in the queue:" + ex.Message);
                            await _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Не можу додати в чергу - в мене лапки :(", showAlert: true);
                        }
                        break;
                    }
                default:
                    break;
            }

        }


        internal async void JobFailed(JobInfo job, Exception exception)
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
                        var keys = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("Спробувати ще", new ImagineCallbackData(Command, ImagineCommands.Repeat)));

                        await _client.EditMessageTextAsync(job.ChatId, job.BotMessageId, "Рендер невдалий. Спробуйте ще", replyMarkup: keys);
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

        internal async void JobFinished(JobInfo inputJob)
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

                    var keys = GetReplyMarkupForJob(job.Type, item.Id, job.UpscaleModifyer);

                    switch (job.Type)
                    {
                        case ImagineCommands.Upscale:
                        case ImagineCommands.HiresFix:
                            {
                                await _client.SendDocumentAsync(job.ChatId, media, messageThreadId: job.MessageThreadId, caption: info, replyToMessageId: job.MessageId, replyMarkup: keys);
                                break;
                            }

                        case ImagineCommands.Text2Image:
                            {
                                await _client.SendPhotoAsync(job.ChatId, media, messageThreadId: job.MessageThreadId, caption: info, replyToMessageId: job.MessageId, replyMarkup: keys);
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
