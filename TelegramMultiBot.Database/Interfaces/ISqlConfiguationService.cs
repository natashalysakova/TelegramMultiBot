using TelegramMultiBot.Database.DTO;

namespace TelegramMultiBot.Database.Interfaces
{
    public interface ISqlConfiguationService
    {
        public IEnumerable<HostInfo> Hosts { get; }
        public IEnumerable<ModelInfo> Models { get; }
        public ModelInfo DefaultModel { get;  }
        public ImageGenerationSettings IGSettings { get;  }
        public Automatic1111Settings AutomaticSettings { get; }
        public ComfyUISettings ComfySettings { get; }
        public GeneralSettings GeneralSettings { get; }

    }


}
