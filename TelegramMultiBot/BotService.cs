// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot;
using TelegramMultiBot.Commands;

class BotService
{
    private readonly TelegramBotClient _client;
    private readonly ILogger _logger;
    JobManager _jobManager;
    private readonly DialogManager _dialogManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly ImageGenearatorQueue _imageGenearatorQueue;
    public static string BotName;
    public BotService(TelegramBotClient client, ILogger<BotService> logger, JobManager jobManager, DialogManager dialogManager, IServiceProvider serviceProvider, ImageGenearatorQueue imageGenearatorQueue)
    {
        _client = client;
        _logger = logger;
        _jobManager = jobManager;
        _dialogManager = dialogManager;
        _serviceProvider = serviceProvider;
        _imageGenearatorQueue = imageGenearatorQueue;
    }

    public void Run(CancellationTokenSource cancellationToken)
    {
        _jobManager.Run(cancellationToken.Token);
        _jobManager.ReadyToSend += JobManager_ReadyToSend;

        _imageGenearatorQueue.Run(cancellationToken.Token);
        _imageGenearatorQueue.JobFinished += _imageGenearatorQueue_JobFinished;
        _imageGenearatorQueue.JobFailed += _imageGenearatorQueue_JobFailed;

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.InlineQuery, UpdateType.ChosenInlineResult, UpdateType.CallbackQuery }
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
    }

    private void _imageGenearatorQueue_JobFailed(GenerationJob obj, string error)
    {
        var command = (StableDiffusionCommand)_serviceProvider.GetServices<ICommand>().Single(x=>x.GetType() == typeof(StableDiffusionCommand));

        command.JobFailed(obj, error);
    }

    private void _imageGenearatorQueue_JobFinished(GenerationJob obj)
    {
        var command = (StableDiffusionCommand)_serviceProvider.GetServices<ICommand>().Single(x => x.GetType() == typeof(StableDiffusionCommand));

        command.JobFinished(obj);
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

    private Task BotOnCallbackRecived(CallbackQuery? callbackQuery)
    {
        try
        {
            var callbackData = CallbackData.FromData(callbackQuery.Data);
            var commands = _serviceProvider.GetServices<ICommand>().Where(x=>x.CanHandleCallback && x.CanHandle(callbackData)).Select(x=> (ICallbackHandler)x );

            foreach (var command in commands)
            {
                _logger.LogDebug($"callback {command}");
                command.HandleCallback(callbackQuery);

            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }

        return Task.CompletedTask;
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
            var applicableComands = _serviceProvider.GetServices<ICommand>().Where(x => x.CanHandle(message));

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
