using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountSegments;

namespace Ams.Application;

public sealed class AccountSegmentRuleService : IAccountSegmentRuleService
{
    private readonly IAccountSegmentRuleRepository _repository;

    public AccountSegmentRuleService(IAccountSegmentRuleRepository repository)
    {
        _repository = repository;
    }

    public Task<Guid> CreateAsync(CreateAccountSegmentRuleRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task<AccountSegmentRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<AccountSegmentRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task UpdateAsync(Guid id, UpdateAccountSegmentRuleRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(id, request, cancellationToken);

    public Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(id, modifiedByUserId, cancellationToken);

    public Task RecalculateAsync(Guid tenantId, Guid? id = null, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
        => _repository.RecalculateAsync(tenantId, id, modifiedByUserId, cancellationToken);
}
