using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database
{
    public static class ContextExtention
    {
        public static void AddSetting(this BoberDbContext context, string section, string key, string value)
        {
            context.Settings.Add(new Settings() { SettingSection = section, SettingsKey = key, SettingsValue = value });
        }
    }
}