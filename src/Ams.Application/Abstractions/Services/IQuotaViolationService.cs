using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.QuotaViolations;

namespace Ams.Application.Abstractions.Services;

public interface IQuotaViolationService
{
    Task<PagedResult<QuotaViolationDto>> SearchAsync(string? searchTerm, string? statusCode, string? severityCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<QuotaViolationDto?> GetByIdAsync(Guid violationId, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(Guid violationId, AcknowledgeQuotaViolationRequest request, CancellationToken cancellationToken = default);
    Task ResolveAsync(Guid violationId, ResolveQuotaViolationRequest request, CancellationToken cancellationToken = default);
    Task NotifyAsync(Guid violationId, NotifyQuotaViolationRequest request, CancellationToken cancellationToken = default);
    Task ApplyRestrictionAsync(Guid violationId, ApplyRestrictionRequest request, CancellationToken cancellationToken = default);
    Task GrantTemporaryIncreaseAsync(Guid violationId, GrantTemporaryIncreaseRequest request, CancellationToken cancellationToken = default);
    Task ConvertToOverageAsync(Guid violationId, ConvertToOverageRequest request, CancellationToken cancellationToken = default);
    Task<int> GetOpenCountAsync(CancellationToken cancellationToken = default);
}
