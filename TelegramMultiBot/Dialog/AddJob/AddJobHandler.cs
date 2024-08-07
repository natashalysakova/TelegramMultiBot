// See https://aka.ms/new-console-template for more information
using Telegram.Bot.Types;
using TelegramMultiBot;

internal class AddJobHandler(TelegramClientWrapper client, JobManager jobManager) : BaseDialogHandler<AddJobDialog>
{
    public override Func<AddJobDialog, Message, Task<bool>> GetHandler(AddJobDialog dialog)
    {
        var state = dialog.State;

        return state switch
        {
            AddDialogState.Start => Start,
            AddDialogState.CheckName => CheckName,
            AddDialogState.CheckCron => CheckCron,
            AddDialogState.CheckMessage => CheckMessage,
            _ => (job, message) => { return Task.Run(() => { return false; }); }
        };
    }

    private async Task<bool> CheckMessage(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text) && message.Photo.Length == 0)
        {
            await client.SendMessageAsync(message, "Повідомлення порожнє. Спробуйте знову");
            return false;
        }

        string photoId = string.Empty;

        dialog.Attachment = message.Photo != null;       
        if (dialog.Attachment)
        {
            photoId = message.Photo.Last().FileId;
            dialog.Text = message.Caption;
        }
        else
        {
            dialog.Text = message.Text;
        }

        dialog.IsFinished = true;

        _ = jobManager.AddJob(dialog.ChatId, dialog.Name, dialog.CRON, dialog.Text, photoId);
        var responce = $"Завдання додано: {dialog.Name} ({dialog.CRON}) з текстом: {dialog.Text}";
        if (dialog.Attachment)
        {
            responce += " та зображенням";
        }

        await client.SendMessageAsync(message, responce);

        return true;
    }

    private async Task<bool> CheckCron(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text))
        {
            await client.SendMessageAsync(message, "CRON порожній. Спробуйте знову");
            return false;
        }
        try
        {
            _ = Cronos.CronExpression.Parse(message.Text);
        }
        catch (Exception)
        {
            await client.SendMessageAsync(message, "CRON не валідний. Спробуйте знову. Більше про CRON можна дізнатися за посиланням https://crontab.guru");
            return false;
        }

        dialog.CRON = message.Text;
        await client.SendMessageAsync(message, "Введіть повідомлення, яке буде відправлятися");
        return true;
    }

    private async Task<bool> Start(AddJobDialog dialog, Message message)
    {
        await client.SendMessageAsync(message, "Дайте назву завдання");
        return true;
    }

    private async Task<bool> CheckName(AddJobDialog dialog, Message message)
    {
        if (string.IsNullOrEmpty(message.Text) || message.Text.StartsWith('/'))
        {
            await client.SendMessageAsync(message, "Ім'я не валідне. Спробуйте знову");
            return false;
        }
        dialog.Name = message.Text;
        await client.SendMessageAsync(message, "Введіть CRON");
        return true;
    }
}