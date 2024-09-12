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
}