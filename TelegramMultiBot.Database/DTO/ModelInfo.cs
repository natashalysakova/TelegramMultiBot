using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramMultiBot.Database.Enums;

namespace TelegramMultiBot.Database.DTO
{
    public class ModelInfo
    {
        public string Name { get; init; }
        public string Path { get; init; }
        public float CGF { get; init; }
        public int Steps { get; init; }
        public string Sampler { get; init; }
        public string Scheduler { get; init; }
        public int CLIPskip { get; init; } = 1;
        public ModelVersion Version { get; init; }
    }
}
