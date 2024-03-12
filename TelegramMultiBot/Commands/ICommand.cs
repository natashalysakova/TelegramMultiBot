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

    internal interface ICallbackHandler
    {
        Task HandleCallback(CallbackQuery callbackQuery);
    }

    internal interface IInlineQueryHandler
    {
        Task HandleInlineQuery(InlineQuery inlineQuery);
    }
}