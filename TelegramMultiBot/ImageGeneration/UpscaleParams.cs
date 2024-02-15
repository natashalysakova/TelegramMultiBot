using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Telegram.Bot.Types;
using TelegramMultiBot.ImageGenerators;

namespace TelegramMultiBot.ImageGeneration
{
    public class UpscaleParams
    {
        public UpscaleParams(JobResult previousJob) 
        {         
            FilePath = previousJob.FilePath;
            ParseInfo(previousJob.Info);
        }
        public string Prompt { get; set; }
        public string NegativePrompt { get; set; }
        public long Seed { get; set; }
        public int Steps { get; set; }
        public string Sampler { get; set; }
        public double CFGScale { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Model { get; set; }
        public string ModelHash { get; set; }

        public double Denoising { get; set; }
        public string Version { get; set; }
        public double Score { get; set; }

        public string FilePath { get; internal set; }


        internal void ParseInfo(string info)
        {
            var split = info.Split(new[] { '\n', '\r' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2 || split.Length > 3)
            {
                throw new Exception("Failed to parse info from DB");
            }

            if (split.Length == 2)
            {
                Prompt = split[0];
                ParseParameters(split[1]);
            }
            else
            {
                Prompt = split[0];
                NegativePrompt = ParseParemeter(split[1]);
                ParseParameters(split[2]);
            }

        }

        private void ParseParameters(string parameters)
        {
            var split = parameters.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            Seed = long.Parse(ParseParemeter(split.Single(x => x.Contains("Seed:"))));
            Steps = int.Parse(ParseParemeter(split.Single(x => x.Contains("Steps:"))));

            Sampler = ParseParemeter(split.Single(x => x.Contains("Sampler:")));
            Model = ParseParemeter(split.Single(x => x.Contains("Model:")));
            ModelHash = ParseParemeter(split.Single(x => x.Contains("Model hash:")));

            Version = ParseParemeter(split.Single(x => x.Contains("Version:")));

            CFGScale = double.Parse(ParseParemeter(split.Single(x => x.Contains("CFG scale:"))), CultureInfo.InvariantCulture);

            //Score = double.Parse(split.Single(x => x.Contains("Score:")));

            if(split.Any(x => x.Contains("Denoising strength:")))
            {
                Denoising = double.Parse(ParseParemeter(split.Single(x => x.Contains("Denoising strength:"))), CultureInfo.InvariantCulture);
            }

            var size = ParseParemeter(split.Single(x => x.Contains("Size:")));
            var splitSize = size.Split('x', StringSplitOptions.RemoveEmptyEntries);
            Width = int.Parse(splitSize[0]);
            Height = int.Parse(splitSize[1]);
        }

        private string ParseParemeter(string paremeter)
        {
            return paremeter.Substring(paremeter.IndexOf(":") + 1).Trim();
        }
    }
}