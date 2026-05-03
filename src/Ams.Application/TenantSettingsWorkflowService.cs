using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantSettings;

namespace Ams.Application;

public sealed class TenantSettingsWorkflowService : ITenantSettingsWorkflowService
{
    private readonly ITenantSettingsWorkflowRepository _repository;

    public TenantSettingsWorkflowService(ITenantSettingsWorkflowRepository repository)
        => _repository = repository;

    public Task<IReadOnlyList<TenantSettingsWorkflowItemDto>> GetByPageAsync(Guid tenantId, string pageCode, CancellationToken cancellationToken = default)
        => _repository.GetByPageAsync(tenantId, pageCode, cancellationToken);

    public Task<Guid> CreateAsync(CreateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid workflowItemId, UpdateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(workflowItemId, request, cancellationToken);

    public Task AdvanceAsync(Guid workflowItemId, AdvanceTenantSettingsWorkflowRequest request, CancellationToken cancellationToken = default)
        => _repository.AdvanceAsync(workflowItemId, request, cancellationToken);

    public Task DeleteAsync(Guid workflowItemId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(workflowItemId, modifiedByUserId, cancellationToken);
}
