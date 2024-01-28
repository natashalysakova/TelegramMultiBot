using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Types;


namespace TelegramMultiBot.Commands
{
    internal interface ICommand
    {
        bool CanHandle(string textCommand);
        void Handle(Message message);
        void HandleCallback(CallbackData callbackData);
    }
}
