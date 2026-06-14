using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Integrations;

namespace Ams.Application.Abstractions.Persistence;

public interface IIntegrationRepository
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
    Task<CarrierDownloadDashboardDto> GetCarrierDownloadDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<CarrierDownloadItemDto>> GetCarrierDownloadItemsAsync(Guid tenantId, Guid? batchId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<CarrierDownloadItemDto?> GetCarrierDownloadItemByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateCarrierDownloadBatchAsync(CreateCarrierDownloadBatchRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateCarrierDownloadItemAsync(CreateCarrierDownloadItemRequest request, CancellationToken cancellationToken = default);
    Task UpdateCarrierDownloadItemStatusAsync(Guid id, UpdateCarrierDownloadItemStatusRequest request, CancellationToken cancellationToken = default);
    Task CompleteCarrierDownloadBatchAsync(Guid id, CompleteCarrierDownloadBatchRequest request, CancellationToken cancellationToken = default);

    // Download exceptions
    Task<PagedResult<DownloadExceptionDto>> GetDownloadExceptionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<DownloadExceptionDto?> GetDownloadExceptionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateCarrierDownloadExceptionAsync(CreateCarrierDownloadExceptionRequest request, CancellationToken cancellationToken = default);
    Task ManualMatchCarrierDownloadExceptionAsync(Guid id, ManualCarrierDownloadMatchRequest request, CancellationToken cancellationToken = default);
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
