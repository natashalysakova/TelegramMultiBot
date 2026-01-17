namespace TelegramMultiBot.Database.DTO;

public class SvitlobotSettings : BaseSetting
{
    public static string Name => "Svitlobot";

    public string? KremCookie { get; set; }
    public string? KemCookie { get; set; }

}