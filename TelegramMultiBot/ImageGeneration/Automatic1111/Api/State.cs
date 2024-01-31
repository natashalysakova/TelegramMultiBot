namespace TelegramMultiBot.ImageGenerators.Automatic1111.Api
{
    public class State
    {
        public bool skipped { get; set; }
        public bool interrupted { get; set; }
        public string job { get; set; }
        public int job_count { get; set; }
        public string job_timestamp { get; set; }
        public int job_no { get; set; }
        public int sampling_step { get; set; }
        public int sampling_steps { get; set; }
    }
}
