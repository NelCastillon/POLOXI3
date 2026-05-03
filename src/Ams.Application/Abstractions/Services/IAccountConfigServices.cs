using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountConfig;

namespace Ams.Application.Abstractions.Services;

public interface IAccountTypeService
{
    Task<AccountTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AccountTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateAccountTypeRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateAccountTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IRelationshipTypeService
{
    Task<RelationshipTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<RelationshipTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateRelationshipTypeRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateRelationshipTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IHouseholdSettingService
{
    Task<List<HouseholdSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateHouseholdSettingRequest request, CancellationToken ct = default);
}

public interface ICommercialEntitySettingService
{
    Task<List<CommercialEntitySettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateCommercialEntitySettingRequest request, CancellationToken ct = default);
}

public interface IContactTypeService
{
    Task<ContactTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<ContactTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateContactTypeRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateContactTypeRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IAccountCustomFieldService
{
    Task<AccountCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<AccountCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Guid> CreateAsync(CreateAccountCustomFieldRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateAccountCustomFieldRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
