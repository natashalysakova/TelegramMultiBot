namespace Bober.Worker.Interfaces
{
    public interface IDiffusor
    {
        public string UI { get; }
        bool isAvailable();
        Task<ImageJob?> Run(ImageJob job);
    }
}
