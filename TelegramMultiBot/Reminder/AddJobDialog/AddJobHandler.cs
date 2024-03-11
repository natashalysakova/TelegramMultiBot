// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using TelegramMultiBot;

class AddJobHandler : BaseDialogHandler<AddJobDialog>
{

    private readonly TelegramClientWrapper _client;
    private readonly JobManager _jobManager;

    public AddJobHandler(TelegramClientWrapper client, JobManager jobManager)
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
            await _client.SendMessageAsync(message, "Повідомлення порожнє. Спробуйте знову");
            return false;
        }

        dialog.Text = message.Text;
        dialog.IsFinished = true;

        _jobManager.AddJob(dialog.ChatId, dialog.Name, dialog.CRON, dialog.Text);
        await _client.SendMessageAsync(message, $"Завдання додано: {dialog.Name} ({dialog.CRON}) з текстом: {dialog.Text}");

        return true;
    }

    private async Task<bool> CheckCron(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            await _client.SendMessageAsync(message, "CRON порожній. Спробуйте знову");
            return false;
        }
        try
        {
            Cronos.CronExpression.Parse(message.Text);
        }
        catch (Exception)
        {
            await _client.SendMessageAsync(message, "CRON не валідний. Спробуйте знову. Більше про CRON можна дізнатися за посиланням https://crontab.guru");
            return false;
        }

        


        dialog.CRON = message.Text;
        await _client.SendMessageAsync(message, "Введіть повідомлення, яке буде відправлятися");
        return true;

    }

    

    private async Task<bool> Start(AddJobDialog dialog, Message message)
    {
        await _client.SendMessageAsync(message, "Дайте назву завдання");
        return true;
    }

    async Task<bool> CheckName(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text) || message.Text.StartsWith('/'))
        {
            await _client.SendMessageAsync(message, "Ім'я не валідне. Спробуйте знову");
            return false;
        }
        dialog.Name = message.Text;
        await _client.SendMessageAsync(message, "Введіть CRON");
        return true;
    }
}
