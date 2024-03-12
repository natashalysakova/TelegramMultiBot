namespace TelegramMultiBot.ImageGeneration.Exceptions
{
    [Serializable]
    public class SdNotAvailableException : Exception
    {
        public SdNotAvailableException()
        { }

        public SdNotAvailableException(string message) : base(message)
        {
        }

        public SdNotAvailableException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}