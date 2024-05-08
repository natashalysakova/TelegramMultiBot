using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;
using TelegramMultiBot.Database.Models;
using static System.Collections.Specialized.BitVector32;

namespace TelegramMultiBot.Database.Services
{
    public class ConfigurationService : ISqlConfiguationService
    {
        private readonly BoberDbContext _context;
        private readonly ILogger<ConfigurationService> _logger;
        private readonly IMapper _mapper;
        private readonly IConfiguration _appSettings;

        public ConfigurationService(BoberDbContext context, ILogger<ConfigurationService> logger, IMapper mapper, IConfiguration appSettings)
        {
            _context = context;
            _logger = logger;
            _mapper = mapper;
            _appSettings = appSettings;

            //if (!context.Settings.Any())
            //{
            //    try
            //    {
            //        PopulateData();

            //    }
            //    catch (Exception ex)
            //    {
            //        _context.Models.RemoveRange(_context.Models);
            //        _context.Settings.RemoveRange(_context.Settings);
            //        _context.Hosts.RemoveRange(_context.Hosts);
            //        _context.SaveChanges();
            //        throw;
            //    }
            //}
        }

        private void PopulateData()
        {

            FillSection(_appSettings.GetSection(ImageGenerationSettings.Name));
            FillSection(_appSettings.GetSection(Automatic1111Settings.Name));
            FillSection(_appSettings.GetSection(ComfyUISettings.Name));

            FillModels(_appSettings.GetSection(ImageGenerationSettings.Name + ":Models"));
            FillHosts(_appSettings.GetSection("Hosts"));
        }

        private void FillHosts(IConfigurationSection configurationSection)
        {
            ConfigurationSection section = default;
            int i = 0;
            do
            {
                section = (ConfigurationSection)configurationSection.GetSection(i.ToString());
                if (!section.GetChildren().Any())
                    break;

                FillSection(section);
                var settings = _context.Settings.Where(x => x.SettingSection == "Hosts:" + i);
                var model = BindSettings<Host>(settings);
                _context.Settings.RemoveRange(settings);
                _context.Hosts.Add(model);
                _context.SaveChanges();

                i++;
            } while (true);
        }

        private void FillModels(IConfigurationSection configurationSection)
        {
            ConfigurationSection section = default;
            int i = 0;
            do
            {
                section = (ConfigurationSection)configurationSection.GetSection(i.ToString());
                if (!section.GetChildren().Any())
                    break;

                FillSection(section);
                var settings = _context.Settings.Where(x => x.SettingSection == ImageGenerationSettings.Name + ":Models:" + i);
                var model = BindSettings<Model>(settings);
                _context.Settings.RemoveRange(settings);
                _context.Models.Add(model);
                _context.SaveChanges();

                i++;
            } while (true);
        }

        void FillSection(IConfigurationSection section)
        {
            foreach (ConfigurationSection item in section.GetChildren())
            {
                if (item.Value != null)
                {
                    _context.Settings.Add(new() { SettingSection = item.Path.Replace(":" + item.Key, string.Empty), SettingsKey = item.Key, SettingsValue = item.Value });
                }
            }
            _context.SaveChanges();
        }

        public IEnumerable<HostInfo> Hosts
        {
            get
            {
                return _mapper.Map<IEnumerable<HostInfo>>(_context.Hosts.OrderBy(x => x.Priority).AsNoTracking());
            }
        }

        public IEnumerable<ModelInfo> Models
        {
            get
            {
                return _mapper.Map<IEnumerable<ModelInfo>>(_context.Models.AsNoTracking());
            }
        }

        public ModelInfo DefaultModel
        {
            get
            {
                var defaultModelName = IGSettings.DefaultModel;
                return _mapper.Map<ModelInfo>(_context.Models.Single(x => x.Name == defaultModelName));
            }
        }

        //public LogLevel LogLevel
        //{
        //    get
        //    {
        //        return Enum.Parse<LogLevel>(_appSettings.GetRequiredSection("Logging/LogLevel/Default").Value);
        //    }
        //    set {
        //        _appSettings.GetRequiredSection("Logging/LogLevel/Default").Value = value.ToString();
        //    }
        //}


        public ImageGenerationSettings IGSettings
        {
            get
            {
                return BindSettings<ImageGenerationSettings>(_context.Settings.Where(x => x.SettingSection == ImageGenerationSettings.Name));
            }
        }

        public Automatic1111Settings AutomaticSettings
        {
            get
            {
                return BindSettings<Automatic1111Settings>(_context.Settings.Where(x => x.SettingSection == Automatic1111Settings.Name));
            }
        }

        public ComfyUISettings ComfySettings
        {
            get
            {
                return BindSettings<ComfyUISettings>(_context.Settings.Where(x => x.SettingSection == ComfyUISettings.Name));
            }
        }

        private T BindSettings<T>(IEnumerable<Settings> settings) where T : class, new()
        {
            var type = typeof(T);
            var obj = Activator.CreateInstance(type);

            foreach (var property in type.GetProperties())
            {
                if (settings.Any(x => x.SettingsKey == property.Name))
                {
                    var config = settings.Single(x => x.SettingsKey == property.Name);

                    if (property.PropertyType.IsEnum)
                    {
                        property.SetValue(obj, Enum.ToObject(property.PropertyType, int.Parse(config.SettingsValue)));
                    }
                    else
                    {
                        property.SetValue(obj, Convert.ChangeType(config.SettingsValue, property.PropertyType, CultureInfo.InvariantCulture));
                    }
                }
            }

            return (T)obj;
        }

    }
}
