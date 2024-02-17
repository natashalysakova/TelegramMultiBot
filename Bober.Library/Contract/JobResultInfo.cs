namespace Bober.Library.Contract
{
    public class JobResultInfo 
    { 
        public string Id { get; set; }
        public long Seed { get; set; }
        public string Info { get; set; }
        public long RenderTime { get; set; }
        public string FilePath { get; set; }
    }
}
