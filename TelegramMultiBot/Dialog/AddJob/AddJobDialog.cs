// See https://aka.ms/new-console-template for more information

internal class AddJobDialog : BaseDialog<AddDialogState>
{
    public AddJobDialog()
    {
        Name = CRON = Text = string.Empty;
    }

    public string Name { get; set; }
    public string CRON { get; set; }
    public string? Text { get; set; }
    public bool Attachment { get; internal set; }

    protected override IEnumerable<StateTransition> GetStates()
    {
        return
        [
            new (AddDialogState.Start, AddDialogState.CheckName),
            new (AddDialogState.CheckName, AddDialogState.CheckCron),
            new (AddDialogState.CheckCron, AddDialogState.CheckMessage)
        ];
    }
}