using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Alerts;

namespace Ams.Application;

public sealed class AlertService : IAlertService
{
    private readonly IAlertRepository _repository;

    public AlertService(IAlertRepository repository) => _repository = repository;

    public Task<PagedResult<AlertDto>> SearchAsync(string? searchTerm, string? statusCode, string? severityCode, string? regionCode = null, Guid? tenantId = null, bool? openOnly = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, statusCode, severityCode, regionCode, tenantId, openOnly, pageNumber, pageSize, cancellationToken);

    public Task<AlertDto?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(alertId, cancellationToken);

    public Task AcknowledgeAsync(Guid alertId, AcknowledgeAlertRequest request, CancellationToken cancellationToken = default)
        => _repository.AcknowledgeAsync(alertId, request, cancellationToken);

    public Task ResolveAsync(Guid alertId, ResolveAlertRequest request, CancellationToken cancellationToken = default)
        => _repository.ResolveAsync(alertId, request, cancellationToken);

    public Task AssignAsync(Guid alertId, AssignAlertRequest request, CancellationToken cancellationToken = default)
        => _repository.AssignAsync(alertId, request, cancellationToken);

    public Task EscalateAsync(Guid alertId, EscalateAlertRequest request, CancellationToken cancellationToken = default)
        => _repository.EscalateAsync(alertId, request, cancellationToken);

    public Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default)
        => _repository.GetOpenCountAsync(cancellationToken);
}
