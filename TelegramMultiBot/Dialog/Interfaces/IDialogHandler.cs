// See https://aka.ms/new-console-template for more information
using Telegram.Bot.Types;

internal interface IDialogHandler
{
    void Handle(IDialog dialog, Message message);

    bool CanHandle(IDialog type);
}