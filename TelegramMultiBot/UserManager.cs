using Microsoft.Extensions.Logging;
using System.Collections;

namespace TelegramMultiBot
{
    record ChatUser(long chatId, long userId, string? username) { }

    class UserManager : Manager<ChatUser>, IEnumerable<ChatUser>
    {
        private readonly ILogger<Manager<ChatUser>> _logger;
        protected override string fileName => "users.json";

        public UserManager(ILogger<Manager<ChatUser>> logger) : base(logger)
        {
            _logger = logger;
            try
            {
                list = Load();
                _logger.LogDebug($"Loadded {list.Count} jobs");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex.Message);
                list = new List<ChatUser>();
                _logger.LogDebug($"Created new job list");
            }
        }

        public bool Exist(long chatId, long userId)
        {
            return list.Any(x => x.chatId == chatId && x.userId == userId);
        }

        public void Add(long chatId, long userId, string? username)
        {
            list.Add(new ChatUser(chatId, userId, username));
            Save();
        }

        public void Delete(long chatId, long userId)
        {
            list.RemoveAll(x => x.userId == userId && x.chatId == chatId);
            Save();
        }

        public void DeleteForChat(long chatId)
        {
            list.RemoveAll(x => x.chatId == chatId);
        }

        public IEnumerator<ChatUser> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }
    }
}
