using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public NotificationRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<NotificationDto?> GetByIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT NotificationId, TenantId, RecipientUserId, TemplateId, ChannelCode,
                   Subject, Body, EntityName, EntityId, StatusCode,
                   IsRead, ReadDateUtc, SentDateUtc, ErrorMessage, CreatedDateUtc
            FROM Core.Notification
            WHERE NotificationId = @NotificationId AND IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<NotificationDto>(
            new CommandDefinition(sql, new { NotificationId = notificationId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<NotificationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT NotificationId, TenantId, RecipientUserId, TemplateId, ChannelCode,
                       Subject, Body, EntityName, EntityId, StatusCode,
                       IsRead, ReadDateUtc, SentDateUtc, ErrorMessage, CreatedDateUtc
                FROM Core.Notification
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@SearchTerm IS NULL OR Subject LIKE '%' + @SearchTerm + '%'
                                          OR Body     LIKE '%' + @SearchTerm + '%'
                                          OR StatusCode = @SearchTerm)
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.Notification
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR Subject LIKE '%' + @SearchTerm + '%'
                                      OR Body     LIKE '%' + @SearchTerm + '%'
                                      OR StatusCode = @SearchTerm);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<NotificationDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<NotificationDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<NotificationTemplateDto>> SearchTemplatesAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT TemplateId, TenantId, TemplateCode, TemplateName, ChannelCode,
                       SubjectTemplate, BodyTemplate, IsSystemTemplate, IsActive, CreatedDateUtc
                FROM Core.NotificationTemplate
                WHERE IsDeleted = 0
                  AND (@SearchTerm IS NULL OR TemplateName   LIKE '%' + @SearchTerm + '%'
                                          OR TemplateCode   LIKE '%' + @SearchTerm + '%'
                                          OR ChannelCode    = @SearchTerm)
            )
            SELECT * FROM Cte ORDER BY IsSystemTemplate DESC, TemplateName ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.NotificationTemplate
            WHERE IsDeleted = 0
              AND (@SearchTerm IS NULL OR TemplateName LIKE '%' + @SearchTerm + '%'
                                      OR TemplateCode LIKE '%' + @SearchTerm + '%'
                                      OR ChannelCode  = @SearchTerm);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<NotificationTemplateDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<NotificationTemplateDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
