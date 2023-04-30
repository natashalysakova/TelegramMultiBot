// See https://aka.ms/new-console-template for more information
using AngleSharp.Html.Dom;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot;
using static System.Net.Mime.MediaTypeNames;

internal class Program
{
    private static JobManager jobManager;
    private static PingSubscribersManager subscribersManager;
    private static DialogManager dialogManager;

    private static PingManager ping;

    private static TelegramBotClient bot;

    static Program()
    {
        jobManager = new JobManager();
        subscribersManager= new PingSubscribersManager();
        dialogManager = new DialogManager();

#if DEBUG
        bot = new TelegramBotClient("5341260793:AAELGS7rXCtEv2TH6_BLTDty_dGDfQ1Luuc");
#else
        bot = new TelegramBotClient("5449772952:AAEJPlCKjeC39kDJk_s89ztqsgQaiLyO8OM");
#endif

        ping = new PingManager();
    }

    private async static void Ping_InternetStatusChanged(DateTime date, bool status)
    {
        var sunscribers = subscribersManager.GetSubscribers();
        foreach (var subscriber in sunscribers)
        {
            if (status)
            {
                await bot.SendTextMessageAsync(subscriber, "Інтернетохарчування із бак!");
            }
            else
            {
                await bot.SendTextMessageAsync(subscriber, "Інтернетохарчування із упало!");
            }
        }
    }

    public static void Main(string[] args)
    {
        var builder = new ConfigurationBuilder();
        builder.AddUserSecrets<Program>();
        var configurationRoot = builder.Build();

        Telegram.Bot.Polling.ReceiverOptions? receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.InlineQuery, UpdateType.ChosenInlineResult, UpdateType.CallbackQuery }
        };
        CancellationTokenSource cancellationToken = new CancellationTokenSource();
        jobManager.Run(cancellationToken.Token);
        jobManager.ReadyToSend += JobManager_ReadyToSend;
        bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken.Token
            );

        ping.InternetDown += Ping_InternetStatusChanged;
        ping.InternetUp += Ping_InternetStatusChanged;
        var pingTask = Task.Run(ping.Run);


        while (!cancellationToken.IsCancellationRequested)
        {
            Thread.Sleep(1000);
        }
        ping.InternetDown-= Ping_InternetStatusChanged;
        ping.InternetUp-= Ping_InternetStatusChanged;
        ping.Abort();
        jobManager.Dispose();
    }

    private async static void JobManager_ReadyToSend(long chatId, string message)
    {
        try
        {
            LogUtil.Log($"sending by schedule: {message}");
            await bot.SendTextMessageAsync(new ChatId(chatId), message, disableWebPagePreview: true);
        }
        catch (Exception ex)
        {
            LogUtil.Log(ex.Message);
            if (ex.Message.Contains("chat not found") || ex.Message.Contains("PEER_ID_INVALID") || ex.Message.Contains("bot was kicked from the group chat"))
            {
                jobManager.DeleteJobsForChat(chatId);
                LogUtil.Log("Removing all jobs for " + chatId);
            }
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient cleint, Exception e, CancellationToken token)
    {
        LogUtil.Log(e.ToString());
        return Task.CompletedTask;
    }

    private static Task HandleUpdateAsync(ITelegramBotClient cleint, Update update, CancellationToken token)
    {
#if DEBUG
        LogUtil.Log(JsonConvert.SerializeObject(update));
#endif
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    return BotOnMessageRecived(bot, update.Message);
                case UpdateType.CallbackQuery:
                    return BotOnCallbackRecived(bot, update.CallbackQuery);
                default:
                    return Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            LogUtil.Log(ex.Message);
        }
        return Task.CompletedTask;
    }

    private static Task BotOnCallbackRecived(TelegramBotClient bot, CallbackQuery? callbackQuery)
    {
        LogUtil.Log("Deleting job: " + callbackQuery.Data);
        jobManager.DeleteJob(long.Parse(callbackQuery.Data));
        bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Job deleted", disableNotification: true);
        return Task.CompletedTask;
    }

    private async static Task BotOnMessageRecived(TelegramBotClient client, Message message)
    {
        if (message == null) { return; }
        if (message.Text == null) { return; }

        LogUtil.Log($"Input message: {message.From.Username} in {message.Chat.Type}{" " + (message.Chat.Type == ChatType.Group ? message.Chat.Title : string.Empty)} : {message.Text}");

        var activeDialog = dialogManager[message.Chat.Id];
        if (activeDialog != null)
        {
            await HandleActiveDialog(client, message, activeDialog);
            return;
        }

        // /add & job name & cron & text
        if (message.Text.ToLower().StartsWith("/add"))
        {
            var dialog = new Dialog()
            {
                ChatId = message.Chat.Id,
                Type = DialogType.AddJob
            };

            dialogManager[message.Chat.Id] = dialog;
            await HandleActiveDialog(client, message, dialog);         
        }

        if (message.Text.ToLower().StartsWith("/list"))
        {
            var jobs = jobManager.GetJobsByChatId(message.Chat.Id);
            var response = string.Join('\n', jobs.Select(x => $"{x.Name} Next run: {x.NextExecution} Text: {x.Message}"));
            if (string.IsNullOrEmpty(response))
            {
                await client.SendTextMessageAsync(message.Chat, "No jobs found", disableNotification: true);
                return;
            }
            await client.SendTextMessageAsync(message.Chat, response, disableWebPagePreview: true, disableNotification: true);
        }

        if (message.Text.ToLower().StartsWith("/delete"))
        {
            var buttons = new List<InlineKeyboardButton[]>();
            var jobs = jobManager.GetJobsByChatId(message.Chat.Id);
            if (jobs.Any())
            {
                foreach (var job in jobs)
                {
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(job.Name, job.Id.ToString()) });
                }
                InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup(buttons);
                LogUtil.Log("Sending lis of available jobs");
                await client.SendTextMessageAsync(message.Chat, "Select job to delete", replyMarkup: inlineKeyboard, disableNotification: true);
            }
            else
            {
                LogUtil.Log("No jobs found");
                await client.SendTextMessageAsync(message.Chat, "No jobs found", disableNotification: true);
            }
        }

        if (message.Text.ToLower().Equals("/status")){
            var result = subscribersManager.UpdateSubscription(message.Chat.Id);
            if (result)
            {
                await client.SendTextMessageAsync(message.Chat.Id, "You've been subscrbed on Інтернетохарчування notification");
            }
            else
            {
                await client.SendTextMessageAsync(message.Chat, "You've been unsubscrbed from Інтернетохарчування notification");
            }
        }
    }

    private static async Task HandleActiveDialog(TelegramBotClient client, Message message, Dialog activeDialog)
    {
        if (message.Text == "cancel")
        {
            await client.SendTextMessageAsync(activeDialog.ChatId, "Job creation cancelled", disableNotification: true);
            dialogManager.Remove(activeDialog);
            return;
        }

        var handler = DialogHandlerFactory.CreateHandler(activeDialog.Type);
        handler.Handle(activeDialog, message, client);

        if (activeDialog.IsDone)
        {
            var nextexec = jobManager.AddJob(activeDialog.ChatId, activeDialog.Name, activeDialog.CRON, activeDialog.Text);
            await client.SendTextMessageAsync(activeDialog.ChatId, "Job added", disableNotification: true);
            dialogManager.Remove(activeDialog);
        }
    }
}