namespace TelegramMultiBot.ImageGenerators.Automatic1111.Api
{
    public class ProgressResponse
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE1006 // Naming Styles

        public float progress { get; set; }
        public float eta_relative { get; set; }
        public State state { get; set; }
        public object current_image { get; set; }
        public object textinfo { get; set; }
    }
}