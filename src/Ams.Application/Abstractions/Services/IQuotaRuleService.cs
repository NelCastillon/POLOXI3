using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.QuotaRules;

namespace Ams.Application.Abstractions.Services;

public interface IQuotaRuleService
{
    Task<PagedResult<QuotaRuleDto>> SearchAsync(string? searchTerm, string? planCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<QuotaRuleDto?> GetByIdAsync(Guid quotaRuleId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateQuotaRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid quotaRuleId, UpdateQuotaRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid quotaRuleId, CancellationToken cancellationToken = default);
    Task<Guid> CloneAsync(Guid quotaRuleId, CloneQuotaRuleRequest request, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid quotaRuleId, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid quotaRuleId, CancellationToken cancellationToken = default);
}
