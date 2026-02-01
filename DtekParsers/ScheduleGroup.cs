namespace DtekParsers;

public class ScheduleGroup
{
    public required string Id { get; set; }
    public required string GroupName { get; set; }
    public string? DataSnapshot { get; set; }
    public double GroupNumber { get; internal set; }
}
