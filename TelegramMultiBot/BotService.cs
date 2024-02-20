// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.ImageGenerators;
using TelegramMultiBot.ImageGenerators.Automatic1111;

class BotService
{
    private readonly TelegramBotClient _client;
    private readonly ILogger _logger;
    JobManager _jobManager;
    private readonly DialogManager _dialogManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ImageGenearatorQueue _imageGenearatorQueue;
    private readonly IConfiguration _configuration;
    public static string BotName;

    System.Timers.Timer _timer;
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
        _imageGenearatorQueue.JobFinished += _imageGenearatorQueue_JobFinished;
        _imageGenearatorQueue.JobFailed += _imageGenearatorQueue_JobFailed;

        var interval = _configuration.GetSection(ImageGeneationSettings.Name).Get<ImageGeneationSettings>().DatabaseCleanupInterval * 1000;

        _timer = new System.Timers.Timer(interval);
        _timer.AutoReset = true;

        _timer.Elapsed += RunCleanup;
        _timer.Start();


        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.InlineQuery, UpdateType.ChosenInlineResult, UpdateType.CallbackQuery  }
        };

        _client.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken.Token
            );

        BotName = _client.GetMeAsync().Result.Username;

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
        using (var scope = _serviceProvider.CreateScope())
        {
            var cleanupService = scope.ServiceProvider.GetService<CleanupService>();
            await cleanupService.Run();
        }
    }


    private async void _imageGenearatorQueue_JobFailed(JobInfo obj, Exception exception)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            try
            {
                var command = (ImagineCommand)scope.ServiceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
                await command.JobFailed(obj, exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }

    private async void _imageGenearatorQueue_JobFinished(JobInfo obj)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            try
            {
                var command = (ImagineCommand)scope.ServiceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
                await command.JobFinished(obj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
        }
    }

    private async void JobManager_ReadyToSend(long chatId, string message)
    {
        try
        {
            _logger.LogDebug($"sending by schedule: {message}");
            await _client.SendTextMessageAsync(chatId, message, disableWebPagePreview: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            if (ex.Message.Contains("chat not found") || ex.Message.Contains("PEER_ID_INVALID") || ex.Message.Contains("bot was kicked from the group chat"))
            {
                _jobManager.DeleteJobsForChat(chatId);
                _logger.LogWarning("Removing all jobs for " + chatId);
            }
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient cleint, Exception e, CancellationToken token)
    {
        _logger.LogDebug(e.ToString());
        _logger.LogError(e.Message);
        return Task.CompletedTask;
    }

    private Task HandleUpdateAsync(ITelegramBotClient cleint, Update update, CancellationToken token)
    {
        _logger.LogTrace(JsonConvert.SerializeObject(update));

        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    return BotOnMessageRecived(update.Message);
                case UpdateType.CallbackQuery:
                    return BotOnCallbackRecived(update.CallbackQuery);
                case UpdateType.InlineQuery:
                    return BotOnInlineQueryRecived(update.InlineQuery);
                default:
                    return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
        return Task.CompletedTask;
    }

    private Task BotOnInlineQueryRecived(InlineQuery? inlineQuery)
    {
        var commands = _serviceProvider.GetServices<ICommand>().Where(x => x.CanHandle(inlineQuery) && x.CanHandleInlineQuery).Select(x => (IInlineQueryHandler)x);

        foreach (var item in commands)
        {
            item.HandleInlineQuery(inlineQuery);
        }

        

        return Task.CompletedTask;
    }

    private async Task BotOnCallbackRecived(CallbackQuery? callbackQuery)
    {
        try
        {
            var commands = _serviceProvider.GetServices<ICommand>().Where(x => x.CanHandleCallback && x.CanHandle(callbackQuery.Data)).Select(x => (ICallbackHandler)x);

            foreach (var command in commands)
            {
                _logger.LogDebug($"callback {command}");
                await command.HandleCallback(callbackQuery);
            }
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException ex)
        {
            _logger.LogError(ex, ex.Message);
            _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Error:" + ex.Message, showAlert: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            _client.AnswerCallbackQueryAsync(callbackQuery.Id, "Error:" + ex.Message, showAlert: true);
        }
    }



    private async Task BotOnMessageRecived(Message message)
    {
        if (message == null) { return; }
        if (message.Text == null) { return; }

        _logger.LogTrace($"Input message: {message.From.Username} in {message.Chat.Type}{" " + (message.Chat.Type == ChatType.Group ? message.Chat.Title : string.Empty)} : {message.Text}");

        var activeDialog = _dialogManager[message.Chat.Id];
        if (activeDialog != null)
        {
            var cancelCommand = _serviceProvider.GetKeyedService<ICommand>("cancel");
            if (cancelCommand.CanHandle(message))
            {
                cancelCommand.Handle(message);
                _dialogManager.Remove(activeDialog);
            }
            else
            {
                await _dialogManager.HandleActiveDialog(message, activeDialog);
            }
        }
        else
        {

            // var applicableComands = _serviceProvider.GetServices<ICommand>().Where(x => x.CanHandle(message));
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
                _logger.LogDebug($"{command}");
                try
                {
                    await command.Handle(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }
            }
        }
    }
}
