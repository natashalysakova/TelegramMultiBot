namespace TelegramMultiBot.Configuration
{
    public class HostSettings
    {
        public static string Name => "Hosts";
        public bool Enabled { get; set; }
        public required int Port { get; set; }
        public required string UI { get; set; }
        public required string Host { get; set; }
        public required string Protocol { get; set; }
        public int Priority { get; set; }

        public Uri Uri { get => new($"{Protocol}://{Host}:{Port}"); }
    }
}