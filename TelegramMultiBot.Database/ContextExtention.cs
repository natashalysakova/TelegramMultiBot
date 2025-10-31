using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database;

public static class ContextExtention
{
    public static void AddSetting(this BoberDbContext context, (string section, string key, string value) setting)
    {
        context.Settings.Add(new Settings() { SettingSection = setting.section, SettingsKey = setting.key, SettingsValue = setting.value });
    }
}