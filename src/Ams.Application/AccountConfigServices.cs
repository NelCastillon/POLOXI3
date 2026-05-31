using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountConfig;

namespace Ams.Application;

public sealed class AccountTypeService : IAccountTypeService
{
    private readonly IAccountTypeRepository _repo;
    public AccountTypeService(IAccountTypeRepository repo) => _repo = repo;
    public Task<AccountTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<AccountTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateAccountTypeRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateAccountTypeRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class RelationshipTypeService : IRelationshipTypeService
{
    private readonly IRelationshipTypeRepository _repo;
    public RelationshipTypeService(IRelationshipTypeRepository repo) => _repo = repo;
    public Task<RelationshipTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<RelationshipTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateRelationshipTypeRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateRelationshipTypeRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class AccountReferenceOptionService : IAccountReferenceOptionService
{
    private readonly IAccountReferenceOptionRepository _repo;
    public AccountReferenceOptionService(IAccountReferenceOptionRepository repo) => _repo = repo;
    public Task<List<AccountReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken ct = default) => _repo.GetAllAsync(tenantId, optionGroup, ct);
}

public sealed class HouseholdSettingService : IHouseholdSettingService
{
    private readonly IHouseholdSettingRepository _repo;
    public HouseholdSettingService(IHouseholdSettingRepository repo) => _repo = repo;
    public Task<List<HouseholdSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => _repo.GetAllAsync(tenantId, ct);
    public Task UpdateAsync(Guid id, UpdateHouseholdSettingRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
}

public sealed class CommercialEntitySettingService : ICommercialEntitySettingService
{
    private readonly ICommercialEntitySettingRepository _repo;
    public CommercialEntitySettingService(ICommercialEntitySettingRepository repo) => _repo = repo;
    public Task<List<CommercialEntitySettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => _repo.GetAllAsync(tenantId, ct);
    public Task UpdateAsync(Guid id, UpdateCommercialEntitySettingRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
}

public sealed class ContactTypeService : IContactTypeService
{
    private readonly IContactTypeRepository _repo;
    public ContactTypeService(IContactTypeRepository repo) => _repo = repo;
    public Task<ContactTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<ContactTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateContactTypeRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateContactTypeRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}

public sealed class AccountCustomFieldService : IAccountCustomFieldService
{
    private readonly IAccountCustomFieldRepository _repo;
    public AccountCustomFieldService(IAccountCustomFieldRepository repo) => _repo = repo;
    public Task<AccountCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);
    public Task<PagedResult<AccountCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default) => _repo.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, ct);
    public Task<Guid> CreateAsync(CreateAccountCustomFieldRequest request, CancellationToken ct = default) => _repo.CreateAsync(request, ct);
    public Task UpdateAsync(Guid id, UpdateAccountCustomFieldRequest request, CancellationToken ct = default) => _repo.UpdateAsync(id, request, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => _repo.DeleteAsync(id, ct);
}
