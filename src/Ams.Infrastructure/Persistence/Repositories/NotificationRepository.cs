using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Communications;
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

    public async Task<Guid> CreateAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Core.Notification
                (NotificationId, TenantId, RecipientUserId, TemplateId, ChannelCode, Subject, Body, EntityName, EntityId, StatusCode, IsRead, ReadDateUtc, SentDateUtc, ErrorMessage, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (@NotificationId, @TenantId, @RecipientUserId, @TemplateId, @ChannelCode, @Subject, @Body, @EntityName, @EntityId, @StatusCode, 0, NULL, @SentDateUtc, @ErrorMessage, SYSUTCDATETIME(), @CreatedByUserId, 0);
            """;
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = id, request.TenantId, request.RecipientUserId, request.TemplateId, request.ChannelCode, request.Subject, request.Body, request.EntityName, request.EntityId, request.StatusCode, request.SentDateUtc, request.ErrorMessage, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task SetReadAsync(Guid notificationId, bool isRead, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Notification SET IsRead = @IsRead, ReadDateUtc = CASE WHEN @IsRead = 1 THEN SYSUTCDATETIME() ELSE NULL END WHERE NotificationId = @NotificationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = notificationId, IsRead = isRead }, cancellationToken: cancellationToken));
    }

    public async Task SetStatusAsync(Guid notificationId, string statusCode, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Notification SET StatusCode = @StatusCode, IsRead = 1, ReadDateUtc = COALESCE(ReadDateUtc, SYSUTCDATETIME()) WHERE NotificationId = @NotificationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = notificationId, StatusCode = statusCode }, cancellationToken: cancellationToken));
    }

    public async Task MarkAllReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Notification SET IsRead = 1, ReadDateUtc = COALESCE(ReadDateUtc, SYSUTCDATETIME()) WHERE TenantId = @TenantId AND RecipientUserId = @RecipientUserId AND IsRead = 0 AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, RecipientUserId = recipientUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Notification SET IsDeleted = 1 WHERE NotificationId = @NotificationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NotificationId = notificationId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Notification SET IsDeleted = 1 WHERE TenantId = @TenantId AND RecipientUserId = @RecipientUserId AND IsRead = 1 AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, RecipientUserId = recipientUserId }, cancellationToken: cancellationToken));
    }
}
