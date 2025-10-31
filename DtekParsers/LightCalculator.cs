namespace DtekParsers;

public static class LightCalculator
{
    public static double CalculateTotal(IEnumerable<LightStatus> lightStatuses, CalculationMode calculationMode = CalculationMode.Real)
    {
        return lightStatuses.Sum(status =>
        {
            return calculationMode switch
            {
                CalculationMode.Planned => status.PlannedHours,
                CalculationMode.Real => status.RealHours,
                CalculationMode.Total => status.Total
            };
        });
    }

    

    public enum CalculationMode
    {
        Planned,
        Real,
        Total
    }
}
