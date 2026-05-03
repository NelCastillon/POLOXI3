using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountConfig;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountTypeRepository : IAccountTypeRepository
{
    private readonly ISqlConnectionFactory _cf;
    public AccountTypeRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "AccountTypeId, TenantId, TypeCode, TypeName, Category, Description, IsDefault, IsActive, SortOrder, CreatedDateUtc";

    public async Task<AccountTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<AccountTypeDto>(new CommandDefinition($"SELECT {Cols} FROM Client.AccountType WHERE AccountTypeId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<AccountTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Client.AccountType", Cols, "TypeName LIKE '%'+@SearchTerm+'%' OR TypeCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, TypeName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<AccountTypeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AccountTypeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateAccountTypeRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO Client.AccountType (AccountTypeId,TenantId,TypeCode,TypeName,Category,Description,IsDefault,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@TypeCode,@TypeName,@Category,@Description,@IsDefault,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.TypeCode, r.TypeName, r.Category, r.Description, r.IsDefault, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateAccountTypeRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE Client.AccountType SET TypeCode=@TypeCode,TypeName=@TypeName,Category=@Category,Description=@Description,IsDefault=@IsDefault,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE AccountTypeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TypeCode, r.TypeName, r.Category, r.Description, r.IsDefault, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Client.AccountType SET IsDeleted=1 WHERE AccountTypeId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class RelationshipTypeRepository : IRelationshipTypeRepository
{
    private readonly ISqlConnectionFactory _cf;
    public RelationshipTypeRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "RelationshipTypeId, TenantId, TypeCode, TypeName, IsBidirectional, InverseTypeCode, Description, IsActive, SortOrder, CreatedDateUtc";

    public async Task<RelationshipTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<RelationshipTypeDto>(new CommandDefinition($"SELECT {Cols} FROM Client.RelationshipType WHERE RelationshipTypeId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<RelationshipTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Client.RelationshipType", Cols, "TypeName LIKE '%'+@SearchTerm+'%' OR TypeCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, TypeName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<RelationshipTypeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RelationshipTypeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateRelationshipTypeRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO Client.RelationshipType (RelationshipTypeId,TenantId,TypeCode,TypeName,IsBidirectional,InverseTypeCode,Description,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@TypeCode,@TypeName,@IsBidirectional,@InverseTypeCode,@Description,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.TypeCode, r.TypeName, r.IsBidirectional, r.InverseTypeCode, r.Description, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateRelationshipTypeRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE Client.RelationshipType SET TypeCode=@TypeCode,TypeName=@TypeName,IsBidirectional=@IsBidirectional,InverseTypeCode=@InverseTypeCode,Description=@Description,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE RelationshipTypeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TypeCode, r.TypeName, r.IsBidirectional, r.InverseTypeCode, r.Description, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Client.RelationshipType SET IsDeleted=1 WHERE RelationshipTypeId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class HouseholdSettingRepository : IHouseholdSettingRepository
{
    private readonly ISqlConnectionFactory _cf;
    public HouseholdSettingRepository(ISqlConnectionFactory cf) => _cf = cf;

    public async Task<List<HouseholdSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<HouseholdSettingDto>(new CommandDefinition("SELECT HouseholdSettingId,TenantId,SettingKey,SettingValue,SettingType,Description,CreatedDateUtc FROM Client.HouseholdSetting WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY SettingKey;", new { TenantId = tenantId }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task UpdateAsync(Guid id, UpdateHouseholdSettingRequest r, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Client.HouseholdSetting SET SettingValue=@SettingValue,ModifiedDateUtc=GETUTCDATE() WHERE HouseholdSettingId=@Id;", new { Id = id, r.SettingValue }, cancellationToken: ct));
    }
}

public sealed class CommercialEntitySettingRepository : ICommercialEntitySettingRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CommercialEntitySettingRepository(ISqlConnectionFactory cf) => _cf = cf;

    public async Task<List<CommercialEntitySettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<CommercialEntitySettingDto>(new CommandDefinition("SELECT CommercialEntitySettingId,TenantId,SettingKey,SettingValue,SettingType,Description,CreatedDateUtc FROM Client.CommercialEntitySetting WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY SettingKey;", new { TenantId = tenantId }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task UpdateAsync(Guid id, UpdateCommercialEntitySettingRequest r, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Client.CommercialEntitySetting SET SettingValue=@SettingValue,ModifiedDateUtc=GETUTCDATE() WHERE CommercialEntitySettingId=@Id;", new { Id = id, r.SettingValue }, cancellationToken: ct));
    }
}

public sealed class ContactTypeRepository : IContactTypeRepository
{
    private readonly ISqlConnectionFactory _cf;
    public ContactTypeRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "ContactTypeId, TenantId, TypeCode, TypeName, Description, IsDefault, IsActive, SortOrder, CreatedDateUtc";

    public async Task<ContactTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<ContactTypeDto>(new CommandDefinition($"SELECT {Cols} FROM Client.ContactType WHERE ContactTypeId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<ContactTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Client.ContactType", Cols, "TypeName LIKE '%'+@SearchTerm+'%' OR TypeCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, TypeName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<ContactTypeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ContactTypeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateContactTypeRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO Client.ContactType (ContactTypeId,TenantId,TypeCode,TypeName,Description,IsDefault,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@TypeCode,@TypeName,@Description,@IsDefault,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.TypeCode, r.TypeName, r.Description, r.IsDefault, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateContactTypeRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE Client.ContactType SET TypeCode=@TypeCode,TypeName=@TypeName,Description=@Description,IsDefault=@IsDefault,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE ContactTypeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TypeCode, r.TypeName, r.Description, r.IsDefault, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Client.ContactType SET IsDeleted=1 WHERE ContactTypeId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class AccountCustomFieldRepository : IAccountCustomFieldRepository
{
    private readonly ISqlConnectionFactory _cf;
    public AccountCustomFieldRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "CustomFieldId, TenantId, FieldCode, FieldName, EntityType, FieldType, DefaultValue, DropdownOptions, IsRequired, IsSearchable, IsActive, SortOrder, CreatedDateUtc";

    public async Task<AccountCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<AccountCustomFieldDto>(new CommandDefinition($"SELECT {Cols} FROM Client.AccountCustomField WHERE CustomFieldId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<AccountCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Client.AccountCustomField", Cols, "FieldName LIKE '%'+@SearchTerm+'%' OR FieldCode LIKE '%'+@SearchTerm+'%'", "EntityType ASC, SortOrder ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<AccountCustomFieldDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AccountCustomFieldDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateAccountCustomFieldRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO Client.AccountCustomField (CustomFieldId,TenantId,FieldCode,FieldName,EntityType,FieldType,DefaultValue,DropdownOptions,IsRequired,IsSearchable,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@FieldCode,@FieldName,@EntityType,@FieldType,@DefaultValue,@DropdownOptions,@IsRequired,@IsSearchable,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.FieldCode, r.FieldName, r.EntityType, r.FieldType, r.DefaultValue, r.DropdownOptions, r.IsRequired, r.IsSearchable, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateAccountCustomFieldRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE Client.AccountCustomField SET FieldCode=@FieldCode,FieldName=@FieldName,EntityType=@EntityType,FieldType=@FieldType,DefaultValue=@DefaultValue,DropdownOptions=@DropdownOptions,IsRequired=@IsRequired,IsSearchable=@IsSearchable,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE CustomFieldId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.FieldCode, r.FieldName, r.EntityType, r.FieldType, r.DefaultValue, r.DropdownOptions, r.IsRequired, r.IsSearchable, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Client.AccountCustomField SET IsDeleted=1 WHERE CustomFieldId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}
