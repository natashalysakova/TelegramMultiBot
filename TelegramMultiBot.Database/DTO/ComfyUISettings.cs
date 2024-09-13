namespace TelegramMultiBot.Database.DTO
{
    public class ComfyUISettings : BaseSetting
    {
        public static string Name => "ComfyUI";

        public string OutputDirectory { get; set; } = "comfy";
        public string PayloadPath { get; set; } = "ImageGeneration/ComfyUI/Payload";
        public string InputDirectory { get; set; } = "/home/input";
        public double NoiseStrength { get; set; } = 0.3;
        public double VegnietteIntensity { get; set; } = 0.3;
    }
}