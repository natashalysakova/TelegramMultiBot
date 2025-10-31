namespace DtekParsers;

public class PrintRow
{
    private IEnumerable<LightStatus> _statuses = new List<LightStatus>();

    public required string Header { get; init; }
    public required IEnumerable<LightStatus> Statuses { get => _statuses.OrderBy(x => x.Id); init => _statuses = value; }

    public double TotalHoursWithoutLight { get => LightCalculator.CalculateTotal(Statuses); }
    public double TotalPlannedHoursWithoutLight { get => LightCalculator.CalculateTotal(Statuses, LightCalculator.CalculationMode.Planned); }
    public string? DateHeader { get; internal set; }

    public double Total { get => TotalHoursWithoutLight + TotalPlannedHoursWithoutLight; }
}
