// See https://aka.ms/new-console-template for more information
public interface IDialog
{
    long ChatId { get; set; }
    bool IsFinished { get; set; }
    void SetNextState();
}
