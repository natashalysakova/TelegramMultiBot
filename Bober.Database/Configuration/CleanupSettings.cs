namespace Bober.Database.Configuration
{
    class CleanupSettings
    {
        public int Interval { get; set; }
        public int JobAge { get; set; }
        public bool RemoveFiles { get; set; }

    }
}
