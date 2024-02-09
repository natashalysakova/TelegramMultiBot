using Telegram.Bot.Types;

namespace TelegramMultiBot.ImageGenerators
{
    public interface IJob
    {
        public IEnumerable<string>? Results { get; set; }

        long UserId { get;  }
        int OriginalMessageId { get;  }
        long OriginalChatId { get; }
        int? OriginalMessageThreadId { get;  }

         Message BotMessage { get; set; }
         TimeSpan Elapsed { get; set; }
         string TmpDirName { get;  }
         string TmpDir { get; set; }
        string Id { get; set; }
    }
}