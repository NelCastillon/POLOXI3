using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PortalInvites;

namespace Ams.Application.Abstractions.Services;

public interface IPortalInviteService
{
    Task<Guid> CreateAsync(CreatePortalInviteRequest request, CancellationToken cancellationToken = default);
    Task<PortalInviteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<PortalInviteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
