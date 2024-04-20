using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TelegramMultiBot.Database.DTO
{
    public class HostInfo
    {
        public bool Enabled { get; init; }
        public int Port { get; init; }
        public string UI { get; init; }
        public string Address { get; init; }
        public string Protocol { get; init; }
        public int Priority { get; set; }

        public Uri Uri { get => new($"{Protocol}://{Address}:{Port}"); }

    }
}
