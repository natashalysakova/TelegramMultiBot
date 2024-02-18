using System.ComponentModel.DataAnnotations.Schema;

public class Reminder
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid Id { get; }
    public string Name { get; }
    public string Message { get; }
    public string Config { get; }
    public long ChatId { get; }

}

