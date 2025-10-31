namespace TelegramMultiBot.ImageGeneration.Automatic1111.Api;

public class State
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE1006 // Naming Styles
    public bool skipped { get; set; }
    public bool interrupted { get; set; }
    public string job { get; set; }
    public int job_count { get; set; }
    public string job_timestamp { get; set; }
    public int job_no { get; set; }
    public int sampling_step { get; set; }
    public int sampling_steps { get; set; }
}