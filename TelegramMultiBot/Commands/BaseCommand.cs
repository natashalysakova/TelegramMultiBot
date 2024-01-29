using Telegram.Bot.Types;

namespace TelegramMultiBot.Commands
{

    abstract class BaseCommand : ICommand
    {
        public bool CanHandleInlineQuery { get => this.GetType().IsAssignableTo(typeof(IInlineQueryHandler)); }
        public bool CanHandleCallback { get => this.GetType().IsAssignableTo(typeof(ICallbackHandler)); }
        public string Command { get => this.GetType().GetAttributeValue((CommandAttribute att) => { return att.Command; }); }

        public virtual bool CanHandle(Message message)
        {
            return message.Text.StartsWith("/" + Command, StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual bool CanHandle(InlineQuery query)
        {
            return query.Query.StartsWith("/" + Command, StringComparison.InvariantCultureIgnoreCase);
        }

        public virtual bool CanHandle(CallbackData callbackData)
        {
            return callbackData.comand == this.GetType().Name;
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
    sealed class CommandAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        readonly string command;

        // This is a positional argument
        public CommandAttribute(string positionalString)
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
