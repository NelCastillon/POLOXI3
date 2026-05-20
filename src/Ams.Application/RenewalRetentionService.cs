using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.RenewalRetention;

namespace Ams.Application;

public sealed class RenewalRetentionService : IRenewalRetentionService
{
    private readonly IRenewalRetentionRepository _repository;

    public RenewalRetentionService(IRenewalRetentionRepository repository) => _repository = repository;

    public Task<RenewalRetentionCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetCenterAsync(tenantId, cancellationToken);

    public Task<RenewalRetentionDetailDto?> GetDetailAsync(Guid retentionCaseId, CancellationToken cancellationToken = default)
        => _repository.GetDetailAsync(retentionCaseId, cancellationToken);

    public Task<Guid> CreateCaseAsync(CreateRenewalRetentionCaseRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateCaseAsync(request, cancellationToken);

    public Task UpdateStageAsync(Guid retentionCaseId, UpdateRenewalRetentionStageRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateStageAsync(retentionCaseId, request, cancellationToken);

    public Task<Guid> AddActivityAsync(CreateRenewalRetentionActivityRequest request, CancellationToken cancellationToken = default)
        => _repository.AddActivityAsync(request, cancellationToken);

    public Task<Guid> AddOfferAsync(CreateRenewalRetentionOfferRequest request, CancellationToken cancellationToken = default)
        => _repository.AddOfferAsync(request, cancellationToken);

    public Task UpdateOfferStatusAsync(Guid retentionOfferId, UpdateRenewalRetentionOfferStatusRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateOfferStatusAsync(retentionOfferId, request, cancellationToken);
}
