using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;

namespace Ams.Application.Abstractions.Services;

public interface IEnterpriseAuditService
{
    Task<Guid> LogAsync(LogEnterpriseAuditEventRequest request, CancellationToken cancellationToken = default);
    Task<Guid> LogEntityAuditAsync(LogEntityAuditRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<EnterpriseAuditEventDto>> SearchAsync(SearchEnterpriseAuditEventsRequest request, CancellationToken cancellationToken = default);
    Task<EnterpriseAuditEventDto?> GetByIdAsync(Guid tenantId, Guid auditEventId, CancellationToken cancellationToken = default);
    Task<EnterpriseAuditSummaryDto> GetSummaryAsync(Guid tenantId, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EnterpriseAuditAlertDto>> GetOpenAlertsAsync(Guid tenantId, int top = 10, CancellationToken cancellationToken = default);
    Task<EnterpriseAuditOptionsDto> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EnterpriseAuditCapabilityDto>> GetCapabilitiesAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
