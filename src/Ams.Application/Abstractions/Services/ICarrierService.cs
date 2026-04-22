using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Carriers;

namespace Ams.Application.Abstractions.Services;

public interface ICarrierService
{
    Task<CarrierDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<CarrierDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateCarrierRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateCarrierRequest request, CancellationToken cancellationToken = default);
}
