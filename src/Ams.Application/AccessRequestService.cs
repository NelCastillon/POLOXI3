using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Governance;

namespace Ams.Application;

public sealed class AccessRequestService : IAccessRequestService
{
    private readonly IAccessRequestRepository _repository;
    public AccessRequestService(IAccessRequestRepository repository) => _repository = repository;
    public Task<AccessRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<AccessRequestDto>> SearchAsync(Guid tenantId, string? searchTerm, string? requestTypeCode, string? statusCode, Guid? requestedForUserId, Guid? requestedByUserId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, requestTypeCode, statusCode, requestedForUserId, requestedByUserId, pageNumber, pageSize, cancellationToken);
    public Task<Guid> SubmitAsync(SubmitAccessRequestRequest request, CancellationToken cancellationToken = default) => _repository.SubmitAsync(request, cancellationToken);
    public Task ProcessAsync(Guid id, ProcessAccessRequestRequest request, CancellationToken cancellationToken = default) => _repository.ProcessAsync(id, request, cancellationToken);
}
