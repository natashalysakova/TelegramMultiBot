namespace TelegramMultiBot.ImageGenerators
{
    interface IDiffusor
    {
        public string UI { get; }
        bool isAvailable();
        Task<IJob> Run(IJob job, string directory);
    }
}
