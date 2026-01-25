namespace TelegramMultiBot.Database.DTO;

public class SvitlobotSettings : BaseSetting
{
    public static string Name => "Svitlobot";

    public string? KremCookie { get; set; }
    public string? KemCookie { get; set; }
    public string? OemCookie { get; set; }

    /// <summary>
    /// Set dtek parser delay in seconds
    /// </summary>
    public int DtekParserDelay { get; set; } = 600;

    /// <summary>
    /// Set monitor delay in seconds
    /// </summary>
    public int MonitorDelay { get; set; } = 30;
}