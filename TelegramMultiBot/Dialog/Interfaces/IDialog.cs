// See https://aka.ms/new-console-template for more information
internal interface IDialog
{
    long ChatId { get; set; }
    bool IsFinished { get; set; }

    void SetNextState();
}