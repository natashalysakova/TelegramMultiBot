namespace TelegramMultiBot.ImageGeneration.Exceptions
{
    [Serializable]
    internal class AlreadyRunningException : Exception
    {
        public AlreadyRunningException()
        {
        }

        public AlreadyRunningException(string? message) : base(message)
        {
        }

        public AlreadyRunningException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}