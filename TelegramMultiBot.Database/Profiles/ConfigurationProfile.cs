using AutoMapper;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Profiles;

public class ConfigurationProfile : Profile
{
    public ConfigurationProfile()
    {
        _ = CreateMap<Host, HostInfo>().ReverseMap();
        _ = CreateMap<Model, ModelInfo>().ReverseMap();
    }
}
