
namespace TelegramMultiBot.Database.Services;

[Serializable]
internal class ConfigurationMissingException : Exception
{
    public ConfigurationMissingException()
    {
    }

    public ConfigurationMissingException(string? message) : base(message)
    {
    }

    public ConfigurationMissingException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}