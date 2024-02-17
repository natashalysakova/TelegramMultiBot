using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.Configuration
{
    internal class ApiHostSettings
    {
        public static string Name = "api-server";

        public string host { get; set; }
        public int port { get; set; }
    }
}
