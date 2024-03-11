using Microsoft.Extensions.Configuration;

namespace TelegramMultiBot.Configuration
{
    public class HostSettings
    {
        public static string Name => "Hosts";
        public required bool Enabled { get; set; }
        public required int Port { get; set; }
        public required string UI { get; set; }
        public required string Host { get; set; }
        public required string Protocol { get; set; }
        public required int Priority { get; set; }

        public Uri Uri { get => new Uri($"{Protocol}://{Host}:{Port}"); }
    }
}
