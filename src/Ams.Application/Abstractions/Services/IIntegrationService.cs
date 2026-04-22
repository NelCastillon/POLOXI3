using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Integrations;

namespace Ams.Application.Abstractions.Services;

public interface IIntegrationService
{
    // Catalog
    Task<PagedResult<IntegrationCatalogDto>> GetCatalogAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IntegrationCatalogDto?> GetCatalogItemByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Carrier status
    Task<PagedResult<CarrierIntegrationStatusDto>> GetCarrierStatusesAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<CarrierIntegrationStatusDto?> GetCarrierStatusByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Download logs
    Task<PagedResult<DownloadLogDto>> GetDownloadLogsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<DownloadLogDto?> GetDownloadLogByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // Download exceptions
    Task<PagedResult<DownloadExceptionDto>> GetDownloadExceptionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<DownloadExceptionDto?> GetDownloadExceptionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task ResolveDownloadExceptionAsync(Guid id, ResolveDownloadExceptionRequest request, CancellationToken cancellationToken = default);
    Task RetryDownloadExceptionAsync(Guid id, CancellationToken cancellationToken = default);

    // Webhooks
    Task<PagedResult<WebhookEndpointDto>> GetWebhooksAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<WebhookEndpointDto?> GetWebhookByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateWebhookAsync(CreateWebhookEndpointRequest request, CancellationToken cancellationToken = default);
    Task UpdateWebhookAsync(Guid id, UpdateWebhookEndpointRequest request, CancellationToken cancellationToken = default);
    Task DeleteWebhookAsync(Guid id, CancellationToken cancellationToken = default);

    // Automation flows
    Task<PagedResult<AutomationFlowDto>> GetAutomationFlowsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<AutomationFlowDto?> GetAutomationFlowByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAutomationFlowAsync(CreateAutomationFlowRequest request, CancellationToken cancellationToken = default);
    Task UpdateAutomationFlowAsync(Guid id, UpdateAutomationFlowRequest request, CancellationToken cancellationToken = default);

    // Workflow designer
    Task<WorkflowDesignDto?> GetWorkflowDesignByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> SaveWorkflowDesignAsync(SaveWorkflowDesignRequest request, CancellationToken cancellationToken = default);
}
