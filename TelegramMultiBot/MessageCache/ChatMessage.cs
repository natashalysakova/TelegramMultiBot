using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TelegramMultiBot.MessageCache
{
    public record ChatMessage
    {
        public string Text { get; init; }


        public string UserName { get; set; }

        public ChatMessage(string text, string userName)
        {
            Text = text;
            UserName = userName;
        }

        public override string ToString()
        {
            return $"{UserName}:\n{Text}";
        }
    }


}
