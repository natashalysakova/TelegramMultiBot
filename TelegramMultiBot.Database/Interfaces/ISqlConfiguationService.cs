using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Models;

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

    }


}
