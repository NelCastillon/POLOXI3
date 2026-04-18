using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class SecurityPolicyService : ISecurityPolicyService
{
    private readonly ISecurityPolicyRepository _repository;
    public SecurityPolicyService(ISecurityPolicyRepository repository) => _repository = repository;
    public Task<SecurityPolicyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<SecurityPolicyDto?> GetByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken = default) => _repository.GetByCodeAsync(tenantId, policyCode, cancellationToken);
    public Task<PagedResult<SecurityPolicyDto>> SearchAsync(Guid tenantId, string? searchTerm, string? resourceCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, resourceCode, pageNumber, pageSize, cancellationToken);
    public Task<IEnumerable<SecurityPolicyDto>> GetActiveByResourceAsync(Guid tenantId, string resourceCode, string actionCode, CancellationToken cancellationToken = default) => _repository.GetActiveByResourceAsync(tenantId, resourceCode, actionCode, cancellationToken);
    public Task<Guid> CreateAsync(CreateSecurityPolicyRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task DeactivateAsync(Guid policyId, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.DeactivateAsync(policyId, modifiedByUserId, cancellationToken);
}
