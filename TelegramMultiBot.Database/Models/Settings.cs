using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
