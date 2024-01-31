namespace TelegramMultiBot.ImageGenerators
{
    interface IDiffusor
    {
        public string UI { get; }
        bool isAvailable();
        Task<GenerationJob> Run(GenerationJob job, string directory);
    }
}
