namespace TelegramMultiBot.Configuration
{
    class ImageGeneationSettings
    {
        public BotSettings BotSettings { get; set; }
        public Automatic1111Settings Automatic1111 { get; set; }
        public ComfyUISettings ComfyUI { get; set; }
    }

    class BotSettings
    {
        public int JobLimitPerUser { get; set; }
        public int ActiveJobs { get; set; }
    }

    class Automatic1111Settings
    {
        public string PayloadPath { get; set; }
        public int HiResBatchCount { get; set; }
        public int BatchCount { get; set; }

    }
    class ComfyUISettings
    {
        public int HiResBatchCount { get; set; }
        public int BatchCount { get; set; }

    }
}
