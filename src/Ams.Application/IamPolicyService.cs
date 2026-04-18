using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Iam;

namespace Ams.Application;

public sealed class IamPolicyService : IIamPolicyService
{
    private readonly IIamPolicyRepository _repository;
    public IamPolicyService(IIamPolicyRepository repository) => _repository = repository;
    public Task<FieldSecurityPolicyDto?> GetFieldPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetFieldPolicyByIdAsync(id, cancellationToken);
    public Task<PagedResult<FieldSecurityPolicyDto>> SearchFieldPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchFieldPoliciesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateFieldPolicyAsync(CreateFieldSecurityPolicyRequest request, CancellationToken cancellationToken = default) => _repository.CreateFieldPolicyAsync(request, cancellationToken);
    public Task DeleteFieldPolicyAsync(Guid policyId, Guid? deletedByUserId, CancellationToken cancellationToken = default) => _repository.DeleteFieldPolicyAsync(policyId, deletedByUserId, cancellationToken);
    public Task<RecordSecurityPolicyDto?> GetRecordPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetRecordPolicyByIdAsync(id, cancellationToken);
    public Task<PagedResult<RecordSecurityPolicyDto>> SearchRecordPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchRecordPoliciesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateRecordPolicyAsync(CreateRecordSecurityPolicyRequest request, CancellationToken cancellationToken = default) => _repository.CreateRecordPolicyAsync(request, cancellationToken);
    public Task DeleteRecordPolicyAsync(Guid policyId, Guid? deletedByUserId, CancellationToken cancellationToken = default) => _repository.DeleteRecordPolicyAsync(policyId, deletedByUserId, cancellationToken);
}
