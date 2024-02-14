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
        string Command { get; }

        bool CanHandle(Message message);
        bool CanHandle(InlineQuery query);
        bool CanHandle(string query);
        Task Handle(Message message);
        bool CanHandleInlineQuery { get; }
        bool CanHandleCallback { get; }
    }

    interface ICallbackHandler
    {
        Task HandleCallback(CallbackQuery callbackQuery);
    }

    interface IInlineQueryHandler
    {
        Task HandleInlineQuery(InlineQuery inlineQuery);
    }
}
