using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Services
{
    public class AssistantDataService(BoberDbContext context) : IAssistantDataService
    {
        public int Cleanup()
        {
            var toDelete = context.ChatHistory.Where(x => x.SendTime < DateTime.Now.AddDays(-2));
            context.RemoveRange(toDelete);
            context.SaveChanges();
            return toDelete.Count();
        }

        public AssistantSubscriber? Get(long id, int? messageThreadId)
        {
            return context.Assistants.SingleOrDefault(x => x.ChatId == id && x.MessageThreadId == messageThreadId);
        }

        public AssistantSubscriber HandleSubscriber(long chatId, int? messageThreadId)
        {
            var subscriber = context.Assistants.SingleOrDefault(x => x.ChatId == chatId && x.MessageThreadId == messageThreadId);
            if (subscriber == null)
            {
                subscriber = new AssistantSubscriber() { ChatId = chatId, MessageThreadId = messageThreadId };
                context.Assistants.Add(subscriber);
            }

            subscriber.IsActive = !subscriber.IsActive;
            context.SaveChanges();

            return subscriber;
        }


        public void SaveToHistory(ChatHistory chatHistory)
        {
            context.ChatHistory.Add(chatHistory);
            context.SaveChanges();
        }
    }
}
