using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application.Abstractions.Persistence;

public interface IPrivilegedAccessRepository
{
    Task<PrivilegedAccessRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PrivilegedAccessRequestDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> SubmitAsync(SubmitPrivilegedAccessRequest request, CancellationToken cancellationToken = default);
    Task ReviewAsync(ReviewAccessDecisionRequest request, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid requestId, Guid revokedByUserId, string reason, CancellationToken cancellationToken = default);
}
