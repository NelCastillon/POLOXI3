using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Subscriptions;

namespace Ams.Application;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _repository;
    public SubscriptionService(ISubscriptionRepository repository) => _repository = repository;

    public Task<SubscriptionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<SubscriptionDto>> SearchAsync(string? searchTerm = null, Guid? tenantId = null, Guid? planId = null, string? statusCode = null, string? renewalType = null, string? billingCycle = null, bool? pastDue = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, tenantId, planId, statusCode, renewalType, billingCycle, pastDue, pageNumber, pageSize, cancellationToken);

    public Task<Guid> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpgradeAsync(Guid id, Guid newPlanId, CancellationToken cancellationToken = default)
        => _repository.UpgradeAsync(id, newPlanId, cancellationToken);

    public Task DowngradeAsync(Guid id, Guid newPlanId, CancellationToken cancellationToken = default)
        => _repository.DowngradeAsync(id, newPlanId, cancellationToken);

    public Task RenewAsync(Guid id, DateTime newEndDateUtc, CancellationToken cancellationToken = default)
        => _repository.RenewAsync(id, newEndDateUtc, cancellationToken);

    public Task CancelAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.CancelAsync(id, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, cancellationToken);
}
