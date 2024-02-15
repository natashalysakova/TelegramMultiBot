using Microsoft.Extensions.Configuration;

namespace Bober.Worker.Configuration
{
    class HostSettings
    {
        public const string Name = "Hosts";

        public bool Enabled { get; set; }
        public int Port { get; set; }
        public string UI { get; set; }
        public string Host { get; set; }
        public string Protocol { get; set; }

        public Uri Uri { get => new Uri($"{Protocol}://{Host}:{Port}"); }
    }
}
