using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CalendarEventRepository : ICalendarEventRepository
{
    private const string SelectColumns = @"EventId, TenantId, Title, Notes, EventTypeCode, StatusCode, StartDateTimeUtc, EndDateTimeUtc, AllDay, TimeZoneId, OrganizerUserId, AssignedToUserId, RelatedEntityType, RelatedEntityId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted";

    private readonly ISqlConnectionFactory _connectionFactory;

    public CalendarEventRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<CalendarEventDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM OPS.CalendarEvent WHERE EventId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<CalendarEventDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<CalendarEventDto>> SearchAsync(Guid tenantId, DateTime? startUtc = null, DateTime? endUtc = null, Guid? assignedToUserId = null, string? eventTypeCode = null, string? statusCode = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var sql = $@"
;WITH Cte AS (
    SELECT {SelectColumns}
    FROM OPS.CalendarEvent
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@StartUtc IS NULL OR COALESCE(EndDateTimeUtc, StartDateTimeUtc) >= @StartUtc)
      AND (@EndUtc IS NULL OR StartDateTimeUtc <= @EndUtc)
      AND (@AssignedToUserId IS NULL OR AssignedToUserId = @AssignedToUserId OR OrganizerUserId = @AssignedToUserId)
      AND (@EventTypeCode IS NULL OR @EventTypeCode = '' OR EventTypeCode = @EventTypeCode)
      AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Title LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%' OR EventTypeCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY StartDateTimeUtc ASC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1)
FROM OPS.CalendarEvent
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@StartUtc IS NULL OR COALESCE(EndDateTimeUtc, StartDateTimeUtc) >= @StartUtc)
  AND (@EndUtc IS NULL OR StartDateTimeUtc <= @EndUtc)
  AND (@AssignedToUserId IS NULL OR AssignedToUserId = @AssignedToUserId OR OrganizerUserId = @AssignedToUserId)
  AND (@EventTypeCode IS NULL OR @EventTypeCode = '' OR EventTypeCode = @EventTypeCode)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Title LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%' OR EventTypeCode LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            AssignedToUserId = assignedToUserId,
            EventTypeCode = eventTypeCode,
            StatusCode = statusCode,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<CalendarEventDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CalendarEventDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.CalendarEvent
    (EventId, TenantId, Title, Notes, EventTypeCode, StatusCode, StartDateTimeUtc, EndDateTimeUtc, AllDay, TimeZoneId, OrganizerUserId, AssignedToUserId, RelatedEntityType, RelatedEntityId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
    (@EventId, @TenantId, @Title, @Notes, @EventTypeCode, @StatusCode, @StartDateTimeUtc, @EndDateTimeUtc, @AllDay, @TimeZoneId, @OrganizerUserId, @AssignedToUserId, @RelatedEntityType, @RelatedEntityId, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { EventId = id, request.TenantId, request.Title, request.Notes, request.EventTypeCode, request.StatusCode, request.StartDateTimeUtc, request.EndDateTimeUtc, request.AllDay, request.TimeZoneId, request.OrganizerUserId, request.AssignedToUserId, request.RelatedEntityType, request.RelatedEntityId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.CalendarEvent
SET Title = @Title,
    Notes = @Notes,
    EventTypeCode = @EventTypeCode,
    StatusCode = @StatusCode,
    StartDateTimeUtc = @StartDateTimeUtc,
    EndDateTimeUtc = @EndDateTimeUtc,
    AllDay = @AllDay,
    TimeZoneId = @TimeZoneId,
    OrganizerUserId = @OrganizerUserId,
    AssignedToUserId = @AssignedToUserId,
    RelatedEntityType = @RelatedEntityType,
    RelatedEntityId = @RelatedEntityId,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EventId = @EventId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { EventId = id, request.Title, request.Notes, request.EventTypeCode, request.StatusCode, request.StartDateTimeUtc, request.EndDateTimeUtc, request.AllDay, request.TimeZoneId, request.OrganizerUserId, request.AssignedToUserId, request.RelatedEntityType, request.RelatedEntityId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE OPS.CalendarEvent SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE EventId = @EventId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { EventId = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
