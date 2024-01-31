using Telegram.Bot.Types;

namespace TelegramMultiBot.ImageGenerators
{
    class GenerationJob
    {
        public IEnumerable<string>? Images { get; internal set; }

        public GenerationJob(Message message)
        {
            Prompt = message.Text.Substring(message.Text.IndexOf(' '));
            UserId = message.From.Id;
            OriginalChatId = message.Chat.Id;
            OriginalMessageId = message.MessageId;
            OriginalMessageThreadId = message.MessageThreadId;
            TmpDirName = $"{OriginalChatId}_{OriginalMessageId}";

            AsFile = Prompt.Contains("#file");
            AsSDXL = Prompt.Contains("#xl");

            var hashtags = Prompt.Split(" ").Where(x => x.StartsWith("#"));
            foreach (var hasthag in hashtags)
            {
                Prompt = Prompt.Replace(hasthag, string.Empty);
            }
        }

        public long UserId { get; private set; }
        public int OriginalMessageId { get; private set; }
        public long OriginalChatId { get; private set; }
        public int? OriginalMessageThreadId { get; private set; }

        public Message BotMessage { get; internal set; }
        public string Prompt { get; private set; }
        public TimeSpan Elapsed { get; internal set; }
        public string TmpDirName { get; private set; }
        public string TmpDir { get; internal set; }
        public bool AsFile { get; set; }
        public bool AsSDXL { get; set; }
    }
}
