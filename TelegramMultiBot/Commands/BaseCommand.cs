using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramMultiBot.Commands.Interfaces;

namespace TelegramMultiBot.Commands
{
    internal abstract class BaseCommand : ICommand
    {
        public bool CanHandleInlineQuery { get => GetType().IsAssignableTo(typeof(IInlineQueryHandler)); }
        public bool CanHandleCallback { get => GetType().IsAssignableTo(typeof(ICallbackHandler)); }
        public bool CanHandleMessageReaction { get => GetType().IsAssignableTo(typeof(IMessageReactionHandler)); }

        public string Command
        {
            get
            {
                var type = GetType();

                var attribute = type.GetAttributeValue(
                    (ServiceKeyAttribute att) =>
                    {
                        return att.Command;
                    });
                return attribute ?? throw new InvalidOperationException("Cannot find command");
            }
        }

        public string Description
        {
            get
            {
                var type = GetType();

                var attribute = type.GetAttributeValue(
                    (ServiceKeyAttribute att) =>
                    {
                        return att.Description;
    
                    });
                return attribute ?? throw new InvalidOperationException("Cannot find command");
            }
        }

        public bool IsPublic
        {
            get
            {
                var type = GetType();

                var attribute = type.GetAttributeValue(
                    (ServiceKeyAttribute att) =>
                    {
                        return att.IsPublic;

                    });
                return attribute;
            }
        }

        public virtual bool CanHandle(Message message)
        {
            if (message.Text is null)
                return false;

            if (message.Entities != null
                && message.Entities.Any(x => x.Type == MessageEntityType.BotCommand)
                && (message.Text.StartsWith('/') || message.Text.StartsWith($"@{BotService.BotName} /")))

            {
                if (message.EntityValues is null)
                    return false;

                var value = message.EntityValues.ElementAt(0);
                if (value.StartsWith('@'))
                {
                    value = message.EntityValues.ElementAt(1);
                }

                if (value.Contains('@'))
                {
                    return value.Equals($"/{Command}@{BotService.BotName}");
                }
                else
                {
                    return value.StartsWith('/' + Command);
                }
            }

            return false;
        }

        public virtual bool CanHandle(InlineQuery query)
        {
            return query.Query.StartsWith($"@{BotService.BotName} /{Command}", StringComparison.InvariantCultureIgnoreCase) || query.Query.StartsWith($"/{Command}", StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual bool CanHandle(string query)
        {
            return query.Split("|", StringSplitOptions.RemoveEmptyEntries)[0] == Command;
        }

        public virtual bool CanHandle(MessageReactionUpdated reactions)
        {
            return false;
        }

        public abstract Task Handle(Message message);
    }

    public static class AttributeExtensions
    {
        public static TValue? GetAttributeValue<TAttribute, TValue>(
            this Type type,
            Func<TAttribute, TValue> valueSelector)
            where TAttribute : Attribute
        {
            if (type.GetCustomAttributes(typeof(TAttribute), true).FirstOrDefault() is TAttribute att)
            {
                return valueSelector(att);
            }
            return default;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    internal sealed class ServiceKeyAttribute(string command, string description, bool isPublic = true) : Attribute
    {
        public string Command
        {
            get => command;
        }

        public string Description 
        {
            get => description;
        }

        public bool IsPublic
        {
            get => isPublic;
        }
    }
}