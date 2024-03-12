// See https://aka.ms/new-console-template for more information

internal class AddJobDialog : BaseDialog<AddDialogState>
{
    public string? Name { get; set; }
    public string? CRON { get; set; }
    public string? Text { get; set; }

    protected override IEnumerable<StateTransition> GetStates()
    {
        return new StateTransition[]
        {
            new (AddDialogState.Start, AddDialogState.CheckName),
            new (AddDialogState.CheckName, AddDialogState.CheckCron),
            new (AddDialogState.CheckCron, AddDialogState.CheckMessage)
        };
    }
}