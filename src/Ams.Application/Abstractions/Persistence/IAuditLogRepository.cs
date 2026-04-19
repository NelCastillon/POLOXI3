using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IAuditLogRepository
{
    Task<PagedResult<AuditLogDto>> SearchAsync(string? searchTerm, string? eventTypeCode, string? actor = null, string? entityName = null, string? tenantId = null, DateTime? fromDate = null, DateTime? toDate = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<AuditLogDto?> GetByIdAsync(Guid auditLogId, CancellationToken cancellationToken = default);
}
