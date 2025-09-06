namespace TelegramMultiBot.MessageCache
{
    public interface IMessageCacheService
    {
        /// <summary>
        /// Добавляет новое сообщение в кэш и применяет политику вытеснения, если необходимо.
        /// </summary>
        /// <param name="message">Сообщение для добавления.</param>
        void AddMessage(ChatMessage message);

        /// <summary>
        /// Возвращает все сохраненные сообщения для указанного чата и/или темы (thread).
        /// </summary>
        /// <param name="chatId">Идентификатор чата.</param>
        /// <param name="threadId">Идентификатор темы (опционально).</param>
        /// <returns>Коллекция сообщений в хронологическом порядке.</returns>
        IEnumerable<ChatMessage> GetContextForChat(long chatId, int? threadId);
    }
}
