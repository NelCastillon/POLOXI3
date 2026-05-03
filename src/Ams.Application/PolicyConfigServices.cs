using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyConfig;

namespace Ams.Application;

public sealed class CoverageTypeService : ICoverageTypeService
{
    private readonly ICoverageTypeRepository _repo;
    public CoverageTypeService(ICoverageTypeRepository repo) => _repo = repo;
    public Task<CoverageTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<CoverageTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateCoverageTypeRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateCoverageTypeRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class PolicyStatusService : IPolicyStatusService
{
    private readonly IPolicyStatusRepository _repo;
    public PolicyStatusService(IPolicyStatusRepository repo) => _repo = repo;
    public Task<PolicyStatusDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<PolicyStatusDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreatePolicyStatusRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdatePolicyStatusRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class EndorsementTypeService : IEndorsementTypeService
{
    private readonly IEndorsementTypeRepository _repo;
    public EndorsementTypeService(IEndorsementTypeRepository repo) => _repo = repo;
    public Task<EndorsementTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<EndorsementTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateEndorsementTypeRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateEndorsementTypeRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class CancellationReasonService : ICancellationReasonService
{
    private readonly ICancellationReasonRepository _repo;
    public CancellationReasonService(ICancellationReasonRepository repo) => _repo = repo;
    public Task<CancellationReasonDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<CancellationReasonDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateCancellationReasonRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateCancellationReasonRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class CertificateSettingService : ICertificateSettingService
{
    private readonly ICertificateSettingRepository _repo;
    public CertificateSettingService(ICertificateSettingRepository repo) => _repo = repo;
    public Task<List<CertificateSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => _repo.GetAllAsync(tenantId, ct);
    public Task UpdateAsync(Guid id, UpdateCertificateSettingRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
}

public sealed class IdCardSettingService : IIdCardSettingService
{
    private readonly IIdCardSettingRepository _repo;
    public IdCardSettingService(IIdCardSettingRepository repo) => _repo = repo;
    public Task<List<IdCardSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => _repo.GetAllAsync(tenantId, ct);
    public Task UpdateAsync(Guid id, UpdateIdCardSettingRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
}

public sealed class PolicyCustomFieldService : IPolicyCustomFieldService
{
    private readonly IPolicyCustomFieldRepository _repo;
    public PolicyCustomFieldService(IPolicyCustomFieldRepository repo) => _repo = repo;
    public Task<PolicyCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<PolicyCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreatePolicyCustomFieldRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdatePolicyCustomFieldRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
