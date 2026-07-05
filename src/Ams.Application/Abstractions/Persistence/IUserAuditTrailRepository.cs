using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;

namespace Ams.Application.Abstractions.Persistence;

public interface IUserAuditTrailRepository
{
    Task<Guid> LogAsync(LogUserAuditTrailRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<UserAuditTrailDto>> SearchAsync(SearchUserAuditTrailRequest request, CancellationToken cancellationToken = default);
    Task<UserAuditTrailDto?> GetByIdAsync(Guid tenantId, Guid auditTrailId, CancellationToken cancellationToken = default);
    Task<UserAuditTrailSummaryDto> GetSummaryAsync(Guid tenantId, DateTime? fromDateUtc = null, DateTime? toDateUtc = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAuditActionTypeDto>> GetActionTypesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
