namespace DtekParsers;

/// <inheritdoc />
public class RealSchedule : BaseSchedule
{
    /// <summary>
    /// Parsed date of schedule day
    /// </summary>
    public DateTime Date { get; set; }
    /// <summary>
    /// Raw timestamp of schedule day
    /// </summary>
    public long DateTimeStamp { get; set; }

    public override string DateHeader { get => Date.ToString("dd.MM.yy"); }
}
