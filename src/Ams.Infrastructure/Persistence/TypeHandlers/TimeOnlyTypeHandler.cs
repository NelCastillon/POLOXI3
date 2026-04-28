using Dapper;
using System.Data;

namespace Ams.Infrastructure.Persistence.TypeHandlers;

/// <summary>
/// Custom Dapper type handler to convert DateTime/Time from SQL to TimeOnly in .NET
/// </summary>
public sealed class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.Value = value.ToTimeSpan();
    }

    public override TimeOnly Parse(object? value)
    {
        return value switch
        {
            TimeOnly timeOnly => timeOnly,
            TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
            DateTime dateTime => TimeOnly.FromDateTime(dateTime),
            string timeString when TimeOnly.TryParse(timeString, out var time) => time,
            null => throw new ArgumentNullException(nameof(value), "Cannot convert NULL value to TimeOnly"),
            _ => throw new InvalidCastException($"Cannot convert value of type {value.GetType()} to TimeOnly")
        };
    }
}
