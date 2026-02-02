
using System.Diagnostics;

namespace DtekParsers;

public class Schedule
{
    public List<ScheduleTimeZone> TimeZones { get; set; } = new();
    public List<ScheduleGroup> Groups { get; set; } = new();
    public string? Location { get; set; }
    public List<RealSchedule> RealSchedule { get; set; } = new();
    public List<PlannedSchedule> PlannedSchedule { get; set; } = new();
    public DateTime Updated { get; set; }

    public string AttentionNote { get; set; } = string.Empty;
    public Dictionary<string, List<string>> Streets { get; internal set; } = new();
}
