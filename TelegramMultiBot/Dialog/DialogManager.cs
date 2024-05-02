// See https://aka.ms/new-console-template for more information
using Telegram.Bot.Types;

internal class DialogManager(DialogHandlerFactory factory)
{
    private readonly List<IDialog> _dialogList = [];

    public IDialog? this[long chatId, long userId]
    {
        get
        {
            return _dialogList.FirstOrDefault(x => x.ChatId == chatId && x.UserId == userId);
        }
    }

    internal void Remove(IDialog dialog)
    {
        _ = _dialogList.Remove(dialog);
    }

    public Task HandleActiveDialog(Message message, IDialog activeDialog)
    {
        var handler = factory.CreateHandler(activeDialog);
        handler.Handle(activeDialog, message);

        if (activeDialog.IsFinished)
        {
            Remove(activeDialog);
        }

        return Task.CompletedTask;
    }

    internal void Add(IDialog dialog)
    {
        if (dialog is null)
            return;

        var existing = _dialogList.FirstOrDefault(x => x.ChatId == dialog.ChatId && x.UserId == dialog.UserId && x.GetType() == dialog.GetType());
        if (existing != null)
        {
            existing = dialog;
        }
        else
        {
            _dialogList.Add(dialog);
        }

    }
}