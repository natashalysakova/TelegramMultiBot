using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands.Interfaces
{
    internal interface ICommand
    {
        string Command { get; }

        bool CanHandle(Message message);

        bool CanHandle(InlineQuery query);

        bool CanHandle(string query);
        bool CanHandle(MessageReactionUpdated reactions);

        Task Handle(Message message);

        bool CanHandleInlineQuery { get; }
        bool CanHandleCallback { get; }
        bool CanHandleMessageReaction { get; }
    }
}