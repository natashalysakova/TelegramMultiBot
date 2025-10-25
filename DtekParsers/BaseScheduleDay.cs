using System.Globalization;
using System.Security.Cryptography;

namespace DtekParsers;

public abstract class BaseScheduleDay
{
    public Dictionary<ScheduleTimeZone, ScheduleStatus> Items { get; set; } = new();
    abstract public string DateHeader { get; }
}

public class ScheduleDay : BaseScheduleDay
{
    public int DayNumber { get; set; }
    public override string DateHeader { get => CultureInfo.CurrentCulture.DateTimeFormat.DayNames[DayNumber]; }
}

public class RealScheduleDay : BaseScheduleDay
{
    public DateTime Date { get; set; }
    public override string DateHeader { get => Date.ToString("dd.MM.yy"); }
}


