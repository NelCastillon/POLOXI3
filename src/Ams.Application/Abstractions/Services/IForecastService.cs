using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Forecast;

namespace Ams.Application.Abstractions.Services;

public interface IForecastService
{
    Task<Guid> CreateAsync(CreateForecastEntryRequest request, CancellationToken cancellationToken = default);
    Task<ForecastEntryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ForecastEntryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
