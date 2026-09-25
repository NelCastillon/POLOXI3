using System.Data;
using Dapper;

namespace Legal.Infrastructure.Persistence;

// Registers Dapper type handlers so SQL Server date/time columns (materialized by the
// provider as DateTime / TimeSpan) map cleanly onto DateOnly? / TimeOnly? DTO properties.
// Without these, Dapper throws InvalidCastException: 'Invalid cast from System.DateTime to System.DateOnly'.
internal static class DapperTypeHandlers
{
    private static bool _registered;
    private static readonly object _gate = new();

    public static void EnsureRegistered()
    {
        if (_registered) return;
        lock (_gate)
        {
            if (_registered) return;
            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
            SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
            _registered = true;
        }
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            DateOnly d => d,
            string s => DateOnly.Parse(s),
            _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
        };

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }
    }

    private sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
    {
        public override TimeOnly Parse(object value) => value switch
        {
            TimeSpan ts => TimeOnly.FromTimeSpan(ts),
            TimeOnly t => t,
            DateTime dt => TimeOnly.FromDateTime(dt),
            string s => TimeOnly.Parse(s),
            _ => TimeOnly.FromTimeSpan((TimeSpan)value)
        };

        public override void SetValue(IDbDataParameter parameter, TimeOnly value)
        {
            parameter.DbType = DbType.Time;
            parameter.Value = value.ToTimeSpan();
        }
    }
}
