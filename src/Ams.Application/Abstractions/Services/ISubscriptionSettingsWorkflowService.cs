using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantSettings;

namespace Ams.Application.Abstractions.Services;

public interface ISubscriptionSettingsWorkflowService
{
    Task<IReadOnlyList<SubscriptionSettingsWorkflowItemDto>> GetByPageAsync(Guid tenantId, string pageCode, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSubscriptionSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid workflowItemId, UpdateSubscriptionSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default);
    Task AdvanceAsync(Guid workflowItemId, AdvanceSubscriptionSettingsWorkflowRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid workflowItemId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
