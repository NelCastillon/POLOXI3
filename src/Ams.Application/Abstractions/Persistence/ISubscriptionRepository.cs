using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Subscriptions;

namespace Ams.Application.Abstractions.Persistence;

public interface ISubscriptionRepository
{
    Task<SubscriptionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<SubscriptionDto>> SearchAsync(string? searchTerm = null, Guid? tenantId = null, Guid? planId = null, string? statusCode = null, string? renewalType = null, string? billingCycle = null, bool? pastDue = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default);
    Task UpgradeAsync(Guid id, Guid newPlanId, CancellationToken cancellationToken = default);
    Task DowngradeAsync(Guid id, Guid newPlanId, CancellationToken cancellationToken = default);
    Task RenewAsync(Guid id, DateTime newEndDateUtc, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
