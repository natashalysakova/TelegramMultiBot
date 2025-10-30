using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.Commands
{
    [ServiceKey("buttons")]
    internal class ButtonsCommand(TelegramClientWrapper client, IImageDatabaseService databaseService) : BaseCommand
    {
        public async override Task Handle(Message message)
        {
            var replyMessage = message.ReplyToMessage;

            if (replyMessage == null || replyMessage.ReplyMarkup != null || replyMessage.Type != MessageType.Photo)
            {
                await DeleteMessage(message);
                return;
            }

            string fileId = replyMessage.Photo.Last().FileId;

            var job = databaseService.GetJobByFileId(fileId);
            if (job == null)
            {
                await DeleteMessage(message);
                return;
            }
                
            var result = job.Results.Single(x => x.FileId == fileId);

            var keys = ImagineCommand.GetReplyMarkupForJob(job.Type, result.Id, job.UpscaleModifyer, prompt: job.Text);

            var caption = $"#seed:{result.Seed}\nRender time: {TimeSpan.FromMilliseconds(result.RenderTime)}\n" + result.Info;

            //singlePhoto, not album
            if (replyMessage.MediaGroupId is null)
            {

                if (caption.Length > 1024)
                {
                    await client.SendMessageAsync(replyMessage, caption, true);
                    await client.EditMessageReplyMarkupAsync(replyMessage, keys);
                }
                else
                {
                    var media = new InputMediaPhoto() { Media = InputFile.FromFileId(result.FileId), Caption = caption };
                    await client.EditMessageMediaAsync(replyMessage, media, keys);
                }
            }
            else
            {
                await client.SendMessageAsync(replyMessage, caption, true, keys);
            }

            await DeleteMessage(message);
        }

        private async Task DeleteMessage(Message message)
        {
            var bot = await client.GetChatMemberAsync(message.Chat.Id, client.BotId.Value);
            if (bot.Status == ChatMemberStatus.Administrator || message.Chat.Type == ChatType.Private)
            {
                await client.DeleteMessageAsync(message.Chat, message.MessageId);
            }
        }
    }
}
