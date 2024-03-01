using Microsoft.Extensions.Configuration;

namespace TelegramMultiBot.Configuration
{
    public class HostSettings
    {
        public static string Name => "Hosts";
        public bool Enabled { get; set; }
        public int Port { get; set; }
        public string UI { get; set; }
        public string Host { get; set; }
        public string Protocol { get; set; }
        public int Priority { get; set; }

        public Uri Uri { get => new Uri($"{Protocol}://{Host}:{Port}"); }
    }
}
