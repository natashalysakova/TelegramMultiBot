using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Database.DTO;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot;

public class TelegramClientWrapper(TelegramBotClient client, IBotMessageDatabaseService databaseService)
{
    public long? BotId { get => client.BotId; }
    public async Task<User> GetMeAsync()
    {
        return await client.GetMe();
    }

    public async Task<bool> DeleteMessageAsync(Message message)
    {
        return await DeleteMessageAsync(message.Chat, message.MessageId);
    }

    public async Task<bool> DeleteMessageAsync(ChatId chatId, int messageId)
    {
        var request = new DeleteMessageRequest()
        {
            ChatId = chatId,
            MessageId = messageId
        };
        return await client.SendRequest(request);
    }

    public async Task<bool> AnswerCallbackQueryAsync(string id, string? text = null, bool showAlert = false)
    {
        var request = new AnswerCallbackQueryRequest()
        {
            CallbackQueryId = id,
            Text = text,
            ShowAlert = showAlert
        };
        return await client.SendRequest(request);
    }

    public async Task<bool> AnswerInlineQueryAsync(string inlineQueryId, InlineQueryResult[] results)
    {
        var request = new AnswerInlineQueryRequest() { InlineQueryId = inlineQueryId, Results = results };
        return await client.SendRequest(request);
    }

    public async Task<Message> SendMessageAsync(ChatId chatId, string text, ReplyMarkup? replyMarkup = null, int? messageThreadId = null, int? replyMessageId = null, bool disableNotification = default, ParseMode parseMode = default, bool protectContent = default, LinkPreviewOptions? linkPreviewOptions = null)
    {
        var request = new SendMessageRequest()
        {
            ChatId = chatId,
            Text = text,
            MessageThreadId = messageThreadId,
            DisableNotification = disableNotification,
            LinkPreviewOptions = linkPreviewOptions,
            ReplyMarkup = replyMarkup,
            ReplyParameters = replyMessageId.HasValue ? new ReplyParameters() { MessageId = replyMessageId.Value, ChatId = chatId, AllowSendingWithoutReply = true } : null,
            ParseMode = parseMode,
            ProtectContent = protectContent
        };
        var botMessage = await client.SendRequest(request);
        databaseService.AddMessage(new(botMessage.Chat.Id, botMessage.MessageId, botMessage.Chat.Type == ChatType.Private, botMessage.Date));
        return botMessage;
    }
    public async Task<Message> SendMessageAsync(Message message, string text, bool replyToMessage = false, ReplyMarkup? replyMarkup = null, bool disableNotification = default, ParseMode parseMode = default, bool protectContent = default, LinkPreviewOptions? linkPreviewOptions = null)
    {
        var messageThreadId = message.IsTopicMessage ? message.MessageThreadId : null;
        var replyToMessageId = message.ReplyToMessage is null ? message.MessageId : message.ReplyToMessage.MessageId;
        var chatId = message.Chat;

        return await SendMessageAsync(chatId, text, replyMarkup, messageThreadId, replyToMessageId, disableNotification, parseMode, protectContent, linkPreviewOptions);
    }

    internal async Task<Message> SendPhotoAsync(ChatId chatId, InputFile photo, int? messageThreadId = null, string? caption = null, bool reply = false, int? messageId = null, ReplyMarkup? markup = null, ParseMode parseMode = default)
    {
        var request = new SendPhotoRequest()
        {
            ChatId = chatId,
            Photo = photo,
            MessageThreadId = messageThreadId,
            Caption = caption,
            ParseMode = parseMode,
            ReplyMarkup = markup,
            ReplyParameters = reply && messageId.HasValue ? new ReplyParameters() { MessageId = messageId.Value, ChatId = chatId } : null
        };
        var botMessage = await client.SendRequest(request);
        databaseService.AddMessage(new(botMessage.Chat.Id, botMessage.MessageId, botMessage.Chat.Type == ChatType.Private, botMessage.Date));
        return botMessage;
    }

    internal async Task<Message> SendPhotoAsync(Message message, InputFileStream photo, string? caption = null, bool reply = false, ReplyMarkup? markup = null, ParseMode parseMode = default)
    {
        return await SendPhotoAsync(message.Chat, photo, message.IsTopicMessage ? message.MessageThreadId : null, caption, reply, message.MessageId, markup, parseMode);
    }

    internal async Task<Message> SendPhotoAsync(JobInfo job, InputFileStream photo, string? caption = null, bool reply = false, ReplyMarkup? markup = null, ParseMode parseMode = default)
    {
        return await SendPhotoAsync(job.ChatId, photo, job.MessageThreadId, caption, reply, job.MessageId, markup, parseMode);
    }

    internal async Task<Message> SendDocumentAsync(Message message, InputFileStream document, string? caption = null, bool reply = false, ReplyMarkup? markup = null, ParseMode parseMode = default)
    {
        return await SendDocumentAsync(message.Chat, document, message.IsTopicMessage ? message.MessageThreadId : null, caption, reply, message.MessageId, markup, parseMode);
    }

