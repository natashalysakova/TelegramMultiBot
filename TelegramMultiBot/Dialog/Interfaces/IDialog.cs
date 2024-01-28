// See https://aka.ms/new-console-template for more information
interface IDialog
{
    long ChatId { get; set; }
    bool IsFinished { get; set; }
    void SetNextState();
}
