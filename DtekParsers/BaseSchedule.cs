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
