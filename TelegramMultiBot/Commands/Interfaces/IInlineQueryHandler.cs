using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands.Interfaces
{
    internal interface IInlineQueryHandler
    {
        Task HandleInlineQuery(InlineQuery inlineQuery);
    }
}