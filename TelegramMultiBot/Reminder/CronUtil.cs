// See https://aka.ms/new-console-template for more information
namespace TelegramMultiBot
{
    public static class CronUtil
    {
        public static DateTime ParseNext(string cron)
        {
            if (Cronos.CronExpression.TryParse(cron, out var exp))
            {
                var next = exp.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
                if (next.HasValue)
                    return next.Value.DateTime;
            }

            throw new InvalidDataException("cannot parse CRON");
        }
    }
}