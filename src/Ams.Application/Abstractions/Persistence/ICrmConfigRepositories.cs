using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CrmConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface ILeadSourceRepository
{
    Task<LeadSourceDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<LeadSourceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateLeadSourceRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateLeadSourceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ILeadStatusRepository
{
    Task<LeadStatusDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<LeadStatusDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateLeadStatusRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateLeadStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IOpportunityStageRepository
{
    Task<OpportunityStageDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<OpportunityStageDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateOpportunityStageRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateOpportunityStageRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IPipelineSettingRepository
{
    Task<List<PipelineSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdatePipelineSettingRequest request, CancellationToken ct = default);
}

public interface IDuplicateRuleRepository
{
    Task<DuplicateRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<DuplicateRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateDuplicateRuleRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateDuplicateRuleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IAssignmentRuleRepository
{
    Task<AssignmentRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AssignmentRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateAssignmentRuleRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateAssignmentRuleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICrmCustomFieldRepository
{
    Task<CrmCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CrmCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCrmCustomFieldRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCrmCustomFieldRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
