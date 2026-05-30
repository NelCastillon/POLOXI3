using Ams.Application.Common.Dtos;

namespace Ams.Application.Features.Agency;

public sealed class UpdateAgencyBusinessHoursRequest
{
    public string TimeZoneId { get; set; } = "Eastern Standard Time";
    public bool EmergencyClosing { get; set; }
    public string? EmergencyMessage { get; set; }
    public List<AgencyBusinessDayDto> WeeklySchedule { get; set; } = [];
    public List<AgencyHolidayClosureDto> HolidayClosures { get; set; } = [];
}
