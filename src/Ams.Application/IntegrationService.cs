using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Integrations;

namespace Ams.Application;

public sealed class IntegrationService : IIntegrationService
{
    private readonly IIntegrationRepository _repository;
    public IntegrationService(IIntegrationRepository repository) => _repository = repository;

    public Task<PagedResult<IntegrationCatalogDto>> GetCatalogAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetCatalogAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<IntegrationCatalogDto?> GetCatalogItemByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetCatalogItemByIdAsync(id, cancellationToken);

    public Task<PagedResult<CarrierIntegrationStatusDto>> GetCarrierStatusesAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetCarrierStatusesAsync(tenantId, pageNumber, pageSize, cancellationToken);

    public Task<CarrierIntegrationStatusDto?> GetCarrierStatusByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetCarrierStatusByIdAsync(id, cancellationToken);

    public Task<PagedResult<DownloadLogDto>> GetDownloadLogsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetDownloadLogsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<DownloadLogDto?> GetDownloadLogByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetDownloadLogByIdAsync(id, cancellationToken);

    public Task<PagedResult<DownloadExceptionDto>> GetDownloadExceptionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetDownloadExceptionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<DownloadExceptionDto?> GetDownloadExceptionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetDownloadExceptionByIdAsync(id, cancellationToken);

    public Task ResolveDownloadExceptionAsync(Guid id, ResolveDownloadExceptionRequest request, CancellationToken cancellationToken = default)
        => _repository.ResolveDownloadExceptionAsync(id, request, cancellationToken);

    public Task RetryDownloadExceptionAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.RetryDownloadExceptionAsync(id, cancellationToken);

    public Task<PagedResult<WebhookEndpointDto>> GetWebhooksAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetWebhooksAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<WebhookEndpointDto?> GetWebhookByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetWebhookByIdAsync(id, cancellationToken);

    public Task<Guid> CreateWebhookAsync(CreateWebhookEndpointRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateWebhookAsync(request, cancellationToken);

    public Task UpdateWebhookAsync(Guid id, UpdateWebhookEndpointRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateWebhookAsync(id, request, cancellationToken);

    public Task DeleteWebhookAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.DeleteWebhookAsync(id, cancellationToken);

    public Task<PagedResult<AutomationFlowDto>> GetAutomationFlowsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.GetAutomationFlowsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<AutomationFlowDto?> GetAutomationFlowByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetAutomationFlowByIdAsync(id, cancellationToken);

    public Task<Guid> CreateAutomationFlowAsync(CreateAutomationFlowRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAutomationFlowAsync(request, cancellationToken);

    public Task UpdateAutomationFlowAsync(Guid id, UpdateAutomationFlowRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAutomationFlowAsync(id, request, cancellationToken);

    public Task<WorkflowDesignDto?> GetWorkflowDesignByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetWorkflowDesignByIdAsync(id, cancellationToken);

    public Task<Guid> SaveWorkflowDesignAsync(SaveWorkflowDesignRequest request, CancellationToken cancellationToken = default)
        => _repository.SaveWorkflowDesignAsync(request, cancellationToken);
}
