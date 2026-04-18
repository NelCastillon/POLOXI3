using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class PrivilegedAccessService : IPrivilegedAccessService
{
    private readonly IPrivilegedAccessRepository _repository;
    public PrivilegedAccessService(IPrivilegedAccessRepository repository) => _repository = repository;
    public Task<PrivilegedAccessRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<PrivilegedAccessRequestDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> SubmitAsync(SubmitPrivilegedAccessRequest request, CancellationToken cancellationToken = default) => _repository.SubmitAsync(request, cancellationToken);
    public Task ReviewAsync(ReviewAccessDecisionRequest request, CancellationToken cancellationToken = default) => _repository.ReviewAsync(request, cancellationToken);
    public Task RevokeAsync(Guid requestId, Guid revokedByUserId, string reason, CancellationToken cancellationToken = default) => _repository.RevokeAsync(requestId, revokedByUserId, reason, cancellationToken);
}
