namespace TelegramMultiBot.Database.DTO
{
    public class Automatic1111Settings
    {
        public const string Name = "Automatic1111";
        public string PayloadPath { get; set; }
        public string UpscalePath { get; set; }
        public string OutputDirectory { get; set; }
    }
}