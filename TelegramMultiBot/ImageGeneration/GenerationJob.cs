using Telegram.Bot.Types;

namespace TelegramMultiBot.ImageGenerators
{
    public class GenerationJob
    {
        public IEnumerable<string>? Images { get; internal set; }

        // /imagine@bober blah blah positive #negative 
        
        public GenerationJob(Message message)
        {
            var text = message.Text;

            if (text.Contains("#negative"))
            {
                var startOfNegative = text.IndexOf("#negative");
                var indexOfPrompt = text.IndexOf(' ');
                Prompt = text.Substring(indexOfPrompt, startOfNegative-indexOfPrompt).Trim();
                NegativePrompt = text.Substring(startOfNegative+9).Trim();
            }
            else
            {
                Prompt = text.Substring(text.IndexOf(' '));
                NegativePrompt = string.Empty;
            }

            for (int i = 1; i <= 4; i++)
            {
                string hashtag = $"#{i}";
                if (text.Contains(hashtag))
                {
                    BatchCount = i;
                    break;
                }
            }

            UserId = message.From.Id;
            OriginalChatId = message.Chat.Id;
            OriginalMessageId = message.MessageId;
            OriginalMessageThreadId = message.MessageThreadId;
            TmpDirName = $"{OriginalChatId}_{OriginalMessageId}";

            AsFile = text.Contains("#file");
            AsSD15 = text.Contains("#sd");
            PostInfo = text.Contains("#info");

            Prompt = RemoveHashtags(Prompt);
            NegativePrompt = RemoveHashtags(NegativePrompt);

        }

        private static string RemoveHashtags(string text)
        {
            var hashtags = text.Split(" ").Where(x => x.StartsWith("#"));
            foreach (var hasthag in hashtags)
            {
                text = text.Replace(hasthag, string.Empty);
            }
            return text;
        }

        public long UserId { get; private set; }
        public int OriginalMessageId { get; private set; }
        public long OriginalChatId { get; private set; }
        public int? OriginalMessageThreadId { get; private set; }

        public Message BotMessage { get; internal set; }
        public string Prompt { get; private set; }
        public string NegativePrompt { get; private set; }

        public TimeSpan Elapsed { get; internal set; }
        public string TmpDirName { get; private set; }
        public string TmpDir { get; internal set; }
        public bool AsFile { get; set; }
        public bool AsSD15 { get; set; }
        public bool PostInfo { get; set; }
        public int BatchCount { get; internal set; }
    }
}
