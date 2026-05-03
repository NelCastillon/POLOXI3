using Ams.Application.Common.Dtos;
using Ams.Application.Features.TenantSettings;

namespace Ams.Application.Abstractions.Services;

public interface ITenantSettingsWorkflowService
{
    Task<IReadOnlyList<TenantSettingsWorkflowItemDto>> GetByPageAsync(Guid tenantId, string pageCode, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid workflowItemId, UpdateTenantSettingsWorkflowItemRequest request, CancellationToken cancellationToken = default);
    Task AdvanceAsync(Guid workflowItemId, AdvanceTenantSettingsWorkflowRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid workflowItemId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
