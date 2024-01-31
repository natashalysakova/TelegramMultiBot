namespace TelegramMultiBot.ImageGenerators.ComfyUI
{
    class ComfyUI : IDiffusor
    {
        public string UI => throw new NotImplementedException();

        public bool isAvailable()
        {
            return false;
        }

        public Task<GenerationJob> Run(GenerationJob job, string directory)
        {
            throw new NotImplementedException();
        }
    }
}
