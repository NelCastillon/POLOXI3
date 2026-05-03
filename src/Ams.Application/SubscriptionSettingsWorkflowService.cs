using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantSettings;

namespace Ams.Application;

public sealed class SubscriptionSettingsWorkflowService : ISubscriptionSettingsWorkflowService
{
    private readonly ISubscriptionSettingsWorkflowRepository _repository;

    public SubscriptionSettingsWorkflowService(ISubscriptionSettingsWorkflowRepository repository)
        => _repository = repository;

    public Task<IReadOnlyList<SubscriptionSettingsWorkflowItemDto>> GetByPageAsync(Guid tenantId, string pageCode, CancellationToken cancellationToken = default)
        => _repository.GetByPageAsync(tenantId, pageCode, cancellationToken);

    public Task<Guid> CreateAsync(CreateSubscriptionSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid workflowItemId, UpdateSubscriptionSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(workflowItemId, request, cancellationToken);

    public Task AdvanceAsync(Guid workflowItemId, AdvanceSubscriptionSettingsWorkflowRequest request, CancellationToken cancellationToken = default)
        => _repository.AdvanceAsync(workflowItemId, request, cancellationToken);

    public Task DeleteAsync(Guid workflowItemId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(workflowItemId, modifiedByUserId, cancellationToken);
}
