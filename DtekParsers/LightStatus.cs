namespace DtekParsers;

public class LightStatus
{
    /// <summary>
    /// Timezone ID
    /// </summary>
    public int Id { get; set; }
    public ScheduleStatus Status { get; set; }
    public double PlannedHours { get => GetPlannedValue(); }
    public double RealHours { get => GetRealValue(); }
    public double Total { get => GetRealValue() + GetPlannedValue(); }

    private double GetPlannedValue()
    {
        return Status switch
        {
            ScheduleStatus.maybe => 1f,
            ScheduleStatus.mfirst => 0.5f,
            ScheduleStatus.msecond => 0.5f,
            _ => 0,
        };
    }

    private double GetRealValue()
    {
        return Status switch
        {
            ScheduleStatus.no => 1f,
            ScheduleStatus.first => 0.5f,
            ScheduleStatus.second => 0.5f,
            _ => 0,
        };
    }
}