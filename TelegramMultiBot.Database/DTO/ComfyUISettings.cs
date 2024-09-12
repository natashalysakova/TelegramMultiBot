namespace TelegramMultiBot.Database.DTO
{
    public class ComfyUISettings
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