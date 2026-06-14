namespace Ams.Worker.Automation;

public static class CronScheduleCalculator
{
    public static DateTime? GetNextOccurrenceUtc(string cronExpression, DateTime fromUtc)
    {
        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 5)
        {
            return fromUtc.AddMinutes(15);
        }

        if (TryParseEveryMinutes(parts[0], out var minutes))
        {
            var next = fromUtc.AddMinutes(minutes);
            return new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0, DateTimeKind.Utc);
        }

        if (int.TryParse(parts[0], out var minute) && int.TryParse(parts[1], out var hour))
        {
            var next = new DateTime(fromUtc.Year, fromUtc.Month, fromUtc.Day, hour, minute, 0, DateTimeKind.Utc);
            if (next <= fromUtc)
            {
                next = next.AddDays(1);
            }

            return next;
        }

        return fromUtc.AddMinutes(15);
    }

    private static bool TryParseEveryMinutes(string field, out int minutes)
    {
        minutes = 0;
        if (!field.StartsWith("*/", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(field[2..], out minutes) && minutes > 0;
    }
}
