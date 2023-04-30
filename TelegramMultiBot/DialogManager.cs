// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Telegram.Bot;
using Telegram.Bot.Types;

internal class DialogManager
{
    List<Dialog> _dialogList;
    public DialogManager()
    {
        _dialogList = new List<Dialog>();
    }

    //public Dialog this [long chatId, DialogType type]
    //{
    //    get
    //    {
    //        return _dialogList.FirstOrDefault(x => x.ChatId == chatId && x.Type == type);
    //    }
    //}

    public Dialog this[long chatId]
    {
        get
        {
            return _dialogList.FirstOrDefault(x => x.ChatId == chatId);
        }

        set
        {
            var existing = _dialogList.FirstOrDefault(x => x.ChatId == chatId && x.Type == value.Type);
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

    internal void Remove(Dialog dialog)
    {
        _dialogList.Remove(dialog);
    }
}

class Dialog
{
    public DialogType Type { get; set; }
    public long ChatId { get; set; }
    public int Step { get; set; }

    public string Name { get; set; }
    public string CRON { get; set; }
    public string Text { get; set; }

    public bool IsDone { get; set; }
}

enum DialogType
{
    AddJob
}


class DialogHandlerFactory
{
    public static IDialogHandler CreateHandler(DialogType type)
    {
        switch (type)
        {
            case DialogType.AddJob:
                return new AddJobHandler();
            default:
                throw new NotImplementedException();
        }
    }
}

interface IDialogHandler
{
    void Handle(Dialog dialog, Message message, TelegramBotClient client);
}

class AddJobHandler : IDialogHandler
{

    public async void Handle(Dialog dialog, Message message, TelegramBotClient client)
    {


        switch (dialog.Step)
        {
            case 0:
                await client.SendTextMessageAsync(dialog.ChatId, "What name of the job?", disableNotification: true);
                dialog.Step++;
                return;
            case 1:
                if (string.IsNullOrEmpty(message.Text))
                {
                    await client.SendTextMessageAsync(dialog.ChatId, "Name is empty. Try again", disableNotification: true);
                    return;
                }
                dialog.Name = message.Text;
                dialog.Step++;
                await client.SendTextMessageAsync(dialog.ChatId, "Enter CRON", disableNotification: true);
                return;
            case 2:
                if (string.IsNullOrEmpty(message.Text))
                {
                    await client.SendTextMessageAsync(dialog.ChatId, "CRON is empty. Try again", disableNotification: true);
                    return;
                }
                try
                {
                    Cronos.CronExpression.Parse(message.Text);
                }
                catch (Exception)
                {
                    await client.SendTextMessageAsync(dialog.ChatId, "CRON is invalid. Try again", disableNotification: true);
                    return;
                }
                dialog.CRON = message.Text;
                dialog.Step++;
                await client.SendTextMessageAsync(dialog.ChatId, "Enter Text Message", disableNotification: true);
                return;
            case 3:
                if (string.IsNullOrEmpty(message.Text))
                {
                    await client.SendTextMessageAsync(dialog.ChatId, "Text is empty. Try again", disableNotification: true);
                    return;
                }

                dialog.Text = message.Text;
                dialog.IsDone = true;
                return;

            default:
                break;
        }
    }
}