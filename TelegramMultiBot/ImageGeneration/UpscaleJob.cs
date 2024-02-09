using Telegram.Bot.Types;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.ImageGeneration
{
    internal class UpscaleJob : IJob
    {
        public UpscaleJob(CallbackQuery callbackQuery)
        {
            Id = Guid.NewGuid().ToString();
        }

        public long UserId => throw new NotImplementedException();

        public int OriginalMessageId => throw new NotImplementedException();

        public long OriginalChatId => throw new NotImplementedException();

        public int? OriginalMessageThreadId => throw new NotImplementedException();

        public Message BotMessage { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan Elapsed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string TmpDirName => throw new NotImplementedException();

        public string TmpDir { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IEnumerable<string>? Results { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Id { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}