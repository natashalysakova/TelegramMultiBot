namespace TelegramMultiBot.ImageGeneration.Exceptions;

[Serializable]
internal class InputException : Exception
{
    public InputException()
    {
    }

    public InputException(string? message) : base(message)
    {
    }

    public InputException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}