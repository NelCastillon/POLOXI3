using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;

namespace Ams.Application;

public sealed class EnterpriseAuditService : IEnterpriseAuditService
{
    private readonly IEnterpriseAuditRepository _repository;

    public EnterpriseAuditService(IEnterpriseAuditRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> LogAsync(LogEnterpriseAuditEventRequest request, CancellationToken cancellationToken = default)
        => _repository.LogAsync(request, cancellationToken);

    public Task<Guid> LogEntityAuditAsync(LogEntityAuditRequest request, CancellationToken cancellationToken = default)
        => _repository.LogEntityAuditAsync(request, cancellationToken);

    public Task<PagedResult<EnterpriseAuditEventDto>> SearchAsync(SearchEnterpriseAuditEventsRequest request, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(request, cancellationToken);

    public Task<EnterpriseAuditEventDto?> GetByIdAsync(Guid tenantId, Guid auditEventId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(tenantId, auditEventId, cancellationToken);

    public Task<EnterpriseAuditSummaryDto> GetSummaryAsync(Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default)
        => _repository.GetSummaryAsync(tenantId, fromUtc, toUtc, cancellationToken);

    public Task<IReadOnlyList<EnterpriseAuditAlertDto>> GetOpenAlertsAsync(Guid tenantId, int top = 10, CancellationToken cancellationToken = default)
        => _repository.GetOpenAlertsAsync(tenantId, top, cancellationToken);

    public Task<EnterpriseAuditOptionsDto> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetOptionsAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<EnterpriseAuditCapabilityDto>> GetCapabilitiesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetCapabilitiesAsync(tenantId, cancellationToken);
}
