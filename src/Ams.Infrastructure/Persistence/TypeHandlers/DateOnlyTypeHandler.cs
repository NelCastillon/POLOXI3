using Dapper;
using System.Data;

namespace Ams.Infrastructure.Persistence.TypeHandlers;

/// <summary>
/// Custom Dapper type handler to convert DateTime from SQL to DateOnly in .NET
/// </summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object? value)
    {
        return value switch
        {
            DateOnly dateOnly => dateOnly,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            string dateString when DateOnly.TryParse(dateString, out var date) => date,
            null => throw new ArgumentNullException(nameof(value), "Cannot convert NULL value to DateOnly"),
            _ => throw new InvalidCastException($"Cannot convert value of type {value.GetType()} to DateOnly")
        };
    }
}
