// See https://aka.ms/new-console-template for more information
using Telegram.Bot.Types;

internal class DialogManager(DialogHandlerFactory factory)
{
    private readonly List<IDialog> _dialogList = [];

    public IDialog? this[long chatId]
    {
        get
        {
            return _dialogList.FirstOrDefault(x => x.ChatId == chatId);
        }

        set
        {
            if (value is null)
                return;

            var existing = _dialogList.FirstOrDefault(x => x.ChatId == chatId && x.GetType() == value.GetType());
            if (existing != null)
            {
                existing = value;
            }
            else
            {
                _dialogList.Add(value);
            }
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
}