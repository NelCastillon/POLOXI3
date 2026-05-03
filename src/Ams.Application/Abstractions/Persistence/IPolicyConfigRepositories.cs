using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface ICoverageTypeRepository
{
    Task<CoverageTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CoverageTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCoverageTypeRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCoverageTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IPolicyStatusRepository
{
    Task<PolicyStatusDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PolicyStatusDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreatePolicyStatusRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdatePolicyStatusRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IEndorsementTypeRepository
{
    Task<EndorsementTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<EndorsementTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateEndorsementTypeRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateEndorsementTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICancellationReasonRepository
{
    Task<CancellationReasonDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<CancellationReasonDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateCancellationReasonRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCancellationReasonRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICertificateSettingRepository
{
    Task<List<CertificateSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCertificateSettingRequest request, CancellationToken ct = default);
}

public interface IIdCardSettingRepository
{
    Task<List<IdCardSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateIdCardSettingRequest request, CancellationToken ct = default);
}

public interface IPolicyCustomFieldRepository
{
    Task<PolicyCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<PolicyCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreatePolicyCustomFieldRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdatePolicyCustomFieldRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
