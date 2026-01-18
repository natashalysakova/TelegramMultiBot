using DtekParsers;
using TelegramMultiBot.Database.DTO;

namespace BotTests;

public static class SvitlobotSettingsExtensions
{
    public static void SetCookie(this SvitlobotSettings settings, string url, string cookie)
    {
        var region = LocationNameUtility.GetRegionByUrl(url);
        switch (region)
        {
            case "krem":
                settings.KremCookie = cookie;
                break;
            case "kem":
                settings.KemCookie = cookie;
                break;
            default:
                throw new ArgumentException($"Unknown region '{region}' for URL '{url}'.", nameof(url));
        }
    }
}