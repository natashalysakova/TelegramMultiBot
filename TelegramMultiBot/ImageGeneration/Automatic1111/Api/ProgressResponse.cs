namespace TelegramMultiBot.ImageGenerators.Automatic1111.Api
{
    public class ProgressResponse
    {
        public float progress { get; set; }
        public float eta_relative { get; set; }
        public State state { get; set; }
        public object current_image { get; set; }
        public object textinfo { get; set; }
    }
}
