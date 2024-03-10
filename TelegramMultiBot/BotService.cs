// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.ImageGenerators;
using TelegramMultiBot.ImageGenerators.Automatic1111;
using static System.Runtime.InteropServices.JavaScript.JSType;

class BotService
{
    private readonly TelegramBotClient _client;
    private readonly ILogger _logger;
    private readonly JobManager _jobManager;
    private readonly DialogManager _dialogManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ImageGenearatorQueue _imageGenearatorQueue;
    private readonly IConfiguration _configuration;
    public static string? BotName;

    System.Timers.Timer? _timer;

    public BotService(TelegramBotClient client, ILogger<BotService> logger, JobManager jobManager, DialogManager dialogManager, IServiceProvider serviceProvider, ImageGenearatorQueue imageGenearatorQueue, IConfiguration configuration)
    {
        _client = client;
        _logger = logger;
        _jobManager = jobManager;
        _dialogManager = dialogManager;
        _serviceProvider = serviceProvider;
        _imageGenearatorQueue = imageGenearatorQueue;
        _configuration = configuration;
    }

    public void Run(CancellationTokenSource cancellationToken)
    {
        _jobManager.Run(cancellationToken.Token);
        _jobManager.ReadyToSend += JobManager_ReadyToSend;

        _imageGenearatorQueue.Run(cancellationToken.Token);
        _imageGenearatorQueue.JobFinished += ImageGenearatorQueue_JobFinished;
        _imageGenearatorQueue.JobFailed += ImageGenearatorQueue_JobFailed;

        var config = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>();
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
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.InlineQuery, UpdateType.ChosenInlineResult, UpdateType.CallbackQuery, UpdateType.MessageReaction, UpdateType.MessageReactionCount }
        };

        _client.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken.Token
            );

        var response = _client.GetMeAsync().Result;
        BotName = response.Username;



        while (!cancellationToken.IsCancellationRequested)
        {
            Thread.Sleep(1000);
        }

        _jobManager.Dispose();
        _timer.Stop();
        _timer.Elapsed -= RunCleanup;
        _timer.Dispose();

    }

    private async void RunCleanup(object? sender, ElapsedEventArgs e)
    {
        using var scope = _serviceProvider.CreateScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<CleanupService>();
        await cleanupService.Run();
    }


    private async void ImageGenearatorQueue_JobFailed(JobInfo obj, Exception exception)
    {
        using var scope = _serviceProvider.CreateScope();
        try
        {
            var command = (ImagineCommand)scope.ServiceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
            await command.JobFailed(obj, exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{message}", ex.Message);
        }
    }

    private async void ImageGenearatorQueue_JobFinished(JobInfo obj)
    {
        using var scope = _serviceProvider.CreateScope();
        try
        {
            var command = (ImagineCommand)scope.ServiceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
            await command.JobFinished(obj);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{message}", ex.Message);
        }
    }

    private async void JobManager_ReadyToSend(long chatId, string message)
    {
        try
        {
            _logger.LogDebug("sending by schedule: {message}", message);
            var request = new SendMessageRequest() { ChatId = chatId, Text = message, LinkPreviewOptions = new LinkPreviewOptions() { IsDisabled = true } };
            await _client.SendMessageAsync(request);
            //await _client.SendTextMessageAsync(chatId, message, disableWebPagePreview: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{message}", ex.Message);
            if (ex.Message.Contains("chat not found") || ex.Message.Contains("PEER_ID_INVALID") || ex.Message.Contains("bot was kicked from the group chat"))
            {
                _jobManager.DeleteJobsForChat(chatId);
                _logger.LogWarning("Removing all jobs for {id}", chatId);
            }
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient cleint, Exception e, CancellationToken token)
    {
        _logger.LogDebug("{trace}", e.ToString());
        _logger.LogError("{message}", e.Message);
        return Task.CompletedTask;
    }

    private Task HandleUpdateAsync(ITelegramBotClient cleint, Update update, CancellationToken token)
    {
        _logger.LogTrace("{update}", update.Type);

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
            _logger.LogError(ex, "{message}", ex.Message);
        }
        return Task.CompletedTask;
    }

    private async Task BotOnInlineQueryRecived(InlineQuery inlineQuery)
    {
        var commands = _serviceProvider.GetServices<ICommand>().Where(x => x.CanHandle(inlineQuery) && x.CanHandleInlineQuery).Select(x => (IInlineQueryHandler)x);

        if (!commands.Any())
        {
            var request = new AnswerInlineQueryRequest()
            {
                InlineQueryId = inlineQuery.Id,
                Results = new List<InlineQueryResult>()
            };
            await _client.AnswerInlineQueryAsync(request);
            return;
        }

        foreach (var item in commands)
        {
            await item.HandleInlineQuery(inlineQuery);
        }
    }

    private async Task BotOnCallbackRecived(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Data == null)
            throw new NullReferenceException(nameof(callbackQuery.Data));

        try
        {
            var commands = _serviceProvider.GetServices<ICommand>().Where(x => x.CanHandleCallback && x.CanHandle(callbackQuery.Data)).Select(x => (ICallbackHandler)x);

            foreach (var command in commands)
            {
                _logger.LogDebug("callback {command}", command);
                await command.HandleCallback(callbackQuery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{message}", ex.Message);
            var request = new AnswerCallbackQueryRequest()
            {
                CallbackQueryId = callbackQuery.Id,
                Text = "Error:" + ex.Message,
                ShowAlert = true
            };
            await _client.AnswerCallbackQueryAsync(request);
        }
    }



    private async Task BotOnMessageRecived(Message message)
    {
        if (message.Type != MessageType.Text)
            return;
        ArgumentNullException.ThrowIfNull(message.From);

        _logger.LogTrace("Input message: {username} in {chatType} {chatTitle}: {Text}", message.From.Username, 
            message.Chat.Type, 
            (message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup) ? message.Chat.Title : string.Empty, 
            message.IsTopicMessage.HasValue && message.IsTopicMessage.Value ? "Topic" : string.Empty,
            message.Text);

        var activeDialog = _dialogManager[message.Chat.Id];
        if (activeDialog != null)
        {
            var cancelCommand = _serviceProvider.GetRequiredKeyedService<ICommand>("cancel");
            if (cancelCommand.CanHandle(message))
            {
                await cancelCommand.Handle(message);
                _dialogManager.Remove(activeDialog);
            }
            else
            {
                await _dialogManager.HandleActiveDialog(message, activeDialog);
            }
        }
        else
        {
            var applicableComands = new List<ICommand>();
            foreach (var item in _serviceProvider.GetServices<ICommand>())
            {
                if (item.CanHandle(message))
                {
                    applicableComands.Add(item);
                }
            }

            foreach (var command in applicableComands)
            {
                _logger.LogDebug("{command}", command);
                try
                {
                    await command.Handle(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "{message}", ex.Message);
                }
            }
        }
    }
}
