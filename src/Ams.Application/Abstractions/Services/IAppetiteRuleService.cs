using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Appetite;

namespace Ams.Application.Abstractions.Services;

public interface IAppetiteRuleService
{
    Task<AppetiteRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AppetiteRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateAppetiteRuleRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateAppetiteRuleRequest request, CancellationToken cancellationToken = default);
}
