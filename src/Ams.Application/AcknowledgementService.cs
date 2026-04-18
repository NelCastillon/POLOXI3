using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class AcknowledgementService : IAcknowledgementService
{
    private readonly IAcknowledgementRepository _repository;

    public AcknowledgementService(IAcknowledgementRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<PendingAcknowledgementDto>> GetPendingAsync(Guid? tenantId, Guid? policyId, string? searchTerm, CancellationToken ct = default)
        => _repository.GetPendingAsync(tenantId, policyId, searchTerm, ct);

    public Task<IReadOnlyList<PendingAcknowledgementDto>> GetOverdueAsync(Guid? tenantId, Guid? policyId, string? searchTerm, CancellationToken ct = default)
        => _repository.GetOverdueAsync(tenantId, policyId, searchTerm, ct);

    public Task<PagedResult<AcknowledgementDetailDto>> SearchAcknowledgedAsync(Guid? tenantId, Guid? policyId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
        => _repository.SearchAcknowledgedAsync(tenantId, policyId, searchTerm, pageNumber, pageSize, ct);

    public Task<AcknowledgementSummaryDto> GetSummaryAsync(Guid? tenantId, CancellationToken ct = default)
        => _repository.GetSummaryAsync(tenantId, ct);
}
