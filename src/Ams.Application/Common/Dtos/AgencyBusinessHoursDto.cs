namespace Ams.Application.Common.Dtos;

public sealed class AgencyBusinessHoursDto
{
    public Guid BusinessHoursId { get; set; }
    public Guid TenantId { get; set; }
    public string TimeZoneId { get; set; } = "Eastern Standard Time";
    public bool EmergencyClosing { get; set; }
    public string? EmergencyMessage { get; set; }
    public List<AgencyBusinessDayDto> WeeklySchedule { get; set; } = [];
    public List<AgencyHolidayClosureDto> HolidayClosures { get; set; } = [];
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class AgencyBusinessDayDto
{
    public string DayName { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public bool IsOpen { get; set; }
    public TimeOnly OpenTime { get; set; } = new(8, 0);
    public TimeOnly CloseTime { get; set; } = new(17, 0);
    public bool HasLunchBreak { get; set; }
    public TimeOnly? LunchStart { get; set; }
    public TimeOnly? LunchEnd { get; set; }
}

public sealed class AgencyHolidayClosureDto
{
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}
