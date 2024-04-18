// See https://aka.ms/new-console-template for more information
using Telegram.Bot.Types;

internal interface IMessageReactionHandler
{
    Task HandleMessageReactionUpdate(MessageReactionUpdated messageReaction);

}