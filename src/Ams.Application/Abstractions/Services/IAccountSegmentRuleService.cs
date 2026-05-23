using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountSegments;

namespace Ams.Application.Abstractions.Services;

public interface IAccountSegmentRuleService
{
    Task<Guid> CreateAsync(CreateAccountSegmentRuleRequest request, CancellationToken cancellationToken = default);
    Task<AccountSegmentRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountSegmentRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateAccountSegmentRuleRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
    Task RecalculateAsync(Guid tenantId, Guid? id = null, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default);
}
