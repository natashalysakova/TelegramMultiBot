namespace TelegramMultiBot.Database.DTO
{
    public class Automatic1111Settings : BaseSetting
    {
        public static string Name => "Automatic1111";
        public string PayloadPath { get; set; } = "ImageGeneration/Automatic1111/Payload";
        public string UpscalePath { get; set; } = "ImageGeneration/Automatic1111/Upscales";
        public string OutputDirectory { get; set; } = "automatic";
    }
}