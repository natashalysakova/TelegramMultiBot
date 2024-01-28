// See https://aka.ms/new-console-template for more information
using Telegram.Bot.Types;

interface IDialogHandler
{
    void Handle(IDialog dialog, Message message);
    bool CanHandle(IDialog type);

}
