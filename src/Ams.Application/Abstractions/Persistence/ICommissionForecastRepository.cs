using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Persistence;

public interface ICommissionForecastRepository
{
    Task<CommissionForecastDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CommissionForecastDto>> SearchAsync(Guid tenantId, string? searchTerm, string? statusCode = null, string? scenarioCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCommissionForecastRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCommissionForecastRequest request, CancellationToken cancellationToken = default);
    Task EnsureSeedAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
