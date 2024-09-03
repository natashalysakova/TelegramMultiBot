using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Services
{
    public class BotMessageService(BoberDbContext context) : IBotMessageDatabaseService
    {
        public void AddMessage(BotMessageAddInfo info)
        {
            context.BotMessages.Add(new BotMessage
            {
                ChatId = info.chatId,
                MessageId = info.messageId,
                SendTime = info.time,
                IsPrivateChat = info.isPrivate,
                UserId = info.userId,
            });
            context.SaveChanges();
        }

        public void DeleteMessage(BotMessageInfo info)
        {
            var toDelete = context.BotMessages.Where(x => x.ChatId == info.chatId && x.MessageId == info.messageId);
            context.BotMessages.RemoveRange(toDelete);
            context.SaveChanges();
        }

        public long GetUserId(BotMessageInfo info)
        {
            var botMessage = context.BotMessages.SingleOrDefault(x => x.ChatId == info.chatId && x.MessageId == info.messageId && x.UserId != null);
            if (botMessage != default && botMessage.UserId != null)
            {
                return botMessage.UserId.Value;
            }
            else
            {
                return 0;
            }
        }

        public bool IsActiveJob(BotMessageInfo info)
        {
            return context.Jobs.Any(x => x.ChatId == info.chatId && x.BotMessageId == info.messageId && (x.Status == Enums.ImageJobStatus.Queued || x.Status == Enums.ImageJobStatus.Running));
        }

        public bool IsBotMessage(BotMessageInfo info)
        {
            return context.BotMessages.Any(x => x.ChatId == info.chatId && x.MessageId == info.messageId);
        }

        public int RunCleanup()
        {
            var h48 = DateTime.Now.AddHours(-48);
            var h24 = DateTime.Now.AddHours(-24);
            var toDelete = context.BotMessages
                .Where(x =>
                       (x.SendTime < h48 && !x.IsPrivateChat)
                    || (x.SendTime < h24 && x.IsPrivateChat));
            context.BotMessages.RemoveRange(toDelete);
            context.SaveChanges();
            return toDelete.Count();
        }
    }
}
