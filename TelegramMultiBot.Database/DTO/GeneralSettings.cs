namespace TelegramMultiBot.Database.DTO;

public class GeneralSettings : BaseSetting
{
    public static string Name => "General";

    public string OllamaApiUrl { get; set; } = "http://localhost:3000/";
}