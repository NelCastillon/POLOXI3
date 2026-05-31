using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountConfig;

namespace Ams.Application.Abstractions.Persistence;

public interface IAccountTypeRepository
{
    Task<AccountTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AccountTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateAccountTypeRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateAccountTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IRelationshipTypeRepository
{
    Task<RelationshipTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<RelationshipTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateRelationshipTypeRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateRelationshipTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IAccountReferenceOptionRepository
{
    Task<List<AccountReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken ct = default);
}

public interface IHouseholdSettingRepository
{
    Task<List<HouseholdSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateHouseholdSettingRequest request, CancellationToken ct = default);
}

public interface ICommercialEntitySettingRepository
{
    Task<List<CommercialEntitySettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCommercialEntitySettingRequest request, CancellationToken ct = default);
}

public interface IContactTypeRepository
{
    Task<ContactTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ContactTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateContactTypeRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateContactTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IAccountCustomFieldRepository
{
    Task<AccountCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AccountCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateAccountCustomFieldRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateAccountCustomFieldRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
