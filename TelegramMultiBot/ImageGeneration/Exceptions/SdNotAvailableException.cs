namespace TelegramMultiBot.ImageGeneration.Exceptions
{
    [Serializable]
    public class SdNotAvailableException : Exception
    {
        public SdNotAvailableException() { }
        public SdNotAvailableException(string message) : base(message) { }
        public SdNotAvailableException(string message, Exception inner) : base(message, inner) { }
        protected SdNotAvailableException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
