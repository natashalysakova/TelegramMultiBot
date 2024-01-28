// See https://aka.ms/new-console-template for more information
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramMultiBot.Commands;

internal class DialogManager
{
    List<IDialog> _dialogList;
    private readonly TelegramBotClient _client;
    private readonly DialogHandlerFactory _factory;

    public DialogManager(TelegramBotClient client, DialogHandlerFactory factory)
    {
        _dialogList = new List<IDialog>();
        _client = client;
        _factory = factory;
    }

    public IDialog this[long chatId]
    {
        get
        {
            return _dialogList.FirstOrDefault(x => x.ChatId == chatId);
        }

        set
        {
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
        _dialogList.Remove(dialog);
    }

    public Task HandleActiveDialog(Message message, IDialog activeDialog)
    {
        var handler = _factory.CreateHandler(activeDialog);
        handler.Handle(activeDialog, message);

        if (activeDialog.IsFinished)
        {
            Remove(activeDialog);
        }

        return Task.CompletedTask;
    }
}
