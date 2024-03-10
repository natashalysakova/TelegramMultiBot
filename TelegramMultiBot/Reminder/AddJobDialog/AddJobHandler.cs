// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

class AddJobHandler : BaseDialogHandler<AddJobDialog>
{

    private readonly TelegramBotClient _client;
    private readonly JobManager _jobManager;

    public AddJobHandler(TelegramBotClient client, JobManager jobManager)
    {
        _client = client;
        _jobManager = jobManager;

    }

    public override Func<AddJobDialog, Message, Task<bool>> GetHandler(AddJobDialog dialog)
    {
        var state = dialog.State;

        switch (state)
        {
            case AddDialogState.Start:
                return Start;
            case AddDialogState.CheckName:
                return CheckName;
            case AddDialogState.CheckCron:
                return CheckCron;
            case AddDialogState.CheckMessage:
                return CheckMessage;
            default:
                return (job, message) => { return Task.Run(() => { return false; }); };
        }
    }

    private async Task<bool> CheckMessage(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            await SendMessage("Повідомлення порожнє. Спробуйте знову", message);
            return false;
        }

        dialog.Text = message.Text;
        dialog.IsFinished = true;

        _jobManager.AddJob(dialog.ChatId, dialog.Name, dialog.CRON, dialog.Text);
        await SendMessage($"Завдання додано: {dialog.Name} ({dialog.CRON}) з текстом: {dialog.Text}", message);

        return true;
    }

    private async Task<bool> CheckCron(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            await SendMessage("CRON порожній. Спробуйте знову", message);
            return false;
        }
        try
        {
            Cronos.CronExpression.Parse(message.Text);
        }
        catch (Exception)
        {
            await SendMessage("CRON не валідний. Спробуйте знову. Більше про CRON можна дізнатися за посиланням https://crontab.guru", message);
            return false;
        }

        


        dialog.CRON = message.Text;
        await SendMessage("Введіть повідомлення, яке буде відправлятися", message);
        return true;

    }

    

    private async Task<bool> Start(AddJobDialog dialog, Message message)
    {
        await SendMessage("Дайте назву завдання", message);
        return true;
    }

    async Task<bool> CheckName(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text) || message.Text.StartsWith('/'))
        {
            await SendMessage("Ім'я не валідне. Спробуйте знову", message);
            return false;
        }
        dialog.Name = message.Text;
        await SendMessage("Введіть CRON", message);
        return true;
    }

    private async Task SendMessage(string text, Message message)
    {
        var request = new SendMessageRequest()
        {
            ChatId = message.Chat,
            Text = text,
            DisableNotification = true,
            MessageThreadId = message.MessageThreadId,
            LinkPreviewOptions = new LinkPreviewOptions() { IsDisabled = true }
        };
        await _client.SendMessageAsync(request);
    }
}
