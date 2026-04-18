using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IRateCardLineService
{
    Task<RateCardLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RateCardLineDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
