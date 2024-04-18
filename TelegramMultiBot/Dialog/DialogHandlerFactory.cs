// See https://aka.ms/new-console-template for more information
internal class DialogHandlerFactory(IEnumerable<IDialogHandler> handlers)
{
    public IDialogHandler CreateHandler(IDialog dialog)
    {
        return handlers.Single(x => x.CanHandle(dialog));
    }
}