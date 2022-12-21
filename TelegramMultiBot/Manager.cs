using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot
{
    internal abstract class Manager<T>
    {
        protected List<T> list;
        protected CancellationToken token;

        protected List<T>? Load(string fileName)
        {
            var tmp = File.ReadAllText(fileName);
            return JsonConvert.DeserializeObject<List<T>>(tmp);
        }

        protected void Save(string fileName)
        {
            var tmp = JsonConvert.SerializeObject(list);
            File.WriteAllText(fileName, tmp);
            LogUtil.Log(fileName + " saved");
        }
    }
}
