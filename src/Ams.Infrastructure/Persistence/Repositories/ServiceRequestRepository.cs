using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ServiceRequestRepository : IServiceRequestRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ServiceRequestRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ServiceRequestDto?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ServiceRequestId, TenantId, AccountId, AgreementId, EngagementId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, ResolvedDate, CreatedDateUtc FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND ServiceRequestId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ServiceRequestDto>(new CommandDefinition(sql, new { TenantId = tenantId, Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ServiceRequestDto>> SearchAsync(Guid tenantId, Guid? accountId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT ServiceRequestId, TenantId, AccountId, AgreementId, EngagementId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, ResolvedDate, CreatedDateUtc FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AccountId IS NULL OR AccountId = @AccountId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Subject LIKE '%' + @SearchTerm + '%' OR RequestNumber LIKE '%' + @SearchTerm + '%'))
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND IsDeleted = 0 AND (@AccountId IS NULL OR AccountId = @AccountId) AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Subject LIKE '%' + @SearchTerm + '%' OR RequestNumber LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, AccountId = accountId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ServiceRequestDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ServiceRequestDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, AgreementId, EngagementId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, IsDeleted)
VALUES (@ServiceRequestId, @TenantId, @AccountId, @AgreementId, @EngagementId, @RequestNumber, @RequestTypeCode, @Subject, @Description, @PriorityCode, @AssignedToUserId, 'Open', SYSUTCDATETIME(), NULL, @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ServiceRequestId = id, request.TenantId, request.AccountId, request.AgreementId, request.EngagementId, request.RequestNumber, request.RequestTypeCode, request.Subject, request.Description, request.PriorityCode, request.AssignedToUserId, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.ServiceRequest
SET AccountId = @AccountId,
    AgreementId = @AgreementId,
    EngagementId = @EngagementId,
    RequestNumber = @RequestNumber,
    RequestTypeCode = @RequestTypeCode,
    Subject = @Subject,
    Description = @Description,
    PriorityCode = @PriorityCode,
    AssignedToUserId = @AssignedToUserId,
    StatusCode = @StatusCode,
    ResolvedDate = @ResolvedDate,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TenantId = @TenantId AND ServiceRequestId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.AccountId, request.AgreementId, request.EngagementId, request.RequestNumber, request.RequestTypeCode, request.Subject, request.Description, request.PriorityCode, request.AssignedToUserId, request.StatusCode, request.ResolvedDate, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE OPS.ServiceRequest
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TenantId = @TenantId AND ServiceRequestId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
