using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;

    public AuditLogService(IAuditLogRepository repository) => _repository = repository;

    public Task<PagedResult<AuditLogDto>> SearchAsync(string? searchTerm, string? eventTypeCode, string? actor = null, string? entityName = null, string? tenantId = null, DateTime? fromDate = null, DateTime? toDate = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, eventTypeCode, actor, entityName, tenantId, fromDate, toDate, pageNumber, pageSize, cancellationToken);

    public Task<AuditLogDto?> GetByIdAsync(Guid auditLogId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(auditLogId, cancellationToken);
}
