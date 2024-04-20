using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelegramMultiBot.Database.Models
{
    [PrimaryKey (nameof(Address), nameof(Port))]
    public class Host
    {
        public bool Enabled { get; set; }
        public int Port { get; set; }
        public string UI { get; set; }
        public string Address { get; set; }
        public string Protocol { get; set; }
        public int Priority { get; set; }

    }
}