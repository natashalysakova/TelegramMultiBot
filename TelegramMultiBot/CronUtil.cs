// See https://aka.ms/new-console-template for more information
public static class CronUtil
{
    public static DateTime? ParseNext(string cron)
    {
        var exp = Cronos.CronExpression.Parse(cron);
        return exp.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local).Value.DateTime;
    }
}
