using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Alerts;

namespace Ams.Application.Abstractions.Persistence;

public interface IAlertRepository
{
    Task<PagedResult<AlertDto>> SearchAsync(string? searchTerm, string? statusCode, string? severityCode, string? regionCode = null, Guid? tenantId = null, bool? openOnly = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<AlertDto?> GetByIdAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(Guid alertId, AcknowledgeAlertRequest request, CancellationToken cancellationToken = default);
    Task ResolveAsync(Guid alertId, ResolveAlertRequest request, CancellationToken cancellationToken = default);
    Task AssignAsync(Guid alertId, AssignAlertRequest request, CancellationToken cancellationToken = default);
    Task EscalateAsync(Guid alertId, EscalateAlertRequest request, CancellationToken cancellationToken = default);
    Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default);
}
