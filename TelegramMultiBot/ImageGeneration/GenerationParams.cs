using System.Text.Json;
using Telegram.Bot.Types;
using static System.Net.Mime.MediaTypeNames;

namespace TelegramMultiBot.ImageGenerators
{
    public class GenerationParams 
    {        
        public GenerationParams(ImageJob job)
        {
            var text = job.Text;

            if (text.Contains("#negative"))
            {
                var startOfNegative = text.IndexOf("#negative");
                var indexOfPrompt = text.IndexOf(' ');
                Prompt = text.Substring(indexOfPrompt, startOfNegative - indexOfPrompt).Trim();
                NegativePrompt = text.Substring(startOfNegative + 9).Trim();
            }
            else
            {
                Prompt = text.Substring(text.IndexOf(' '));
                NegativePrompt = string.Empty;
            }

            Prompt = ClearPrompt(RemoveHashtags(Prompt));
            NegativePrompt = ClearPrompt(RemoveHashtags(NegativePrompt));

            for (int i = 1; i <= 4; i++)
            {
                string hashtag = $"#{i}";
                if (text.Contains(hashtag))
                {
                    BatchCount = i;
                    break;
                }
            }

            if (text.Contains("#model"))
            {
                int indexOfModel = text.IndexOf("#model") + 6;
                int nextSpace = text.IndexOf(' ', indexOfModel);
                if(nextSpace != -1)
                {
                    Model = text.Substring(indexOfModel + 1, nextSpace - indexOfModel).Trim();
                }
                else
                {
                    Model = text.Substring(indexOfModel+1).Trim();
                }
            }

            Seed = -1;
            if (text.Contains("#seed:"))
            {
                int indexOfSeed = text.IndexOf("#seed:");
                int startOfSeed = text.IndexOf(":", indexOfSeed);
                int endOfSeed = text.IndexOf(" ", indexOfSeed);
                if(endOfSeed != -1)
                {
                    Seed = long.Parse(text.Substring(startOfSeed+1, endOfSeed - startOfSeed).Trim());
                }
                else
                {
                    Seed = long.Parse(text.Substring(startOfSeed+1).Trim());
                }
            }

            var resolution = supportedResolutions.Where(x => text.Contains(x.hashtag)).FirstOrDefault();
            if(resolution == default)
            {
                Width = defaultResolution.width; 
                Height = defaultResolution.height;
            }
            else
            {
                Width = resolution.width;
                Height = resolution.height;
            }

        }

        public int BatchCount { get; internal set; }
        public string Model { get; internal set; }
        public string Prompt { get; set; }
        public string NegativePrompt { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public long Seed { get; set; }

        protected string RemoveHashtags(string text)
        {
            var hashtags = text.Split(" ").Where(x => x.StartsWith("#"));
            foreach (var hasthag in hashtags)
            {
                text = text.Replace(hasthag, string.Empty);
            }
            return text;
        }

        private string ClearPrompt(string prompt)
        {
            return JsonEncodedText.Encode(prompt).Value;
        }

        (int width, int height) defaultResolution = (1024, 1024);

        (string hashtag, int width, int height)[] supportedResolutions = new[] 
        {
            ("#vertical", 768, 1344),
            ("#portrait", 915, 1144),
            ("#photo", 1182, 886),
            ("#landscape", 1254, 836),
            ("#widescreen", 1365, 768),
            ("#cinematic", 1564, 670),
        };

        /*
         768 x 1344: Vertical (9:16)
        915 x 1144: Portrait (4:5)
        1024 x 1024: square 1:1
        1182 x 886: Photo (4:3)
        1254 x 836: Landscape (3:2)
        1365 x 768: Widescreen (16:9)
        1564 x 670: Cinematic (21:9)
         */

    }
}
