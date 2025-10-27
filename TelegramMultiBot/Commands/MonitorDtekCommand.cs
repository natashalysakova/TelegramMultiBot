using Microsoft.Extensions.Logging;
using Telegram.Bot.Types;
using TelegramMultiBot.ImageCompare;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("monitor")]
    internal class MonitorDtekCommand(TelegramClientWrapper client, MonitorService monitorService, ILogger<MonitorDtekCommand> logger) : BaseCommand
    {
        private string supportedRegions = "регіони що підтримуються: krem - Київська область, kem - м. Київ";

        public async override Task Handle(Message message)
        {
            if (message.Text is null)
                return;

            var command = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (command.Length > 3 || command.Length < 1)
            {
                await client.SendMessageAsync(message.Chat, "Не поняв команду");
                return;
            }

            //if (command[1] == "id")
            //{
            //    var id = message.SenderChat is null ? message.Chat.Id : message.SenderChat.Id;
            //    await client.SendMessageAsync(message.Chat, "your id is " + id);

            //}

            if (command.Length == 1)
            {
                var activeJobs = monitorService.GetActiveJobs(message.Chat.Id);
                if (activeJobs.Count() == 0)
                {
                    await client.SendMessageAsync(message.Chat, "Нема активних завдань", messageThreadId: message.MessageThreadId);
                    return;
                }

                List<IAlbumInputMedia> media = new List<IAlbumInputMedia>();
                List<Stream> streams = new List<Stream>();
                foreach (var job in activeJobs)
                {

                    var info = monitorService.GetInfo(job.Id);
                    if (info == default)
                        continue;

                    logger.LogDebug("activeJob {id} - {file} - {caption}", job.Id, info.filename, info.caption);
                    var stream = System.IO.File.OpenRead(info.filename);
                    streams.Add(stream);
                    var filename = Path.GetFileName(info.filename);
                    var photo = new InputMediaPhoto(InputFile.FromStream(stream, filename));
                    photo.Caption = info.caption;
                    media.Add(photo);

                }

                if (media.Count() == 0)
                {
                    await client.SendMessageAsync(message.Chat, "Нема актуальних даних", messageThreadId: message.MessageThreadId);
                    CloseStreams(streams);
                    return;
                }

                try
                {
                    await client.SendMediaAlbumAsync(message.Chat.Id, media, messageThreadId: message.MessageThreadId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{message}", ex.Message);
                    if (ex.Message.Contains("chat not found") || ex.Message.Contains("PEER_ID_INVALID") || ex.Message.Contains("bot was kicked from the group chat"))
                    {
                        monitorService.DisableJob(message.Chat.Id, ex.Message);
                        logger.LogWarning("Removing all jobs for {id}", message.Chat.Id);
                    }
                }
                finally
                {
                    CloseStreams(streams);
                }

                return;
            }

            if (command[1].StartsWith("add-dtek-"))
            {
                if (command.Length < 2 || command.Length > 3)
                {
                    await client.SendMessageAsync(message.Chat, "Не валідна команда. Використовуй /monitor add-dtek-{region} {chatid}(optional)." + supportedRegions);
                    return;
                }

                long chatId;

                if (command.Length == 3)
                {
                    if (!long.TryParse(command[2], out chatId))
                    {

                        await client.SendMessageAsync(message.Chat, "Неправильний Id чату");
                        return;
                    }
                }
                else
                {
                    if (message.IsAutomaticForward && message.SenderChat != null)
                    {
                        chatId = message.SenderChat.Id;
                    }
                    else
                    {
                        chatId = message.Chat.Id;
                    }
                }



                var region = command[1].Split('-', StringSplitOptions.RemoveEmptyEntries).Last();

                var jobAdded = monitorService.AddDtekJob(chatId, region);

                if (jobAdded == -1)
                {
                    await client.SendMessageAsync(message.Chat, "Упс, Така задача вже є, або регіон не підтримується. " + supportedRegions);
                }
                else
                {
                    if (!monitorService.SendExisiting(jobAdded))
                    {
                        await client.SendMessageAsync(message.Chat, "Задача додана. Актуального графіку наразі нема.");
                    }
                }

                return;
            }

            if (command[1].StartsWith("del-dtek-"))
            {
                if (command.Length < 2 || command.Length > 3)
                {
                    await client.SendMessageAsync(message.Chat, "Не валідна команда. Використовуй /monitor add-dtek-{region} {chatid}(optional). " + supportedRegions);
                    return;
                }

                long chatId;

                if (command.Length == 3)
                {
                    if (!long.TryParse(command[2], out chatId))
                    {

                        await client.SendMessageAsync(message.Chat, "Неправильний Id чату");
                        return;
                    }
                }
                else
                {
                    if (message.IsAutomaticForward && message.SenderChat != null)
                    {
                        chatId = message.SenderChat.Id;
                    }
                    else
                    {
                        chatId = message.Chat.Id;
                    }
                }



                var region = command[1].Split('-', StringSplitOptions.RemoveEmptyEntries).Last();

                if (monitorService.DisableJob(chatId, region, "user request"))
                {
                    await client.SendMessageAsync(message.Chat, "Задача видалена");
                }
                else
                {
                    await client.SendMessageAsync(message.Chat, "Шось пішло не так");
                }

                return;
            }

            if (command[1] == "list")
            {
                if (command.Length < 2 || command.Length > 3)
                {
                    await client.SendMessageAsync(message.Chat, "Не валідна команда. Використовуй /monitor list {chatId}(optional)");
                }

                long chatId;
                if (command.Length == 3)
                {
                    if (!long.TryParse(command[2], out chatId))
                    {
                        await client.SendMessageAsync(message.Chat, "Неправильний Id чату");
                        return;
                    }
                }
                else
                {
                    if (message.IsAutomaticForward && message.SenderChat != null)
                    {
                        chatId = message.SenderChat.Id;
                    }
                    else
                    {
                        chatId = message.Chat.Id;
                    }
                }


                var jobs = monitorService.GetActiveJobs(chatId);

                if (!jobs.Any())
                {
                    await client.SendMessageAsync(message.Chat, "Задач не знайдено");
                    return;
                }

                var responce = string.Join(", ", jobs.Select(x => $"Перевіряємо {x.Url} Наступний запуск: {x.NextRun}"));
                await client.SendMessageAsync(message.Chat, responce);
            }
        }

        private static void CloseStreams(List<Stream> streams)
        {
            foreach (var stream in streams)
            {
                stream.Close();
                stream.Dispose();
            }
        }
    }
}
