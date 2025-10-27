// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;
using TelegramMultiBot.ImageCompare;
using TelegramMultiBot.ImageGenerators;
using TelegramMultiBot.MessageCache;

internal class BotService(
    TelegramBotClient client,
    ILogger<BotService> logger,
    JobManager jobManager,
    DialogManager dialogManager,
    IServiceProvider serviceProvider,
    ImageGenearatorQueue imageGenearatorQueue,
    ISqlConfiguationService configuration,
    MonitorService monitorService,
    IAssistantDataService assistantDataService,
    IMessageCacheService messageCacheService)
{
    public static string? BotName;
    private System.Timers.Timer? _timer;
    CancellationTokenSource cancellationTokenSource;
    CancellationTokenSource managerCancellationTokenSource;

    public void Run()
    {
        managerCancellationTokenSource = new CancellationTokenSource();
        jobManager.Run(managerCancellationTokenSource.Token);
        jobManager.ReadyToSend += JobManager_ReadyToSend;

        monitorService.Run(managerCancellationTokenSource.Token);
        monitorService.ReadyToSend += MonitorService_ReadyToSend;

        imageGenearatorQueue.Run(managerCancellationTokenSource.Token);
        imageGenearatorQueue.JobFinished += ImageGenearatorQueue_JobFinished;
        imageGenearatorQueue.JobFailed += ImageGenearatorQueue_JobFailed;
        imageGenearatorQueue.JobInQueue += ImageGenearatorQueue_JobInQueue;

        var config = configuration.IGSettings;
        if (config is null)
            throw new NullReferenceException(nameof(config));

        var interval = config.DatabaseCleanupInterval * 1000;
        _timer = new System.Timers.Timer()
        {
            Interval = interval,
            AutoReset = true
        };

        _timer.Elapsed += RunCleanup;
        _timer.Start();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [
                UpdateType.Message,
                UpdateType.InlineQuery,
                UpdateType.ChosenInlineResult,
                UpdateType.ShippingQuery,
                UpdateType.CallbackQuery,
                UpdateType.MessageReaction,
                UpdateType.MessageReactionCount
            ],
            DropPendingUpdates = true,
        };

        client.OnUpdate += HandleUpdateAsync;
        client.OnError += HandleErrorAsync;

        do
        {
            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var response = client.GetMe(cancellationTokenSource.Token);
                BotName = response.Result.Username;

                logger.LogInformation("client connected");
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
            }
            finally
            {
                logger.LogWarning("error reaching telegram servers. Retry in 30 seconds");
                Thread.Sleep(30000);
            }

        } while (!managerCancellationTokenSource.IsCancellationRequested);

        //jobManager.Dispose();
        _timer.Stop();
        _timer.Elapsed -= RunCleanup;
        _timer.Dispose();

    }

    private async void MonitorService_ReadyToSend(long chatId, List<(string localFilePath, string caption)> infos)
    {
        var streams = new List<Stream>();
        try
        {
            List<IAlbumInputMedia> media = new List<IAlbumInputMedia>();
            foreach (var info in infos)
            {
                logger.LogDebug("sending new schedule: {chatId} {message}", chatId, info.localFilePath);

                var stream = System.IO.File.OpenRead(info.localFilePath);
                streams.Add(stream);
                var filename = Path.GetFileName(info.localFilePath);
                var photo = new InputMediaPhoto(stream);
                photo.Caption = info.caption;
                media.Add(photo);
            }

            await client.SendMediaGroup(chatId, media);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
            if (ex.Message.Contains("chat not found") || ex.Message.Contains("PEER_ID_INVALID") || ex.Message.Contains("bot was kicked from the group chat"))
            {
                monitorService.DisableJob(chatId, ex.Message);
                logger.LogWarning("Removing all jobs for {id}", chatId);
            }
        }
        finally
        {
            foreach (var stream in streams)
            {
                stream.Close();
                stream.Dispose();
            }
        }

    }



    private async void ImageGenearatorQueue_JobInQueue(JobInfo info)
    {
        using var scope = serviceProvider.CreateScope();
        try
        {
            var command = (ImagineCommand)scope.ServiceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
            await command.JobInQueue(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
        }
    }

    private async void RunCleanup(object? sender, ElapsedEventArgs e)
    {
        using var scope = serviceProvider.CreateScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<CleanupService>();
        await cleanupService.Run();
    }

    private async void ImageGenearatorQueue_JobFailed(JobInfo obj, Exception exception)
    {
        using var scope = serviceProvider.CreateScope();
        try
        {
            var command = (ImagineCommand)scope.ServiceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
            await command.JobFailed(obj, exception);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
        }
    }

    private async void ImageGenearatorQueue_JobFinished(JobInfo obj)
    {
        using var scope = serviceProvider.CreateScope();
        try
        {
            var command = (ImagineCommand)scope.ServiceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
            await command.JobFinished(obj);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
        }
    }

    private async void JobManager_ReadyToSend(long chatId, int? messageThreadId, string message, string photo)
    {
        try
        {
            logger.LogDebug("sending by schedule: {message}", message);

            if (string.IsNullOrEmpty(photo))
            {
                await client.SendMessage(chatId, message, messageThreadId: messageThreadId, linkPreviewOptions: new LinkPreviewOptions() { IsDisabled = true });
            }
            else
            {
                //var filePath = await client.GetFileAsync(new GetFileRequest(){ FileId = photo });
                //MemoryStream stream = new MemoryStream();
                //await client.DownloadFileAsync(filePath.FilePath, stream);
                //var request = new SendPhotoRequest() { ChatId = chatId, Caption = message, Photo = InputFile.FromFileId(photo) };
                await client.SendPhoto(chatId, InputFile.FromFileId(photo), messageThreadId: messageThreadId, caption: message);
            }

            //await _client.SendTextMessageAsync(chatId, message, disableWebPagePreview: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
            if (ex.Message.Contains("chat not found") || ex.Message.Contains("PEER_ID_INVALID") || ex.Message.Contains("bot was kicked from the group chat"))
            {
                jobManager.DeleteJobsForChat(chatId);
                logger.LogWarning("Removing all jobs for {id}", chatId);
            }
        }
    }

    private Task HandleErrorAsync(Exception exception, HandleErrorSource source)
    {
        logger.LogDebug("{trace}", exception.ToString());
        logger.LogError("{message}", exception.Message);

        if (exception.Message.Contains("Bad Gateway") || exception.Message.Contains("Exception during making request"))
        {
            cancellationTokenSource.Cancel();
        }

        return Task.CompletedTask;
    }

    private Task HandleUpdateAsync(Update update)
    {
        logger.LogTrace("{update}", update.Type);

        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    if (update.Message == null)
                        throw new NullReferenceException(nameof(update.Message));
                    return BotOnMessageRecived(update.Message);

                case UpdateType.CallbackQuery:
                    if (update.CallbackQuery == null)
                        throw new NullReferenceException(nameof(update.CallbackQuery));
                    return BotOnCallbackRecived(update.CallbackQuery);

                case UpdateType.InlineQuery:
                    if (update.InlineQuery == null)
                        throw new NullReferenceException(nameof(update.InlineQuery));
                    return BotOnInlineQueryRecived(update.InlineQuery);

                case UpdateType.MessageReaction:
                    ///TODO: add update
                    if (update.MessageReaction == null)
                        throw new NullReferenceException(nameof(update.InlineQuery));
                    return BotOnMessageReactionRecived(update.MessageReaction);
                    break;

                case UpdateType.MessageReactionCount:
                    ///TODO: add update
                    break;

                default:
                    return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
        }
        return Task.CompletedTask;
    }

    private async Task BotOnMessageReactionRecived(MessageReactionUpdated messageReaction)
    {
        try
        {
            var commands = serviceProvider.GetServices<ICommand>().Where(x => x.CanHandle(messageReaction) && x.CanHandleMessageReaction).Select(x => (IMessageReactionHandler)x).ToList();

            foreach (var item in commands)
            {
                await item.HandleMessageReactionUpdate(messageReaction);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
        }
    }

    private async Task BotOnInlineQueryRecived(InlineQuery inlineQuery)
    {
        try
        {
            logger.LogTrace("{query}", inlineQuery.Query);
            var commands = serviceProvider.GetServices<ICommand>().Where(x => x.CanHandle(inlineQuery) && x.CanHandleInlineQuery).Select(x => (IInlineQueryHandler)x).ToList();

            if (commands.Count == 0)
            {
                await client.AnswerInlineQuery(inlineQuery.Id, []);
                return;
            }

            foreach (var item in commands)
            {
                await item.HandleInlineQuery(inlineQuery);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
            await client.AnswerInlineQuery(inlineQuery.Id, []);
        }
    }

    private async Task BotOnCallbackRecived(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Data == null)
            throw new NullReferenceException(nameof(callbackQuery.Data));

        try
        {
            var commands = serviceProvider.GetServices<ICommand>().Where(x => x.CanHandleCallback && x.CanHandle(callbackQuery.Data)).Select(x => (ICallbackHandler)x);

            foreach (var command in commands)
            {
                logger.LogDebug("callback {command}", command);
                await command.HandleCallback(callbackQuery);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{message}", ex.Message);
            await client.AnswerCallbackQuery(callbackQuery.Id, "Error:" + ex.Message, true);
        }
    }

    private async Task BotOnMessageRecived(Message message)
    {
        HandleChatHistory(message);
        CacheMessageForChat(message);

        if (message.Type != MessageType.Text && message.Type != MessageType.Photo)
            return;
        ArgumentNullException.ThrowIfNull(message.From);

        logger.LogTrace("Input message: {username} in {chatType} {chatTitle}: {Text}", message.From.Username,
            message.Chat.Type,
            (message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup) ? message.Chat.Title : string.Empty,
            message.Text);

        var activeDialog = dialogManager[message.Chat.Id, message.From.Id];
        if (activeDialog != null)
        {
            var cancelCommand = serviceProvider.GetRequiredKeyedService<ICommand>("cancel");
            if (cancelCommand.CanHandle(message))
            {
                await cancelCommand.Handle(message);
                dialogManager.Remove(activeDialog);
            }
            else
            {
                await dialogManager.HandleActiveDialog(message, activeDialog);
            }
        }
        else
        {
            var applicableComands = new List<ICommand>();
            foreach (var item in serviceProvider.GetServices<ICommand>())
            {
                if (item.CanHandle(message))
                {
                    applicableComands.Add(item);
                }
            }

            if (applicableComands.Any() && message.ForwardOrigin != null)
            {
                return;
            }

            foreach (var command in applicableComands)
            {
                logger.LogDebug("{command}", command);
                try
                {
                    await command.Handle(message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{message}", ex.Message);
                }
            }
        }
    }

    private void CacheMessageForChat(Message message)
    {
        var chatId = message.Chat.Id;
        var threadId = message.MessageThreadId;
        var text = message.Text?.Replace("/gpt", string.Empty).Trim();

        if (string.IsNullOrEmpty(text)) return;

        var user = message.From == null ?
        "Unknown" :
        message.From.Username ?? message.From.FirstName;

        var chatMessage = new ChatMessage(text, user);
        messageCacheService.AddMessage(chatId, threadId, chatMessage);
    }

    private void HandleChatHistory(Message message)
    {
        var assistant = assistantDataService.Get(message.Chat.Id, message.MessageThreadId);

        if (assistant is null || !assistant.IsActive)
        {
            return;
        }

        if (message.Entities != null && message.Entities.Any(x => x.Type == MessageEntityType.BotCommand))
        {
            return;
        }

        string forwardedFrom = "";
        string? text = message.Caption ?? message.Text;
        if (message.ForwardOrigin != null)
        {
            switch (message.ForwardOrigin.Type)
            {
                case MessageOriginType.User:
                    forwardedFrom = $"{message.ForwardFrom.FirstName} {message.ForwardFrom.LastName ?? string.Empty}";
                    break;
                case MessageOriginType.Chat:
                    forwardedFrom = message.ForwardFromChat.Title;
                    break;
                case MessageOriginType.Channel:
                    forwardedFrom = message.ForwardFromChat.Title;
                    break;
                case MessageOriginType.HiddenUser:
                    forwardedFrom = message.ForwardSenderName;
                    break;
                default:
                    break;
            }
        }

        var hasLink = message.Entities != null && message.Entities.Any(x => x.Type == MessageEntityType.Url);
        var from = message.From.FirstName;
        if (message.From.LastName != null)
        {
            from += " " + message.From.LastName;
        }
        var record = new ChatHistory()
        {
            Assistant = assistant,
            Author = from,
            HasLink = hasLink,
            MessageId = message.MessageId,
            HasPhoto = message.Photo is not null,
            HasVideo = message.Video is not null,
            RepostedFrom = forwardedFrom,
            SendTime = message.Date.ToLocalTime(),
            Text = text
        };

        assistantDataService.SaveToHistory(record);
    }
}