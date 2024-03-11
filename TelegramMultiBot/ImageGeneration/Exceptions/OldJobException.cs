using System.Runtime.Serialization;

namespace TelegramMultiBot.ImageGeneration.Exceptions;

[Serializable]
internal class OldJobException : Exception
{
    public OldJobException()
    {
    }

    public OldJobException(string? message) : base(message)
    {
    }

    public OldJobException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}