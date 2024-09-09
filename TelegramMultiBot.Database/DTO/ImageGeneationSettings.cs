namespace TelegramMultiBot.Database.DTO
{
    public class ImageGenerationSettings
    {
        public const string Name = "ImageGeneration";

        public int DatabaseCleanupInterval { get; set; }
        public int JobAge { get; set; }
        public bool RemoveFiles { get; set; } = true;
        public string BaseImageDirectory { get; set; }
        public string DownloadDirectory { get; set; }
        public int JobLimitPerUser { get; set; }
        public int ActiveJobs { get; set; }
        public int BatchCount { get; set; } = 1;
        public string DefaultModel { get; set; }
        public double UpscaleMultiplier { get; set; }
        public string UpscaleModel { get; set; }
        public double HiresFixDenoise { get; set; }
        public bool Watermark { get; set; }
        public int MaxGpuUtil { get; set; }
        public ushort ReciverPort { get; set; }
    }

    public class Automatic1111Settings
    {
        public const string Name = "Automatic1111";
        public string PayloadPath { get; set; }
        public string UpscalePath { get; set; }
        public string OutputDirectory { get; set; }
    }

    public  class ComfyUISettings
    {
        public const string Name = "ComfyUI";

        public int HiResBatchCount { get; set; } = 1;
        public int BatchCount { get; set; } = 1;
        public string OutputDirectory { get; set; }
        public string PayloadPath { get; set; }
        public string InputDirectory { get; set; }
        public double NoiseStrength { get; set; }
        public double VegnietteIntensity { get; set; }
    }
}