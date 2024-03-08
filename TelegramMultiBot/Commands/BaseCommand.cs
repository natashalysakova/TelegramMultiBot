using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramMultiBot.Commands
{
    abstract class BaseCommand : ICommand
    {
        public bool CanHandleInlineQuery { get => this.GetType().IsAssignableTo(typeof(IInlineQueryHandler)); }
        public bool CanHandleCallback { get => this.GetType().IsAssignableTo(typeof(ICallbackHandler)); }
        public string Command { get => this.GetType().GetAttributeValue((ServiceKeyAttribute att) => { return att.Command; }); }

        public virtual bool CanHandle(Message message)
        {
            if( message.Entities!=null
                && message.Entities.Any(x=> x.Type == MessageEntityType.BotCommand)
                && (message.Text.StartsWith("/") || message.Text.StartsWith($"@{BotService.BotName} /")))

            {
                var value = message.EntityValues.ElementAt(0);
                if (value.StartsWith("@"))
                {
                    value = message.EntityValues.ElementAt(1);
                }

                if (value.Contains("@"))
                {
                    return value.Equals($"/{Command}@{BotService.BotName}");
                }
                else
                {
                    return value.StartsWith("/" + Command);
                }
            }
            
            return false;
        }

        public virtual bool CanHandle(InlineQuery query)
        {
            return query.Query.StartsWith($"@{BotService.BotName} /{Command}", StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual bool CanHandle(string query)
        {
            return query.Split("|", StringSplitOptions.RemoveEmptyEntries)[0] == Command;
        }

        public abstract Task Handle(Message message);
    }

    public static class AttributeExtensions
    {
        public static TValue GetAttributeValue<TAttribute, TValue>(
            this Type type,
            Func<TAttribute, TValue> valueSelector)
            where TAttribute : Attribute
        {
            var att = type.GetCustomAttributes(
                typeof(TAttribute), true
            ).FirstOrDefault() as TAttribute;
            if (att != null)
            {
                return valueSelector(att);
            }
            return default(TValue);
        }
    }




    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed class ServiceKeyAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        readonly string command;

        // This is a positional argument
        public ServiceKeyAttribute(string positionalString)
        {
            this.command = positionalString;

            // TODO: Implement code here

            //throw new NotImplementedException();
        }

        public string Command
        {
            get { return command; }
        }
    }
}
