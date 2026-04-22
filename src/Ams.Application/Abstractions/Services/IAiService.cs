using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IAiService
{
    Task<PagedResult<AiInsightDto>> GetInsightsAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<AiAccountSummaryDto?> GetAccountSummaryAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<PagedResult<AiNextActionDto>> GetNextActionsAsync(Guid tenantId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
