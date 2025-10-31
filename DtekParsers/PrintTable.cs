namespace DtekParsers;

public class PrintTable
{
    public required DateTime Updated { get; init; }
    public required string Header { get; init; }

    public required IEnumerable<string> TimeZones { get; init; }
    public required IEnumerable<PrintRow> Rows { get; init; }

    public bool IsPlanned { get => TotalPlannedHoursWithoutLight > 0; }

    public double TotalRealHoursWithoutLight { get => Rows.Sum(x => x.TotalHoursWithoutLight); }
    public double TotalPlannedHoursWithoutLight { get => Rows.Sum(x => x.TotalPlannedHoursWithoutLight); }

    public double Total { get => Rows.Sum(x => x.Total); }

}
