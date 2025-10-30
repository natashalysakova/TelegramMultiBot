using System.Text.Json;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Enums;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.ImageGenerators
{
    public class GenerationParams
    {
        public GenerationParams(JobInfo job, ISqlConfiguationService configuration )
        {
            if (job.Text == null)
                throw new InvalidOperationException("Job has no text");

            var text = job.Text;

            if (text.Contains("#negative"))
            {
                var startOfNegative = text.IndexOf("#negative");
                var indexOfPrompt = text.IndexOf(' ');
                Prompt = text[indexOfPrompt..startOfNegative].Trim();
                NegativePrompt = text[(startOfNegative + 9)..].Trim();
            }
            else
            {
                Prompt = text[text.IndexOf(' ')..];
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
            string modelName;
            if (text.Contains("#model"))
            {
                int indexOfModel = text.IndexOf("#model") + 6;
                int nextSpace = text.IndexOf(' ', indexOfModel);
                if (nextSpace != -1)
                {
                    modelName = text.Substring(indexOfModel + 1, nextSpace - indexOfModel).Trim();
                }
                else
                {
                    modelName = text[(indexOfModel + 1)..].Trim();
                }
                Model = configuration.Models.Single(x => x.Name == modelName);
            }
            else
            {
                Model = configuration.DefaultModel;
            }

            Seed = -1;
            if (text.Contains("#seed:"))
            {
                int indexOfSeed = text.IndexOf("#seed:");
                int startOfSeed = text.IndexOf(':', indexOfSeed);
                int endOfSeed = text.IndexOf(' ', indexOfSeed);
                if (endOfSeed != -1)
                {
                    Seed = long.Parse(text.Substring(startOfSeed + 1, endOfSeed - startOfSeed).Trim());
                }
                else
                {
                    Seed = long.Parse(text[(startOfSeed + 1)..].Trim());
                }
            }


            var resolution = supportedResolutions[Model.Version].Where(x => text.Contains(x.Hashtag)).FirstOrDefault();
            if (resolution == default)
            {
                Width = defaultResolution[Model.Version].Width;
                Height = defaultResolution[Model.Version].Height;
            }
            else
            {
                Width = resolution.Width;
                Height = resolution.Height;
            }
        }

        public int BatchCount { get; internal set; }
        public ModelInfo Model { get; internal set; }
        public string Prompt { get; set; }
        public string NegativePrompt { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public long Seed { get; set; }

        protected static string RemoveHashtags(string text)
        {
            var hashtags = text.Split(' ').Where(x => x.StartsWith('#'));
            foreach (var hasthag in hashtags)
            {
                text = text.Replace(hasthag, string.Empty);
            }
            return text;
        }

        private static string ClearPrompt(string prompt)
        {
            return JsonEncodedText.Encode(prompt).Value.Trim();
        }

        public static readonly Dictionary<ModelVersion, Resolution> defaultResolution = new()
        {
            { ModelVersion.SDXL,  new("#square", 1024, 1024, "1\\:1")},
            { ModelVersion.OneFive,  new("#square", 512, 512, "1\\:1")}
        };

        public static readonly Dictionary<ModelVersion, Resolution[]> supportedResolutions = new()
        {
            { ModelVersion.SDXL, [
                new("#vertical", 768, 1344, "9\\:16"),
                new("#widescreen", 1365 , 768, "16\\:9"),
                new("#portrait", 915 , 1144, "4\\:5"),
                new("#photo", 1182 , 886, "4\\:3"),
                new("#landscape", 1254 , 836, "3\\:2"),
                new("#cinematic", 1564 , 670, "21\\:9"),
                new("#phone", 786, 1704, "19\\.5\\:9" ) ]
            },
            { ModelVersion.OneFive, [
                new("#vertical", 432, 768, "9\\:16"),
                new("#widescreen", 768 , 432, "16\\:9"),
                new("#portrait", 512 , 640, "4\\:5"),
                new("#photo", 576 , 432, "4\\:3"),
                new("#landscape", 768 , 512, "3\\:2"),
                new("#cinematic", 728 , 312, "21\\:9"),
                new("#phone", 393, 852, "19\\.5\\:9" )]
            }
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

    public record Resolution(string Hashtag, int Width, int Height, string Ar);
}