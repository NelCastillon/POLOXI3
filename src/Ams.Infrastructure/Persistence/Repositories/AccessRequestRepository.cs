using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Governance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccessRequestRepository : IAccessRequestRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public AccessRequestRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<AccessRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ar.AccessRequestId, ar.TenantId,
                   ar.RequestedByUserId, rb.FullName AS RequestedByFullName,
                   ar.RequestedForUserId, rf.FullName AS RequestedForFullName, rf.Email AS RequestedForEmail,
                   ar.RequestTypeCode, ar.RoleId, ro.RoleName,
                   ar.PermissionId, p.PermissionName,
                   ar.ScopeCode, ar.StartDateUtc, ar.EndDateUtc,
                   ar.BusinessJustification, ar.TicketReference, ar.UrgencyCode,
                   ar.AttachmentFileName, ar.StatusCode, ar.ApproverComment,
                   ar.CreatedDateUtc, ar.ModifiedDateUtc
            FROM IAM.AccessRequest ar
            JOIN IAM.[User] rb  ON rb.UserId  = ar.RequestedByUserId
            JOIN IAM.[User] rf  ON rf.UserId  = ar.RequestedForUserId
            LEFT JOIN IAM.Role ro       ON ro.RoleId       = ar.RoleId
            LEFT JOIN IAM.Permission p  ON p.PermissionId  = ar.PermissionId
            WHERE ar.AccessRequestId = @Id AND ar.IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccessRequestDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccessRequestDto>> SearchAsync(
        Guid tenantId, string? searchTerm, string? requestTypeCode, string? statusCode,
        Guid? requestedForUserId, Guid? requestedByUserId, int pageNumber = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT ar.AccessRequestId, ar.TenantId,
                       ar.RequestedByUserId, rb.FullName AS RequestedByFullName,
                       ar.RequestedForUserId, rf.FullName AS RequestedForFullName, rf.Email AS RequestedForEmail,
                       ar.RequestTypeCode, ar.RoleId, ro.RoleName,
                       ar.PermissionId, p.PermissionName,
                       ar.ScopeCode, ar.StartDateUtc, ar.EndDateUtc,
                       ar.BusinessJustification, ar.TicketReference, ar.UrgencyCode,
                       ar.AttachmentFileName, ar.StatusCode, ar.ApproverComment,
                       ar.CreatedDateUtc, ar.ModifiedDateUtc
                FROM IAM.AccessRequest ar
                JOIN IAM.[User] rb  ON rb.UserId  = ar.RequestedByUserId
                JOIN IAM.[User] rf  ON rf.UserId  = ar.RequestedForUserId
                LEFT JOIN IAM.Role ro       ON ro.RoleId       = ar.RoleId
                LEFT JOIN IAM.Permission p  ON p.PermissionId  = ar.PermissionId
                WHERE ar.TenantId = @TenantId AND ar.IsDeleted = 0
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR rf.FullName LIKE '%' + @SearchTerm + '%'
                       OR rb.FullName LIKE '%' + @SearchTerm + '%'
                       OR ar.TicketReference LIKE '%' + @SearchTerm + '%'
                       OR ar.RequestTypeCode LIKE '%' + @SearchTerm + '%')
                  AND (@RequestTypeCode IS NULL OR @RequestTypeCode = '' OR ar.RequestTypeCode = @RequestTypeCode)
                  AND (@StatusCode IS NULL OR @StatusCode = '' OR ar.StatusCode = @StatusCode)
                  AND (@RequestedForUserId IS NULL OR ar.RequestedForUserId = @RequestedForUserId)
                  AND (@RequestedByUserId IS NULL OR ar.RequestedByUserId = @RequestedByUserId)
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM IAM.AccessRequest ar
            JOIN IAM.[User] rb ON rb.UserId = ar.RequestedByUserId
            JOIN IAM.[User] rf ON rf.UserId = ar.RequestedForUserId
            WHERE ar.TenantId = @TenantId AND ar.IsDeleted = 0
              AND (@SearchTerm IS NULL OR @SearchTerm = ''
                   OR rf.FullName LIKE '%' + @SearchTerm + '%'
                   OR rb.FullName LIKE '%' + @SearchTerm + '%'
                   OR ar.TicketReference LIKE '%' + @SearchTerm + '%'
                   OR ar.RequestTypeCode LIKE '%' + @SearchTerm + '%')
              AND (@RequestTypeCode IS NULL OR @RequestTypeCode = '' OR ar.RequestTypeCode = @RequestTypeCode)
              AND (@StatusCode IS NULL OR @StatusCode = '' OR ar.StatusCode = @StatusCode)
              AND (@RequestedForUserId IS NULL OR ar.RequestedForUserId = @RequestedForUserId)
              AND (@RequestedByUserId IS NULL OR ar.RequestedByUserId = @RequestedByUserId);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId           = tenantId,
            SearchTerm         = searchTerm,
            RequestTypeCode    = requestTypeCode,
            StatusCode         = statusCode,
            RequestedForUserId = requestedForUserId,
            RequestedByUserId  = requestedByUserId,
            Offset             = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize           = pageSize,
        }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AccessRequestDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AccessRequestDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> SubmitAsync(SubmitAccessRequestRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = """
            INSERT INTO IAM.AccessRequest
                (AccessRequestId, TenantId, RequestedByUserId, RequestedForUserId,
                 RequestTypeCode, RoleId, PermissionId, ScopeCode,
                 StartDateUtc, EndDateUtc, BusinessJustification,
                 TicketReference, UrgencyCode, AttachmentFileName,
                 StatusCode, CreatedDateUtc, IsDeleted)
            VALUES
                (@AccessRequestId, @TenantId, @RequestedByUserId, @RequestedForUserId,
                 @RequestTypeCode, @RoleId, @PermissionId, @ScopeCode,
                 @StartDateUtc, @EndDateUtc, @BusinessJustification,
                 @TicketReference, @UrgencyCode, @AttachmentFileName,
                 'Pending', GETUTCDATE(), 0);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AccessRequestId      = id,
            request.TenantId,
            request.RequestedByUserId,
            request.RequestedForUserId,
            request.RequestTypeCode,
            request.RoleId,
            request.PermissionId,
            request.ScopeCode,
            request.StartDateUtc,
            request.EndDateUtc,
            request.BusinessJustification,
            request.TicketReference,
            request.UrgencyCode,
            request.AttachmentFileName,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ProcessAsync(Guid id, ProcessAccessRequestRequest request, CancellationToken cancellationToken = default)
    {
        var newStatus = request.ActionCode switch
        {
            "Approve" => "Approved",
            "Reject"  => "Rejected",
            "Return"  => "Returned",
            _         => (string?)null,
        };
        const string sql = """
            UPDATE IAM.AccessRequest
            SET    StatusCode      = CASE WHEN @NewStatus IS NOT NULL THEN @NewStatus ELSE StatusCode END,
                   ApproverComment = CASE WHEN @Comment   IS NOT NULL THEN @Comment   ELSE ApproverComment END,
                   ModifiedDateUtc = GETUTCDATE()
            WHERE  AccessRequestId = @Id AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id        = id,
            NewStatus = newStatus,
            Comment   = request.Comment,
        }, cancellationToken: cancellationToken));
    }
}
