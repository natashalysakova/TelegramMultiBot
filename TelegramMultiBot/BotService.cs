// See https://aka.ms/new-console-template for more information
using Bober.Library.Contract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot;
using TelegramMultiBot.Commands;

using TelegramMultiBot.ImageGenerators.Automatic1111;

public class BotService : BackgroundService
{
    private readonly TelegramBotClient _client;
    private readonly ILogger _logger;
    JobManager _jobManager;
    private readonly DialogManager _dialogManager;
    private readonly IServiceProvider _serviceProvider;
    //private readonly ImageGenearatorQueue _imageGenearatorQueue;
    //private readonly IConfiguration _configuration;
    public static string BotName;

    //System.Timers.Timer _timer;


    public BotService(TelegramBotClient client, ILogger<BotService> logger, JobManager jobManager, DialogManager dialogManager, IServiceProvider serviceProvider,  IConfiguration configuration)
    {
        _client = client;
        _logger = logger;
        _jobManager = jobManager;
        _dialogManager = dialogManager;
        _serviceProvider = serviceProvider;
    }

    public void JobFailed(JobInfo obj, Exception exception)
    {
        try
        {
            var command = (ImagineCommand)_serviceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
            command.JobFailed(obj, exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }

    }

    public void JobFinished(JobInfo obj)
    {
        try
        {
            var command = (ImagineCommand)_serviceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(ImagineCommand));
            command.JobFinished(obj);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
    }

    private async void JobManager_ReadyToSend(long chatId, string message)
    {
        try
        {
            _logger.LogDebug($"sending by schedule: {message}");
            await _client.SendTextMessageAsync(new ChatId(chatId), message, disableWebPagePreview: true);
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

        using (var scope = _serviceProvider.CreateScope())
        {
            var activeDialog = _dialogManager[message.Chat.Id];
            if (activeDialog != null)
            {
                var cancelCommand = scope.ServiceProvider.GetKeyedService<ICommand>("cancel");
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
                foreach (var item in scope.ServiceProvider.GetServices<ICommand>())
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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _jobManager.Run(stoppingToken);
        _jobManager.ReadyToSend += JobManager_ReadyToSend;

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.InlineQuery, UpdateType.ChosenInlineResult, UpdateType.CallbackQuery }
        };

        using (var scope = _serviceProvider.CreateScope())
        {
            var apiClient = scope.ServiceProvider.GetService<BoberApiClient>();
            apiClient.Subscribe();
        }


        _client.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
        receiverOptions,
                stoppingToken
            );

        BotName = _client.GetMeAsync().Result.Username;

        while (!stoppingToken.IsCancellationRequested)
        {
            Thread.Sleep(1000);
        }

        _jobManager.Dispose();

        return Task.CompletedTask;
    }

    internal void JobProgress(JobInfo jobInfo, Exception ex)
    {
        _client.EditMessageTextAsync(jobInfo.ChatId, jobInfo.BotMessageId, "Progress " + jobInfo.Progress);
    }
}
