namespace TelegramMultiBot.ImageGenerators.Automatic1111.Api
{
    public class SdResponse
    {
        public string[] images { get; set; }
        public Parameters parameters { get; set; }
        public string info { get; set; }

        public Detail[] detail { get; set; }
    }

}
