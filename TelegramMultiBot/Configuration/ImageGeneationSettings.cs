namespace TelegramMultiBot.Configuration
{
    internal class ImageGeneationSettings
    {
        public const string Name = "ImageGeneation";

        public int DatabaseCleanupInterval { get; set; }
        public int JobAge { get; set; }
        public bool RemoveFiles { get; set; } = true;
        public required string BaseOutputDirectory { get; set; }
        public int JobLimitPerUser { get; set; }
        public int ActiveJobs { get; set; }
        public ModelSettings[] Models { get; set; } = [];
        public int BatchCount { get; set; } = 1;
        public required string DefaultModel { get; set; }
        public double UpscaleMultiplier { get; set; }
        public required string UpscaleModel { get; set; }
        public double HiresFixDenoise { get; set; }
    }

    internal class Automatic1111Settings
    {
        public const string Name = "Automatic1111";
        public required string PayloadPath { get; set; }
        public required string UpscalePath { get; set; }
        public required string OutputDirectory { get; set; }
    }

    internal class ComfyUISettings
    {
        public const string Name = "ComfyUI";

        public int HiResBatchCount { get; set; } = 1;
        public int BatchCount { get; set; } = 1;
        public required string OutputDirectory { get; set; }
        public required string PayloadPath { get; set; }
        public required string InputDirectory { get; set; }
        public double NoiseStrength { get; set; }
        public double VegnietteIntensity { get; set; }
    }

    public class ModelSettings
    {
        public required string Name { get; set; }
        public required string Path { get; set; }
        public required float CGF { get; set; }
        public required int Steps { get; set; }
        public required string Sampler { get; set; }
        public required string Scheduler { get; set; }
        public int CLIPskip { get; set; } = 1;
    }
}