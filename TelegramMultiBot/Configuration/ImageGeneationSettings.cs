namespace TelegramMultiBot.Configuration
{
    class ImageGeneationSettings
    {
        public const string Name = "ImageGeneation";

        public int DatabaseCleanupInterval { get; set; }
        public int JobAge { get; set; }
        public bool RemoveFiles { get; set; }
        public string BaseOutputDirectory { get; set; }
        public int JobLimitPerUser { get; set; }
        public int ActiveJobs { get; set; }

    }

    class Automatic1111Settings
    {
        public const string Name = "Automatic1111";
        public string PayloadPath { get; set; }
        public int HiResBatchCount { get; set; }
        public int BatchCount { get; set; }
        public string DefaultModel { get; set; }
        public string SdDefaultModel { get; set; }

        public string UpscalePath { get; set; }
        public double UpscaleMultiplier { get; set; }
        public string OutputDirectory { get; set; }
    }
    class ComfyUISettings
    {
        public const string Name = "ComfyUI";

        public int HiResBatchCount { get; set; }
        public int BatchCount { get; set; }

    }
}
