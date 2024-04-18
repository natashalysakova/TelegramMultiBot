namespace TelegramMultiBot.ImageGenerators.Automatic1111.Api
{
    public class SdResponse
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE1006 // Naming Styles
        public string[] images { get; set; }
        public Parameters parameters { get; set; }
        public string info { get; set; }
        public Detail[] detail { get; set; }
    }
}