using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IVendorRepository
{
    Task<VendorDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<VendorDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateVendorRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateVendorRequest request, CancellationToken cancellationToken = default);
}
