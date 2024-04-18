﻿namespace TelegramMultiBot.Configuration
{
    public class ImageGeneationSettings
    {
        public const string Name = "ImageGeneation";

        public int DatabaseCleanupInterval { get; set; }
        public int JobAge { get; set; }
        public bool RemoveFiles { get; set; } = true;
        public string BaseImageDirectory { get; set; }
        public string DownloadDirectory { get; set; }
        public int JobLimitPerUser { get; set; }
        public int ActiveJobs { get; set; }
        public ModelSettings[] Models { get; set; } = [];
        public int BatchCount { get; set; } = 1;
        public string DefaultModel { get; set; }
        public double UpscaleMultiplier { get; set; }
        public string UpscaleModel { get; set; }
        public double HiresFixDenoise { get; set; }
        public bool Watermark { get; set; }
        public int MaxGpuUtil { get; set; }
    }

    internal class Automatic1111Settings
    {
        public const string Name = "Automatic1111";
        public string PayloadPath { get; set; }
        public string UpscalePath { get; set; }
        public string OutputDirectory { get; set; }
    }

    internal class ComfyUISettings
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

    public class ModelSettings
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public float CGF { get; set; }
        public int Steps { get; set; }
        public string Sampler { get; set; }
        public string Scheduler { get; set; }
        public int CLIPskip { get; set; } = 1;
        public ModelVersion Version { get; set; }
    }

    public enum ModelVersion
    {
        Unknown = 0,
        OneFive = 1,
        SDXL = 2,
    }
}