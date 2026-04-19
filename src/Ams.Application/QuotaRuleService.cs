using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.QuotaRules;

namespace Ams.Application;

public sealed class QuotaRuleService : IQuotaRuleService
{
    private readonly IQuotaRuleRepository _repository;

    public QuotaRuleService(IQuotaRuleRepository repository) => _repository = repository;

    public Task<PagedResult<QuotaRuleDto>> SearchAsync(string? searchTerm, string? planCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, planCode, isActive, pageNumber, pageSize, cancellationToken);

    public Task<QuotaRuleDto?> GetByIdAsync(Guid quotaRuleId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(quotaRuleId, cancellationToken);

    public Task<Guid> CreateAsync(CreateQuotaRuleRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateAsync(request, cancellationToken);

    public Task UpdateAsync(Guid quotaRuleId, UpdateQuotaRuleRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateAsync(quotaRuleId, request, cancellationToken);

    public Task DeleteAsync(Guid quotaRuleId, CancellationToken cancellationToken = default)
        => _repository.DeleteAsync(quotaRuleId, cancellationToken);

    public Task<Guid> CloneAsync(Guid quotaRuleId, CloneQuotaRuleRequest request, CancellationToken cancellationToken = default)
        => _repository.CloneAsync(quotaRuleId, request, cancellationToken);

    public Task ActivateAsync(Guid quotaRuleId, CancellationToken cancellationToken = default)
        => _repository.ActivateAsync(quotaRuleId, cancellationToken);

    public Task DeactivateAsync(Guid quotaRuleId, CancellationToken cancellationToken = default)
        => _repository.DeactivateAsync(quotaRuleId, cancellationToken);
}
