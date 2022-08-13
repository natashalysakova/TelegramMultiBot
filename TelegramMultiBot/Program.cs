﻿// See https://aka.ms/new-console-template for more information
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    static JobManager jobManager;
    static TelegramBotClient bot;
    public static void Main(string[] args)
    {
        var builder = new ConfigurationBuilder();
        builder.AddUserSecrets<Program>();
        var configurationRoot = builder.Build();

        jobManager = new JobManager();
#if DEBUG
        bot = new TelegramBotClient("5341260793:AAELGS7rXCtEv2TH6_BLTDty_dGDfQ1Luuc");
#else
        bot = new TelegramBotClient("5449772952:AAEJPlCKjeC39kDJk_s89ztqsgQaiLyO8OM");
#endif

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

        while (!cancellationToken.IsCancellationRequested)
        {
            Thread.Sleep(1000);
        }

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
                //case UpdateType.InlineQuery:
                //    return BotOnInlineQueryReceived(bot, update.InlineQuery);
                //case UpdateType.ChosenInlineResult:
                //    return BotOnChosenInlineResultReceived(bot, update.ChosenInlineResult!);
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
        bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Job deleted");
        return Task.CompletedTask;
    }

    private async static Task BotOnMessageRecived(TelegramBotClient client, Message message)
    {
        if (message == null) { return; }
        if (message.Text == null) { return; }

        LogUtil.Log($"Input message: {message.From.Username} in {message.Chat.Type}{ " " + (message.Chat.Type == ChatType.Group ? message.Chat.Title : string.Empty)} : {message.Text}");
        // /add & job name & cron & text
        if (message.Text.ToLower().StartsWith("/add"))
        {
            var split = message.Text.Split('&', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 4)
            {
                var name = split[1].Trim();
                var cron = split[2].Trim();
                var text = split[3].Trim();

                var nextexec = jobManager.AddJob(message.Chat.Id, name, cron, text);
                await client.SendTextMessageAsync(message.Chat, "Job added");
            }
            else
            {
                LogUtil.Log("Invalid command. Correct format: /add & {job name} & {cron} & {text}");
                await client.SendTextMessageAsync(message.Chat, "Invalid command. Correct format: /add & {job name} & {cron} & {text}");
            }
            return;
        }

        if (message.Text.ToLower().StartsWith("/list"))
        {
            var jobs = jobManager.GetJobsByChatId(message.Chat.Id);
            var response = string.Join('\n', jobs.Select(x => $"{x.Name} Next run: {x.NextExecution} Text: {x.Message}"));
            if (string.IsNullOrEmpty(response))
            {
                await client.SendTextMessageAsync(message.Chat, "No jobs found");
                return;
            }

            await client.SendTextMessageAsync(message.Chat, response, disableWebPagePreview: true);
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
                await client.SendTextMessageAsync(message.Chat, "Select job to delete", replyMarkup: inlineKeyboard);
            }
            else
            {
                LogUtil.Log("No jobs found");
                await client.SendTextMessageAsync(message.Chat, "No jobs found");
            }
        }

        //if (message.Text.ToLower().StartsWith("https://www.tiktok.com/"))
        //{

        //    using (HttpClient tiktok_client = new HttpClient()) // WebClient class inherits IDisposable
        //    {
        //        // Or you can get the file content without saving it
        //        string htmlCode = await tiktok_client.GetStringAsync(message.Text);

        //        IHtmlDocument angle = new HtmlParser().ParseDocument(htmlCode);
        //        foreach (IElement element in angle.QuerySelectorAll("video"))
        //            Console.WriteLine(element.GetAttribute("src"));
        //    }
        //}
    }
}