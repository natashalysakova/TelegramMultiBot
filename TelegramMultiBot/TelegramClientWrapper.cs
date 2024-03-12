using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramMultiBot.Database.DTO;

namespace TelegramMultiBot
{
    public class TelegramClientWrapper
    {
        private readonly TelegramBotClient _client;

        public long? BotId { get => _client.BotId; }

        public TelegramClientWrapper(TelegramBotClient client)
        {
            _client = client;
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
            return await _client.DeleteMessageAsync(request);
        }

        public async Task<bool> AnswerCallbackQueryAsync(string id, string? text = null, bool showAlert = false)
        {
            var request = new AnswerCallbackQueryRequest()
            {
                CallbackQueryId = id,
                Text = text,
                ShowAlert = showAlert
            };
            return await _client.AnswerCallbackQueryAsync(request);
        }

        public async Task<bool> AnswerInlineQueryAsync(string inlineQueryId, InlineQueryResult[] results)
        {
            var request = new AnswerInlineQueryRequest() { InlineQueryId = inlineQueryId, Results = results };
            return await _client.AnswerInlineQueryAsync(request);
        }

        public async Task<Message> SendMessageAsync(Message message, string text, bool replyToMessage = false, IReplyMarkup? replyMarkup = null, bool? disableNotification = null, ParseMode? parseMode = null, bool? protectContent = null, LinkPreviewOptions? linkPreviewOptions = null)
        {
            var request = new SendMessageRequest()
            {
                ChatId = message.Chat,
                Text = text,
                MessageThreadId = message.IsTopicMessage.HasValue && message.IsTopicMessage.Value ? message.MessageThreadId : null,
                DisableNotification = disableNotification,
                LinkPreviewOptions = linkPreviewOptions,
                ReplyMarkup = replyMarkup,
                ReplyParameters = replyToMessage ? new ReplyParameters() { MessageId = message.MessageId, ChatId = message.Chat, AllowSendingWithoutReply = true } : null,
                ParseMode = parseMode,
                ProtectContent = protectContent
            };
            return await _client.SendMessageAsync(request);
        }

        internal async Task<Message> SendPhotoAsync(ChatId chatId, InputFileStream photo, int? messageThreadId = null, string? caption = null, bool reply = false, int? messageId = null, IReplyMarkup? markup = null, ParseMode? parseMode = null)
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

            return await _client.SendPhotoAsync(request);
        }

        internal async Task<Message> SendPhotoAsync(Message message, InputFileStream photo, string? caption = null, bool reply = false, IReplyMarkup? markup = null, ParseMode? parseMode = null)
        {
            return await SendPhotoAsync(message.Chat, photo, message.IsTopicMessage.HasValue && message.IsTopicMessage.Value ? message.MessageThreadId : null, caption, reply, message.MessageId, markup, parseMode);
        }

        internal async Task<Message> SendPhotoAsync(JobInfo job, InputFileStream photo, string? caption = null, bool reply = false, IReplyMarkup? markup = null, ParseMode? parseMode = null)
        {
            return await SendPhotoAsync(job.ChatId, photo, job.MessageThreadId, caption, reply, job.MessageId, markup, parseMode);
        }

        internal async Task<Message> SendDocumentAsync(Message message, InputFileStream document, string? caption = null, bool reply = false, IReplyMarkup? markup = null, ParseMode? parseMode = null)
        {
            return await SendDocumentAsync(message.Chat, document, message.IsTopicMessage.HasValue && message.IsTopicMessage.Value ? message.MessageThreadId : null, caption, reply, message.MessageId, markup, parseMode);
        }

        internal async Task<Message> SendDocumentAsync(JobInfo job, InputFileStream document, string? caption = null, bool reply = false, IReplyMarkup? markup = null, ParseMode? parseMode = null)
        {
            return await SendDocumentAsync(job.ChatId, document, job.MessageThreadId, caption, reply, job.MessageId, markup, parseMode);
        }

        internal async Task<Message> SendDocumentAsync(ChatId chatId, InputFileStream document, int? messageThreadId = null, string? caption = null, bool reply = false, int? messageId = null, IReplyMarkup? markup = null, ParseMode? parseMode = null)
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

            return await _client.SendDocumentAsync(request);
        }

        internal async Task<Message> EditMessageReplyMarkupAsync(Message message, InlineKeyboardMarkup? keys = null)
        {
            return await EditMessageReplyMarkupAsync(message.Chat, message.MessageId, keys);
        }

        internal async Task<Message> EditMessageReplyMarkupAsync(ChatId chatId, int messageId, InlineKeyboardMarkup? keys = null)
        {
            var editRequest = new EditMessageReplyMarkupRequest()
            {
                ChatId = chatId,
                MessageId = messageId,
                ReplyMarkup = keys
            };

            return await _client.EditMessageReplyMarkupAsync(editRequest);
        }

        internal async Task<Message> EditMessageMediaAsync(Message message, InputMedia media, InlineKeyboardMarkup? keys = null)
        {
            return await EditMessageMediaAsync(message.Chat, message.MessageId, media, keys);
        }

        internal async Task<Message> EditMessageMediaAsync(ChatId chatId, int messageId, InputMedia media, InlineKeyboardMarkup? keys = null)
        {
            var editRequest = new EditMessageMediaRequest()
            {
                ChatId = chatId,
                MessageId = messageId,
                ReplyMarkup = keys,
                Media = media
            };

            return await _client.EditMessageMediaAsync(editRequest);
        }

        public async Task<Message> EditMessageTextAsync(Message message, string text, InlineKeyboardMarkup? keyboardMarkup = null, ParseMode? parseMode = null, LinkPreviewOptions? linkPreviewOptions = null)
        {
            return await EditMessageTextAsync(message.Chat, message.MessageId, text, keyboardMarkup, parseMode, linkPreviewOptions);
        }

        public async Task<Message> EditMessageTextAsync(ChatId chatId, int messageId, string text, InlineKeyboardMarkup? keyboardMarkup = null, ParseMode? parseMode = null, LinkPreviewOptions? linkPreviewOptions = null)
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
            return await _client.EditMessageTextAsync(request);
        }

        public async Task<MessageId> CopyMessageAsync(ChatId destinationChatId, ChatId originalChatId, int messageId)
        {
            var request = new CopyMessageRequest() { ChatId = destinationChatId, FromChatId = originalChatId, MessageId = messageId };
            return await _client.CopyMessageAsync(request);
        }

        public async Task<ChatMember> GetChatMemberAsync(ChatId chatId, long userId)
        {
            var request = new GetChatMemberRequest() { ChatId = chatId, UserId = userId };
            return await _client.GetChatMemberAsync(request);
        }
    }
}