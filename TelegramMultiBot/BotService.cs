﻿// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Commands;
using TelegramMultiBot.Commands.Interfaces;
using TelegramMultiBot.Configuration;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.ImageGenerators;

internal class BotService(TelegramBotClient client, ILogger<BotService> logger, JobManager jobManager, DialogManager dialogManager, IServiceProvider serviceProvider, ImageGenearatorQueue imageGenearatorQueue, ISqlConfiguationService configuration)
{
    public static string? BotName;
    private System.Timers.Timer? _timer;
    CancellationTokenSource cancellationTokenSource;
    CancellationTokenSource managerCancellationTokenSource;

    public async Task Run()
    {
        managerCancellationTokenSource = new CancellationTokenSource();
        jobManager.Run(managerCancellationTokenSource.Token);
        jobManager.ReadyToSend += JobManager_ReadyToSend;

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
            DropPendingUpdates = false
        };


        do
        {
            cancellationTokenSource = new CancellationTokenSource();

            try
            {
                client.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationTokenSource.Token);

                var response = client.GetMeAsync(new GetMeRequest()).Result;
                BotName = response.Username;

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

        jobManager.Dispose();
        _timer.Stop();
        _timer.Elapsed -= RunCleanup;
        _timer.Dispose();

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

    private async void JobManager_ReadyToSend(long chatId, string message)
    {
        try
        {
            logger.LogDebug("sending by schedule: {message}", message);
            var request = new SendMessageRequest() { ChatId = chatId, Text = message, LinkPreviewOptions = new LinkPreviewOptions() { IsDisabled = true } };
            await client.SendMessageAsync(request);
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

    private Task HandleErrorAsync(ITelegramBotClient cleint, Exception e, CancellationToken token)
    {
        logger.LogDebug("{trace}", e.ToString());
        logger.LogError("{message}", e.Message);

        if (e.Message.Contains("Bad Gateway") || e.Message.Contains("Exception during making request"))
        {
            cancellationTokenSource.Cancel();
        }

        return Task.CompletedTask;
    }

    private Task HandleUpdateAsync(ITelegramBotClient cleint, Update update, CancellationToken token)
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
                var request = new AnswerInlineQueryRequest()
                {
                    InlineQueryId = inlineQuery.Id,
                    Results = []
                };
                await client.AnswerInlineQueryAsync(request);
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
            var request = new AnswerInlineQueryRequest()
            {
                InlineQueryId = inlineQuery.Id,
                Results = []
            };
            await client.AnswerInlineQueryAsync(request);
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
            var request = new AnswerCallbackQueryRequest()
            {
                CallbackQueryId = callbackQuery.Id,
                Text = "Error:" + ex.Message,
                ShowAlert = true
            };
            await client.AnswerCallbackQueryAsync(request);
        }
    }

    private async Task BotOnMessageRecived(Message message)
    {
        if (message.Type != MessageType.Text)
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
}