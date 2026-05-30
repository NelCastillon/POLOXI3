using System.Text.Json;
using Ams.Application.Common.Dtos;

namespace Ams.Infrastructure.Persistence.Repositories;

internal static class BusinessHoursJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string DefaultWeeklyScheduleJson => SerializeWeeklySchedule(DefaultWeeklySchedule());
    public static string DefaultHolidayClosuresJson => SerializeHolidayClosures(DefaultHolidayClosures());

    public static AgencyBusinessHoursDto ToDto(AgencyBusinessHoursRecord record)
        => new()
        {
            BusinessHoursId = record.BusinessHoursId,
            TenantId = record.TenantId,
            TimeZoneId = string.IsNullOrWhiteSpace(record.TimeZoneId) ? "Eastern Standard Time" : record.TimeZoneId,
            EmergencyClosing = record.EmergencyClosing,
            EmergencyMessage = record.EmergencyMessage,
            WeeklySchedule = DeserializeWeeklySchedule(record.WeeklyScheduleJson),
            HolidayClosures = DeserializeHolidayClosures(record.HolidayClosuresJson),
            CreatedDateUtc = record.CreatedDateUtc,
            ModifiedDateUtc = record.ModifiedDateUtc,
        };

    public static string SerializeWeeklySchedule(IEnumerable<AgencyBusinessDayDto>? schedule)
        => JsonSerializer.Serialize(NormalizeSchedule(schedule), JsonOptions);

    public static string SerializeHolidayClosures(IEnumerable<AgencyHolidayClosureDto>? holidays)
        => JsonSerializer.Serialize((holidays ?? DefaultHolidayClosures()).Where(h => h.Date != default && !string.IsNullOrWhiteSpace(h.Name)).OrderBy(h => h.Date).ToList(), JsonOptions);

    private static List<AgencyBusinessDayDto> DeserializeWeeklySchedule(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            var schedule = JsonSerializer.Deserialize<List<AgencyBusinessDayDto>>(json, JsonOptions);
            if (schedule?.Count > 0)
            {
                return NormalizeSchedule(schedule);
            }
        }

        return DefaultWeeklySchedule();
    }

    private static List<AgencyHolidayClosureDto> DeserializeHolidayClosures(string? json)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            var holidays = JsonSerializer.Deserialize<List<AgencyHolidayClosureDto>>(json, JsonOptions);
            if (holidays is not null)
            {
                return holidays.Where(h => h.Date != default && !string.IsNullOrWhiteSpace(h.Name)).OrderBy(h => h.Date).ToList();
            }
        }

        return DefaultHolidayClosures();
    }

    private static List<AgencyBusinessDayDto> NormalizeSchedule(IEnumerable<AgencyBusinessDayDto>? schedule)
    {
        var byDay = (schedule ?? []).GroupBy(d => d.DayOfWeek).ToDictionary(g => g.Key, g => g.First());
        var result = new List<AgencyBusinessDayDto>(7);

        for (var day = 0; day < 7; day++)
        {
            var source = byDay.GetValueOrDefault(day);
            var isWeekday = day is >= 1 and <= 5;
            result.Add(new AgencyBusinessDayDto
            {
                DayOfWeek = day,
                DayName = Enum.GetName((DayOfWeek)day) ?? day.ToString(),
                IsOpen = source?.IsOpen ?? isWeekday,
                OpenTime = source?.OpenTime ?? new TimeOnly(8, 0),
                CloseTime = source?.CloseTime ?? new TimeOnly(17, 0),
                HasLunchBreak = source?.HasLunchBreak ?? false,
                LunchStart = source?.LunchStart,
                LunchEnd = source?.LunchEnd,
            });
        }

        return result;
    }

    private static List<AgencyBusinessDayDto> DefaultWeeklySchedule()
        => NormalizeSchedule(null);

    private static List<AgencyHolidayClosureDto> DefaultHolidayClosures()
    {
        var year = DateTime.UtcNow.Year;
        return
        [
            new() { Date = new DateOnly(year, 1, 1), Name = "New Year's Day" },
            new() { Date = new DateOnly(year, 7, 4), Name = "Independence Day" },
            new() { Date = new DateOnly(year, 12, 25), Name = "Christmas Day" },
        ];
    }
}

internal sealed class AgencyBusinessHoursRecord
{
    public Guid BusinessHoursId { get; set; }
    public Guid TenantId { get; set; }
    public string TimeZoneId { get; set; } = "Eastern Standard Time";
    public string? WeeklyScheduleJson { get; set; }
    public string? HolidayClosuresJson { get; set; }
    public bool EmergencyClosing { get; set; }
    public string? EmergencyMessage { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
