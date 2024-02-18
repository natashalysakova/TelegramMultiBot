// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;
using TelegramMultiBot.Commands;

public class DialogManager
{
    List<IDialog> _dialogList;
    private readonly TelegramBotClient _client;
    private readonly IServiceProvider _serviceProvider;

    public DialogManager(TelegramBotClient client, IServiceProvider serviceProvider)
    {
        _dialogList = new List<IDialog>();
        _client = client;
        _serviceProvider = serviceProvider;
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
        using (var scope = _serviceProvider.CreateScope())
        {
            var factory = scope.ServiceProvider.GetService<DialogHandlerFactory>();
            var handler = factory.CreateHandler(activeDialog);
            handler.Handle(activeDialog, message);

            if (activeDialog.IsFinished)
            {
                Remove(activeDialog);
            }

            return Task.CompletedTask;

        }
    }
}
