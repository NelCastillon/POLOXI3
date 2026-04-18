using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Security;

namespace Ams.Application;

public sealed class TrustedDeviceService : ITrustedDeviceService
{
    private readonly ITrustedDeviceRepository _repository;
    public TrustedDeviceService(ITrustedDeviceRepository repository) => _repository = repository;

    public Task<TrustedDeviceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<TrustedDeviceDto>> SearchAsync(Guid tenantId, Guid? userId, string? searchTerm, bool? isActive, bool? highRiskOnly, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, userId, searchTerm, isActive, highRiskOnly, pageNumber, pageSize, cancellationToken);

    public Task RevokeAsync(RevokeTrustedDeviceRequest request, CancellationToken cancellationToken = default)
        => _repository.RevokeAsync(request, cancellationToken);

    public Task SubmitRiskReviewAsync(RiskReviewRequest request, CancellationToken cancellationToken = default)
        => _repository.SubmitRiskReviewAsync(request, cancellationToken);
}
