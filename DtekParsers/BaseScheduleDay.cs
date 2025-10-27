using System.Globalization;
namespace DtekParsers;

public abstract class BaseScheduleDay
{
    public Dictionary<ScheduleTimeZone, ScheduleStatus> Items { get; set; } = new();
    abstract public string DateHeader { get; }
    public required string Group { get; set; }
}

public class ScheduleDay : BaseScheduleDay
{
    public int DayNumber { get; set; }
    public override string DateHeader { get => dayName[DayNumber]; }

    private static readonly Dictionary<int, string> dayName = new Dictionary<int, string>()
    {
        {1, "Понеділок"},
        {2, "Вівторок"},
        {3, "Середа"},
        {4, "Четвер"},
        {5, "П'ятниця"},
        {6, "Субота"},
        {7, "Неділя"},
    };
}

public class RealScheduleDay : BaseScheduleDay
{
    public DateTime Date { get; set; }
    public override string DateHeader { get => Date.ToString("dd.MM.yy"); }
}


