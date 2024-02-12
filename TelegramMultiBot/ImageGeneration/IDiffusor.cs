namespace TelegramMultiBot.ImageGenerators
{
    interface IDiffusor
    {
        public string UI { get; }
        bool isAvailable();
        Task<ImageJob?> Run(ImageJob job, string directory);
    }
}
