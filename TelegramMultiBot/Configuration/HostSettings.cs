using Microsoft.Extensions.Configuration;

namespace TelegramMultiBot.Configuration
{
    class HostSettings
    {
        public int Port { get; set; }
        public string UI { get; set; }
        public string Host { get; set; }
        public string Protocol { get; set; }

        public Uri Uri { get => new Uri($"{Protocol}://{Host}:{Port}"); }
    }
}
