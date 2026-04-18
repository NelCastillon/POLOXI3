using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Sod;

namespace Ams.Application.Abstractions.Services;

public interface ISodRuleService
{
    Task<SegregationOfDutyRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<SegregationOfDutyRuleDto>> SearchAsync(Guid? tenantId, string? searchTerm, string? severityCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSodRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateSodRuleRequest request, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
    Task<Guid> CloneAsync(Guid id, CloneSodRuleRequest request, CancellationToken cancellationToken = default);
}
