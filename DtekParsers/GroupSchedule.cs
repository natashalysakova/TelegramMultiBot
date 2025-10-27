namespace DtekParsers;

public class GroupSchedule
{
    public string Id { get; set; }
    public string GroupName { get; set; }
    public string Location { get; set; }
    public List<RealScheduleDay> RealSchedule { get; set; } = new();
    public List<ScheduleDay> PlannedSchedule { get; set; } = new();

    public DateTime Updated { get; set; }
}
