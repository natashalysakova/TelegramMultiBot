// See https://aka.ms/new-console-template for more information
using Telegram.Bot.Types;

abstract class BaseDialogHandler<T> : IDialogHandler where T : class, IDialog
{
    public void Handle(IDialog dialog, Message message)
    {
        var castedDialog = dialog as T ?? throw new NullReferenceException("dialog is not type of " + typeof(T).Name);

        var handler = GetHandler(castedDialog);

        if (handler(castedDialog, message).Result)
        {
            dialog.SetNextState();
        }
    }

    abstract public Func<T, Message, Task<bool>> GetHandler(T dialog);

    public bool CanHandle(IDialog type)
    {
        return typeof(T) == type.GetType();
    }
}
