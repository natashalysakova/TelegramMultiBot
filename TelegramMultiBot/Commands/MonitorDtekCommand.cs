using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("monitor")]
    internal class MonitorDtekCommand(TelegramClientWrapper client, IMonitorDataService monitorDataService) : BaseCommand
    {
        public async override Task Handle(Message message)
        {
            if (message.Text is null)
                return;

            var command = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (command.Length > 3 || command.Length == 1)
            {
                await client.SendMessageAsync(message.Chat, "invalid command");
                return;
            }

            if (command[1] == "id")
            {
                var id = message.SenderChat is null ? message.Chat.Id : message.SenderChat.Id;
                await client.SendMessageAsync(message.Chat, "your id is " + id);

            }

            //if (command[1] == "add")
            //{
            //    if(command.Length!= 4)
            //    {
            //        await client.SendMessageAsync(message.Chat, "invalid add command. use /monitor add {chatId} {url}");
            //    }

            //    if (!long.TryParse(command[2], out var chatId))
            //    {
            //        await client.SendMessageAsync(message.Chat, "cannot parse chat Id");
            //    }

            //    monitorDataService.AddJob(chatId, command[3]);
            //    return;
            //}

            if (command[1].StartsWith("add-dtek-"))
            {
                if (command.Length < 2 || command.Length > 3)
                {
                    await client.SendMessageAsync(message.Chat, "invalid add command. use /monitor add-dtek-{region} {chatid}(optional). Supported regions: krem - Київська область");
                    return;
                }

                long chatId;

                if(command.Length == 3)
                {
                    if (!long.TryParse(command[2], out chatId))
                    {

                        await client.SendMessageAsync(message.Chat, "cannot parse chat Id");
                        return;
                    }
                }
                else
                {
                    if( message.IsAutomaticForward && message.SenderChat != null)
                    {
                        chatId = message.SenderChat.Id;
                    }
                    else
                    {
                        chatId = message.Chat.Id;
                    }
                }

                

                var region = command[1].Split('-', StringSplitOptions.RemoveEmptyEntries).Last();

                bool jobAdded = jobAdded = monitorDataService.AddDtekJob(chatId, region);

                if(!jobAdded)
                {
                    await client.SendMessageAsync(message.Chat, "Failed add job exist of unsupported region. Supported regions: krem - Київська область");
                }
                await client.SendMessageAsync(message.Chat, "Job added");

                return;
            }

            if(command[1] == "list")
            {
                if (command.Length != 3)
                {
                    await client.SendMessageAsync(message.Chat, "invalid list command. use /monitor list {chatId}");
                }

                if (long.TryParse(command[2], out var chatId))
                {
                    var jobs = monitorDataService.GetJobs(chatId);

                    if (!jobs.Any())
                    {
                        await client.SendMessageAsync(message.Chat, "No Jobs found");
                        return;
                    }

                    var responce = string.Join("\n", jobs.Select(x=> $"{x.ChatId} {x.Url} IsActive:{x.IsActive} {x.DeactivationReason}" ));
                    await client.SendMessageAsync(message.Chat, responce);
                }
                else
                {
                    await client.SendMessageAsync(message.Chat, "cannot parse chat Id");

                }
            }
        }
    }
}
