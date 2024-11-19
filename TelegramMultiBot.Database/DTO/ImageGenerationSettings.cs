using System.Collections;
using System.Reflection;

namespace TelegramMultiBot.Database.DTO
{
    public class ImageGenerationSettings : BaseSetting
    {
        public static string Name => "ImageGeneration";

        public int DatabaseCleanupInterval { get; set; } = 3600;
        public int JobAge { get; set; } = 172800;
        public bool RemoveFiles { get; set; } = true;
        public string BaseImageDirectory { get; set; } = "images";
        public string DownloadDirectory { get; set; } = "download";
        public int JobLimitPerUser { get; set; } = 3;
        public int ActiveJobs { get; set; } = 1;
        public int BatchCount { get; set; } = 1;
        public string DefaultModel { get; set; } = "dreamshaper";
        public double UpscaleMultiplier { get; set; } = 4;
        public string UpscaleModel { get; set; } = "4x-UltraSharp.pth";
        public double HiresFixDenoise { get; set; } = 0.35;
        public bool Watermark { get; set; } = true;
        public int MaxGpuUtil { get; set; } = 20;
        public ushort ReciverPort { get; set; } = 5267;

        public string StandartNegative { get; set; } = "deformed iris, deformed pupils, bad eyes, bad anatomy, bad hands, long neck, long body, extra, fewer, missing, depth of field, blurry, cropped, jpeg artifacts, greyscale, monochrome, motion blur, emphasis lines, title, trademark, watermark, signature, username, artist name, lowres, bad anatomy, bad hands, error, missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, normal quality, jpeg artifacts, signature, watermark, username, blurry";
    }

    public abstract class BaseSetting : IEnumerable<(string section, string key, string value)>
    {
        public IEnumerator<(string section, string key, string value)> GetEnumerator()
        {
            return FillList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return FillList().GetEnumerator();
        }

        List<(string section, string key, string value)> FillList()
        {
            var list = new List<(string section, string key, string value)>();
            Type T = this.GetType();
            var nameprop = T.GetProperty("Name");
            var name = nameprop.GetValue(this).ToString();
            foreach (var prop in T.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var value = prop.GetValue(this);
                var item = (name, prop.Name, value is null ? string.Empty : value.ToString());
                list.Add(item);
            }

            return list;
        }
    }
}