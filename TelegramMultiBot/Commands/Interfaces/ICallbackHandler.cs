using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands.Interfaces;

internal interface ICallbackHandler
{
    Task HandleCallback(CallbackQuery callbackQuery);
}