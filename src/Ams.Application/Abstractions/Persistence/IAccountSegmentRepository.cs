using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountSegments;

namespace Ams.Application.Abstractions.Persistence;

public interface IAccountSegmentRepository
{
    Task<Guid> CreateAsync(CreateAccountSegmentRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(UpdateAccountSegmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AccountSegmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountSegmentDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
