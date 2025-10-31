namespace DtekParsers;

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
