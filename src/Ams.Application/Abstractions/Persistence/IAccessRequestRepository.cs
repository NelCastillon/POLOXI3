using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Governance;

namespace Ams.Application.Abstractions.Persistence;

public interface IAccessRequestRepository
{
    Task<AccessRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AccessRequestDto>> SearchAsync(Guid tenantId, string? searchTerm, string? requestTypeCode, string? statusCode, Guid? requestedForUserId, Guid? requestedByUserId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> SubmitAsync(SubmitAccessRequestRequest request, CancellationToken cancellationToken = default);
    Task ProcessAsync(Guid id, ProcessAccessRequestRequest request, CancellationToken cancellationToken = default);
}
