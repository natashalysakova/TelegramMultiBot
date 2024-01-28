// See https://aka.ms/new-console-template for more information
class DialogHandlerFactory
{
    private readonly IEnumerable<IDialogHandler> _handlers;

    public DialogHandlerFactory(IEnumerable<IDialogHandler> handlers)
    {
        _handlers = handlers;
    }

    public IDialogHandler CreateHandler(IDialog dialog)
    {
        return _handlers.Single(x => x.CanHandle(dialog));
    }
}