    internal async Task<Message> SendDocumentAsync(JobInfo job, InputFileStream document, string? caption = null, bool reply = false, ReplyMarkup? markup = null, ParseMode parseMode = default)
    {
        return await SendDocumentAsync(job.ChatId, document, job.MessageThreadId, caption, reply, job.MessageId, markup, parseMode);
    }

    internal async Task<Message> SendDocumentAsync(ChatId chatId, InputFileStream document, int? messageThreadId = null, string? caption = null, bool reply = false, int? messageId = null, ReplyMarkup? markup = null, ParseMode parseMode = default)
    {
        var request = new SendDocumentRequest()
        {
            ChatId = chatId,
            Document = document,
            MessageThreadId = messageThreadId,
            Caption = caption,
            ParseMode = parseMode,
            ReplyMarkup = markup,
            ReplyParameters = reply && messageId.HasValue ? new ReplyParameters() { MessageId = messageId.Value, ChatId = chatId } : null
        };

        var botMessage = await client.SendRequest(request);
        databaseService.AddMessage(new(botMessage.Chat.Id, botMessage.MessageId, botMessage.Chat.Type == ChatType.Private, botMessage.Date));
        return botMessage;
    }
    internal async Task<Message[]> SendMediaAlbumAsync(JobInfo job, IEnumerable<IAlbumInputMedia> media)
    {
        return await SendMediaAlbumAsync(job.ChatId, media, job.MessageThreadId, job.MessageId);
    }
    internal async Task<Message[]> SendMediaAlbumAsync(ChatId chatId, IEnumerable<IAlbumInputMedia> media, int? messageThreadId = null, int? replyToMessageId = null)
    {
        var request = new SendMediaGroupRequest()
        {
            ChatId = chatId,
            Media = media,
            MessageThreadId = messageThreadId,
            ReplyParameters = replyToMessageId.HasValue ? new ReplyParameters() { MessageId = replyToMessageId.Value, ChatId = chatId } : null,
        };

        var botMessages = await client.SendRequest(request);
        foreach (var item in botMessages)
        {
            databaseService.AddMessage(new(item.Chat.Id, item.MessageId, item.Chat.Type == ChatType.Private, item.Date));
        }
        return botMessages;
    }

    internal async Task<Message> EditMessageReplyMarkupAsync(Message message, InlineKeyboardMarkup? keys = null)
    {
        return await EditMessageReplyMarkupAsync(message.Chat, message.MessageId, keys);
    }

    internal async Task<Message> EditMessageReplyMarkupAsync(ChatId chatId, int messageId, InlineKeyboardMarkup? keys = null)
    {
        var request = new EditMessageReplyMarkupRequest()
        {
            ChatId = chatId,
            MessageId = messageId,
            ReplyMarkup = keys
        };

        return await client.SendRequest(request);
    }

    internal async Task<Message> EditMessageMediaAsync(Message message, InputMedia media, InlineKeyboardMarkup? keys = null)
    {
        return await EditMessageMediaAsync(message.Chat, message.MessageId, media, keys);
    }

    internal async Task<Message> EditMessageMediaAsync(ChatId chatId, int messageId, InputMedia media, InlineKeyboardMarkup? keys = null)
    {
        var request = new EditMessageMediaRequest()
        {
            ChatId = chatId,
            MessageId = messageId,
            ReplyMarkup = keys,
            Media = media
        };

        return await client.SendRequest(request);
    }

    public async Task<Message> EditMessageTextAsync(Message message, string text, InlineKeyboardMarkup? keyboardMarkup = null, ParseMode parseMode = default, LinkPreviewOptions? linkPreviewOptions = null)
    {
        return await EditMessageTextAsync(message.Chat, message.MessageId, text, keyboardMarkup, parseMode, linkPreviewOptions);
    }

    public async Task<Message> EditMessageTextAsync(ChatId chatId, int messageId, string text, InlineKeyboardMarkup? keyboardMarkup = null, ParseMode parseMode = default, LinkPreviewOptions? linkPreviewOptions = null)
    {
        var request = new EditMessageTextRequest()
        {
            MessageId = messageId,
            ChatId = chatId,
            Text = text,
            ReplyMarkup = keyboardMarkup,
            ParseMode = parseMode,
            LinkPreviewOptions = linkPreviewOptions
        };
        return await client.SendRequest(request);
    }

    public async Task<MessageId> CopyMessageAsync(ChatId destinationChatId, ChatId originalChatId, int messageId)
    {
        var request = new CopyMessageRequest() { ChatId = destinationChatId, FromChatId = originalChatId, MessageId = messageId };
        return await client.SendRequest(request);
    }

    public async Task<ChatMember> GetChatMemberAsync(ChatId chatId, long userId)
    {
        var request = new GetChatMemberRequest() { ChatId = chatId, UserId = userId };
        return await client.SendRequest(request);
    }

    internal async Task<string> GetFileUrl(string fileId)
    {
        var request = new GetFileRequest() { FileId = fileId };
        var file = await client.SendRequest(request);
        return file.FilePath;
    }

    internal async Task DownloadFile(string filePath, string destination)
    {
        await using Stream fileStream = System.IO.File.Create(destination);
        await client.DownloadFile(filePath, fileStream);
    }
}