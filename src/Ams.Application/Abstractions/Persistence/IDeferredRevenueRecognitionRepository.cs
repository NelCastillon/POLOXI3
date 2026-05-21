using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;

namespace Ams.Application.Abstractions.Persistence;

public interface IDeferredRevenueRecognitionRepository
{
    Task<DeferredRevenueRecognitionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DeferredRevenueRecognitionDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDeferredRevenueRecognitionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateDeferredRevenueRecognitionRequest request, CancellationToken cancellationToken = default);
}
