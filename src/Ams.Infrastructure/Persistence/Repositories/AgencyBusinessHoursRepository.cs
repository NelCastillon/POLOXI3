using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Agency;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AgencyBusinessHoursRepository : IAgencyBusinessHoursRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AgencyBusinessHoursRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<AgencyBusinessHoursDto> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM Core.AgencyBusinessHours WHERE TenantId = @TenantId AND IsDeleted = 0)
            BEGIN
                INSERT INTO Core.AgencyBusinessHours
                    (TenantId, TimeZoneId, WeeklyScheduleJson, HolidayClosuresJson, EmergencyClosing, EmergencyMessage, CreatedDateUtc, IsDeleted)
                VALUES
                    (@TenantId, N'Eastern Standard Time', @DefaultWeeklyScheduleJson, @DefaultHolidayClosuresJson, 0, NULL, SYSUTCDATETIME(), 0);
            END;

            SELECT BusinessHoursId, TenantId, TimeZoneId, WeeklyScheduleJson, HolidayClosuresJson,
                   EmergencyClosing, EmergencyMessage, CreatedDateUtc, ModifiedDateUtc
            FROM Core.AgencyBusinessHours
            WHERE TenantId = @TenantId AND IsDeleted = 0;
            """;

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await cn.QuerySingleAsync<AgencyBusinessHoursRecord>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            DefaultWeeklyScheduleJson = BusinessHoursJson.DefaultWeeklyScheduleJson,
            DefaultHolidayClosuresJson = BusinessHoursJson.DefaultHolidayClosuresJson,
        }, cancellationToken: cancellationToken));

        return BusinessHoursJson.ToDto(row);
    }

    public async Task UpdateAsync(Guid tenantId, UpdateAgencyBusinessHoursRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM Core.AgencyBusinessHours WHERE TenantId = @TenantId AND IsDeleted = 0)
                UPDATE Core.AgencyBusinessHours SET
                    TimeZoneId           = @TimeZoneId,
                    WeeklyScheduleJson   = @WeeklyScheduleJson,
                    HolidayClosuresJson  = @HolidayClosuresJson,
                    EmergencyClosing     = @EmergencyClosing,
                    EmergencyMessage     = @EmergencyMessage,
                    ModifiedDateUtc      = SYSUTCDATETIME()
                WHERE TenantId = @TenantId AND IsDeleted = 0;
            ELSE
                INSERT INTO Core.AgencyBusinessHours
                    (TenantId, TimeZoneId, WeeklyScheduleJson, HolidayClosuresJson, EmergencyClosing, EmergencyMessage, CreatedDateUtc, IsDeleted)
                VALUES
                    (@TenantId, @TimeZoneId, @WeeklyScheduleJson, @HolidayClosuresJson, @EmergencyClosing, @EmergencyMessage, SYSUTCDATETIME(), 0);
            """;

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "Eastern Standard Time" : request.TimeZoneId.Trim(),
            WeeklyScheduleJson = BusinessHoursJson.SerializeWeeklySchedule(request.WeeklySchedule),
            HolidayClosuresJson = BusinessHoursJson.SerializeHolidayClosures(request.HolidayClosures),
            request.EmergencyClosing,
            EmergencyMessage = string.IsNullOrWhiteSpace(request.EmergencyMessage) ? null : request.EmergencyMessage.Trim(),
        }, cancellationToken: cancellationToken));
    }
}
