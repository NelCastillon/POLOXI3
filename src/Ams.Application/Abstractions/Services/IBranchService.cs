using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Agency;

namespace Ams.Application.Abstractions.Services;

public interface IBranchService
{
    Task<BranchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<BranchDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default);
}
