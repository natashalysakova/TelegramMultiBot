using Microsoft.EntityFrameworkCore;

namespace TelegramMultiBot.Database.Models
{
    [PrimaryKey(nameof(SettingSection), nameof(SettingsKey))]
    public class Settings
    {
        public string SettingSection { get; set; }
        public string SettingsKey { get; set; }
        public string SettingsValue { get; set; }
    }
}
