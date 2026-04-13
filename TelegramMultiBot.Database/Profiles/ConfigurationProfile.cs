using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Models;

namespace TelegramMultiBot.Database.Mappers;

public static partial class Mappers
{
    public static HostInfo ToHostInfo(this Host host)
    {
        return new HostInfo
        {
            Address = host.Address,
            Enabled = host.Enabled,
            Port = host.Port,
            Priority = host.Priority,
            Protocol = host.Protocol,
            UI = host.UI
        };
    }

    public static Host ToHostInfo(this HostInfo host)
    {
        return new Host
        {
            Address = host.Address,
            Enabled = host.Enabled,
            Port = host.Port,
            Priority = host.Priority,
            Protocol = host.Protocol,
            UI = host.UI
        };
    }

    public static ModelInfo ToModelInfo(this Model model)
    {
        return new ModelInfo()
        {
            CGF = model.CGF,
            CLIPskip = model.CLIPskip,
            Name = model.Name,
            Path = model.Path,
            Sampler = model.Sampler,
            Scheduler = model.Scheduler,
            Steps = model.Steps,
            Version= model.Version,
        };
    }

    public static Model ToModel(this ModelInfo model)
    {
        return new Model()
        {
            CGF = model.CGF,
            CLIPskip = model.CLIPskip,
            Name = model.Name,
            Path = model.Path,
            Sampler = model.Sampler,
            Scheduler = model.Scheduler,
            Steps = model.Steps,
            Version = model.Version,
        };
    }
}