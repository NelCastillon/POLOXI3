using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CrmConfig;

namespace Ams.Application;

public sealed class LeadSourceService : ILeadSourceService
{
    private readonly ILeadSourceRepository _repo;
    public LeadSourceService(ILeadSourceRepository repo) => _repo = repo;
    public Task<LeadSourceDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<LeadSourceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateLeadSourceRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateLeadSourceRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class LeadStatusService : ILeadStatusService
{
    private readonly ILeadStatusRepository _repo;
    public LeadStatusService(ILeadStatusRepository repo) => _repo = repo;
    public Task<LeadStatusDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<LeadStatusDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateLeadStatusRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateLeadStatusRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class OpportunityStageService : IOpportunityStageService
{
    private readonly IOpportunityStageRepository _repo;
    public OpportunityStageService(IOpportunityStageRepository repo) => _repo = repo;
    public Task<OpportunityStageDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<OpportunityStageDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateOpportunityStageRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateOpportunityStageRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class PipelineSettingService : IPipelineSettingService
{
    private readonly IPipelineSettingRepository _repo;
    public PipelineSettingService(IPipelineSettingRepository repo) => _repo = repo;
    public Task<List<PipelineSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => _repo.GetAllAsync(tenantId, ct);
    public Task UpdateAsync(Guid id, UpdatePipelineSettingRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
}

public sealed class DuplicateRuleService : IDuplicateRuleService
{
    private readonly IDuplicateRuleRepository _repo;
    public DuplicateRuleService(IDuplicateRuleRepository repo) => _repo = repo;
    public Task<DuplicateRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<DuplicateRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateDuplicateRuleRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateDuplicateRuleRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class AssignmentRuleService : IAssignmentRuleService
{
    private readonly IAssignmentRuleRepository _repo;
    public AssignmentRuleService(IAssignmentRuleRepository repo) => _repo = repo;
    public Task<AssignmentRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<AssignmentRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateAssignmentRuleRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateAssignmentRuleRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class LeadActivityOutcomeService : ILeadActivityOutcomeService
{
    private readonly ILeadActivityOutcomeRepository _repo;
    public LeadActivityOutcomeService(ILeadActivityOutcomeRepository repo) => _repo = repo;
    public Task<LeadActivityOutcomeDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<LeadActivityOutcomeDto>> SearchAsync(Guid tenantId, string? searchTerm, string? activityTypeCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, activityTypeCode, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateLeadActivityOutcomeRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateLeadActivityOutcomeRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken ct = default) => _repo.DeleteAsync(id, modifiedByUserId, ct);
}

public sealed class LeadActivityTypeService : ILeadActivityTypeService
{
    private readonly ILeadActivityTypeRepository _repo;
    public LeadActivityTypeService(ILeadActivityTypeRepository repo) => _repo = repo;
    public Task<LeadActivityTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<LeadActivityTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateLeadActivityTypeRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateLeadActivityTypeRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken ct = default) => _repo.DeleteAsync(id, modifiedByUserId, ct);
}

public sealed class CrmCustomFieldService : ICrmCustomFieldService
{
    private readonly ICrmCustomFieldRepository _repo;
    public CrmCustomFieldService(ICrmCustomFieldRepository repo) => _repo = repo;
    public Task<CrmCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<CrmCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateCrmCustomFieldRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateCrmCustomFieldRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
