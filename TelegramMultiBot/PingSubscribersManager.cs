using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot
{
    internal class PingSubscribersManager : Manager<long>
    {
        private const string JobFile = "subscribers.json";

        public PingSubscribersManager()
        {
            try
            {
                list = Load(JobFile);
                LogUtil.Log($"Loadded {list.Count} subscribers");
            }
            catch (Exception ex)
            {
                LogUtil.Log(ex.Message);
                list = new List<long>();
                LogUtil.Log($"Created new subscribers list");
                Save(JobFile);
            }
        }

        public bool UpdateSubscription(long chatId)
        {
            if (list.Contains(chatId))
            {
                list.Remove(chatId);
                Save(JobFile);
                return false;
            }
            else
            {
                list.Add(chatId);
                Save(JobFile);
                return true;
            }
        }

        public IEnumerable<long> GetSubscribers()
        {
            return list;
        }
    }
}
