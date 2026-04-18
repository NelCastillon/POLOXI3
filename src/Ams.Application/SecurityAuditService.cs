using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class SecurityAuditService : ISecurityAuditService
{
    private readonly ISecurityAuditRepository _repository;

    public SecurityAuditService(ISecurityAuditRepository repository)
        => _repository = repository;

    public Task<PagedResult<FieldChangeLogDto>> SearchFieldChangesAsync(Guid tenantId, string? entityName, Guid? entityId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchFieldChangesAsync(tenantId, entityName, entityId, pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<SecurityEventLogDto>> SearchSecurityEventsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchSecurityEventsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
}
