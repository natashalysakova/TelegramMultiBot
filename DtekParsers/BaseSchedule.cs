namespace DtekParsers;

public abstract class BaseSchedule
{
    /// <summary>
    /// Gets or sets a collection of light statuses with group name as a Key
    /// </summary>
    public Dictionary<string, IEnumerable<LightStatus>> Statuses { get; set; } = new();

    /// <summary>
    /// Formatted header for table image
    /// </summary>
    abstract public string DateHeader { get; }

    /// <summary>
    /// Gets or sets the date and time when the schedule was last updated.
    /// </summary>
    public DateTime Updated { get; set; }

}

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

public class PlannedSchedule : BaseSchedule
{
    /// <summary>
    /// Day of the week number (0 - Monday, 6 - Sunday)
    /// </summary>
    public int DayNumber { get; set; }
    public override string DateHeader { get => dayName[DayNumber]; }

    private static readonly Dictionary<int, string> dayName = new Dictionary<int, string>()
    {
        {0, "Понеділок"},
        {1, "Вівторок"},
        {2, "Середа"},
        {3, "Четвер"},
        {4, "П'ятниця"},
        {5, "Субота"},
        {6, "Неділя"},
    };
}
