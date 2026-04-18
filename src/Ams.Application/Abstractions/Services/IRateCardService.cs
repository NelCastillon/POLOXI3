using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IRateCardService
{
    Task<RateCardDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<RateCardDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
