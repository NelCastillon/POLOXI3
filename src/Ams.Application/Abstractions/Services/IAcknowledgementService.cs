using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IAcknowledgementService
{
    Task<IReadOnlyList<PendingAcknowledgementDto>> GetPendingAsync(Guid? tenantId, Guid? policyId, string? searchTerm, CancellationToken ct = default);
    Task<IReadOnlyList<PendingAcknowledgementDto>> GetOverdueAsync(Guid? tenantId, Guid? policyId, string? searchTerm, CancellationToken ct = default);
    Task<PagedResult<AcknowledgementDetailDto>> SearchAcknowledgedAsync(Guid? tenantId, Guid? policyId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default);
    Task<AcknowledgementSummaryDto> GetSummaryAsync(Guid? tenantId, CancellationToken ct = default);
}
