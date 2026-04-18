using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Sod;

namespace Ams.Application;

public sealed class SodRuleService : ISodRuleService
{
    private readonly ISodRuleRepository _repository;
    public SodRuleService(ISodRuleRepository repository) => _repository = repository;

    public Task<SegregationOfDutyRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetByIdAsync(id, cancellationToken);
    public Task<PagedResult<SegregationOfDutyRuleDto>> SearchAsync(Guid? tenantId, string? searchTerm, string? severityCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default) => _repository.SearchAsync(tenantId, searchTerm, severityCode, isActive, pageNumber, pageSize, cancellationToken);
    public Task<Guid> CreateAsync(CreateSodRuleRequest request, CancellationToken cancellationToken = default) => _repository.CreateAsync(request, cancellationToken);
    public Task UpdateAsync(Guid id, UpdateSodRuleRequest request, CancellationToken cancellationToken = default) => _repository.UpdateAsync(id, request, cancellationToken);
    public Task SetActiveAsync(Guid id, bool isActive, Guid? modifiedByUserId, CancellationToken cancellationToken = default) => _repository.SetActiveAsync(id, isActive, modifiedByUserId, cancellationToken);
    public Task<Guid> CloneAsync(Guid id, CloneSodRuleRequest request, CancellationToken cancellationToken = default) => _repository.CloneAsync(id, request, cancellationToken);
}
