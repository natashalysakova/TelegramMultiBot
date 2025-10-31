namespace TelegramMultiBot.ImageGeneration.Exceptions;

[Serializable]
internal class RenderFailedException : Exception
{
    public RenderFailedException()
    {
    }

    public RenderFailedException(string? message) : base(message)
    {
    }

    public RenderFailedException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}