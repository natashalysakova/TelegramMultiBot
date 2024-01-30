// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

class AddJobHandler : BaseDialogHandler<AddJobDialog>
{

    private readonly TelegramBotClient _client;
    private readonly ILogger _logger;
    private readonly JobManager _jobManager;

    public AddJobHandler(TelegramBotClient client, ILogger<AddJobHandler> logger, JobManager jobManager)
    {
        _client = client;
        _logger = logger;
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
                return null;
        }
    }

    private async Task<bool> CheckMessage(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            await _client.SendTextMessageAsync(dialog.ChatId, "Повідомлення порожнє. Спробуйте знову", disableNotification: true, messageThreadId: message.MessageThreadId);
            return false;
        }

        dialog.Text = message.Text;
        dialog.IsFinished = true;

        _jobManager.AddJob(dialog.ChatId, dialog.Name, dialog.CRON, dialog.Text);
        await _client.SendTextMessageAsync(dialog.ChatId, "Завдання додано: ", disableNotification: true, messageThreadId: message.MessageThreadId);

        return true;
    }

    private async Task<bool> CheckCron(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            await _client.SendTextMessageAsync(dialog.ChatId, "CRON порожній. Спробуйте знову", disableNotification: true, messageThreadId: message.MessageThreadId);
            return false;
        }
        try
        {
            Cronos.CronExpression.Parse(message.Text);
        }
        catch (Exception)
        {
            await _client.SendTextMessageAsync(dialog.ChatId, "CRON не валідний. Спробуйте знову", disableNotification: true, messageThreadId: message.MessageThreadId);
            return false;
        }
        dialog.CRON = message.Text;
        await _client.SendTextMessageAsync(dialog.ChatId, "Введіть повідомлення, яке буде відправлятися", disableNotification: true);
        return true;

    }

    private async Task<bool> Start(AddJobDialog dialog, Message message)
    {
        await _client.SendTextMessageAsync(dialog.ChatId, "Дайте назву завдання", disableNotification: true, messageThreadId: message.MessageThreadId);
        return true;
    }

    async Task<bool> CheckName(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text) || message.Text.StartsWith('/'))
        {
            await _client.SendTextMessageAsync(dialog.ChatId, "Ім'я не валідне. Спробуйте знову", disableNotification: true, messageThreadId: message.MessageThreadId);
            return false;
        }
        dialog.Name = message.Text;
        await _client.SendTextMessageAsync(dialog.ChatId, "Введіть CRON", disableNotification: true, messageThreadId: message.MessageThreadId);
        return true;

    }
}
