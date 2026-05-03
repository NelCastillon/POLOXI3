using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PolicyConfig;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CoverageTypeRepository : ICoverageTypeRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CoverageTypeRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "CoverageTypeId, TenantId, CoverageCode, CoverageName, LobCode, Description, IsActive, SortOrder, CreatedDateUtc";

    public async Task<CoverageTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<CoverageTypeDto>(new CommandDefinition($"SELECT {Cols} FROM Policy.CoverageType WHERE CoverageTypeId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<CoverageTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Policy.CoverageType", Cols, "CoverageName LIKE '%'+@SearchTerm+'%' OR CoverageCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, CoverageName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<CoverageTypeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CoverageTypeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCoverageTypeRequest r, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO Policy.CoverageType (CoverageTypeId,TenantId,CoverageCode,CoverageName,LobCode,Description,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@CoverageCode,@CoverageName,@LobCode,@Description,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.CoverageCode, r.CoverageName, r.LobCode, r.Description, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCoverageTypeRequest r, CancellationToken ct = default)
    {
        const string sql = "UPDATE Policy.CoverageType SET CoverageCode=@CoverageCode,CoverageName=@CoverageName,LobCode=@LobCode,Description=@Description,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE CoverageTypeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.CoverageCode, r.CoverageName, r.LobCode, r.Description, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Policy.CoverageType SET IsDeleted=1 WHERE CoverageTypeId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class PolicyStatusRepository : IPolicyStatusRepository
{
    private readonly ISqlConnectionFactory _cf;
    public PolicyStatusRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "PolicyStatusId, TenantId, StatusCode, StatusName, StatusType, Description, ColorHex, IsDefault, IsActive, SortOrder, CreatedDateUtc";

    public async Task<PolicyStatusDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<PolicyStatusDto>(new CommandDefinition($"SELECT {Cols} FROM Policy.PolicyStatus WHERE PolicyStatusId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<PolicyStatusDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Policy.PolicyStatus", Cols, "StatusName LIKE '%'+@SearchTerm+'%' OR StatusCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, StatusName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<PolicyStatusDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PolicyStatusDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreatePolicyStatusRequest r, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO Policy.PolicyStatus (PolicyStatusId,TenantId,StatusCode,StatusName,StatusType,Description,ColorHex,IsDefault,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@StatusCode,@StatusName,@StatusType,@Description,@ColorHex,@IsDefault,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.StatusCode, r.StatusName, r.StatusType, r.Description, r.ColorHex, r.IsDefault, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdatePolicyStatusRequest r, CancellationToken ct = default)
    {
        const string sql = "UPDATE Policy.PolicyStatus SET StatusCode=@StatusCode,StatusName=@StatusName,StatusType=@StatusType,Description=@Description,ColorHex=@ColorHex,IsDefault=@IsDefault,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE PolicyStatusId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.StatusCode, r.StatusName, r.StatusType, r.Description, r.ColorHex, r.IsDefault, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Policy.PolicyStatus SET IsDeleted=1 WHERE PolicyStatusId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class EndorsementTypeRepository : IEndorsementTypeRepository
{
    private readonly ISqlConnectionFactory _cf;
    public EndorsementTypeRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "EndorsementTypeId, TenantId, TypeCode, TypeName, Description, IsActive, SortOrder, CreatedDateUtc";

    public async Task<EndorsementTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<EndorsementTypeDto>(new CommandDefinition($"SELECT {Cols} FROM Policy.EndorsementType WHERE EndorsementTypeId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<EndorsementTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Policy.EndorsementType", Cols, "TypeName LIKE '%'+@SearchTerm+'%' OR TypeCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, TypeName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<EndorsementTypeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<EndorsementTypeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateEndorsementTypeRequest r, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO Policy.EndorsementType (EndorsementTypeId,TenantId,TypeCode,TypeName,Description,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@TypeCode,@TypeName,@Description,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.TypeCode, r.TypeName, r.Description, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateEndorsementTypeRequest r, CancellationToken ct = default)
    {
        const string sql = "UPDATE Policy.EndorsementType SET TypeCode=@TypeCode,TypeName=@TypeName,Description=@Description,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE EndorsementTypeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TypeCode, r.TypeName, r.Description, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Policy.EndorsementType SET IsDeleted=1 WHERE EndorsementTypeId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class CancellationReasonRepository : ICancellationReasonRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CancellationReasonRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "CancellationReasonId, TenantId, ReasonCode, ReasonName, ReasonType, Description, IsActive, SortOrder, CreatedDateUtc";

    public async Task<CancellationReasonDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<CancellationReasonDto>(new CommandDefinition($"SELECT {Cols} FROM Policy.CancellationReason WHERE CancellationReasonId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<CancellationReasonDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Policy.CancellationReason", Cols, "ReasonName LIKE '%'+@SearchTerm+'%' OR ReasonCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, ReasonName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<CancellationReasonDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CancellationReasonDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCancellationReasonRequest r, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO Policy.CancellationReason (CancellationReasonId,TenantId,ReasonCode,ReasonName,ReasonType,Description,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@ReasonCode,@ReasonName,@ReasonType,@Description,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.ReasonCode, r.ReasonName, r.ReasonType, r.Description, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCancellationReasonRequest r, CancellationToken ct = default)
    {
        const string sql = "UPDATE Policy.CancellationReason SET ReasonCode=@ReasonCode,ReasonName=@ReasonName,ReasonType=@ReasonType,Description=@Description,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE CancellationReasonId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.ReasonCode, r.ReasonName, r.ReasonType, r.Description, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Policy.CancellationReason SET IsDeleted=1 WHERE CancellationReasonId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class CertificateSettingRepository : ICertificateSettingRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CertificateSettingRepository(ISqlConnectionFactory cf) => _cf = cf;

    public async Task<List<CertificateSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<CertificateSettingDto>(new CommandDefinition("SELECT CertificateSettingId,TenantId,SettingKey,SettingValue,SettingType,Description,CreatedDateUtc FROM Policy.CertificateSetting WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY SettingKey;", new { TenantId = tenantId }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task UpdateAsync(Guid id, UpdateCertificateSettingRequest r, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Policy.CertificateSetting SET SettingValue=@SettingValue,ModifiedDateUtc=GETUTCDATE() WHERE CertificateSettingId=@Id;", new { Id = id, r.SettingValue }, cancellationToken: ct));
    }
}

public sealed class IdCardSettingRepository : IIdCardSettingRepository
{
    private readonly ISqlConnectionFactory _cf;
    public IdCardSettingRepository(ISqlConnectionFactory cf) => _cf = cf;

    public async Task<List<IdCardSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<IdCardSettingDto>(new CommandDefinition("SELECT IdCardSettingId,TenantId,SettingKey,SettingValue,SettingType,Description,CreatedDateUtc FROM Policy.IdCardSetting WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY SettingKey;", new { TenantId = tenantId }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task UpdateAsync(Guid id, UpdateIdCardSettingRequest r, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Policy.IdCardSetting SET SettingValue=@SettingValue,ModifiedDateUtc=GETUTCDATE() WHERE IdCardSettingId=@Id;", new { Id = id, r.SettingValue }, cancellationToken: ct));
    }
}

public sealed class PolicyCustomFieldRepository : IPolicyCustomFieldRepository
{
    private readonly ISqlConnectionFactory _cf;
    public PolicyCustomFieldRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "CustomFieldId, TenantId, FieldCode, FieldName, EntityType, FieldType, DefaultValue, DropdownOptions, IsRequired, IsSearchable, IsActive, SortOrder, CreatedDateUtc";

    public async Task<PolicyCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<PolicyCustomFieldDto>(new CommandDefinition($"SELECT {Cols} FROM Policy.PolicyCustomField WHERE CustomFieldId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<PolicyCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Policy.PolicyCustomField", Cols, "FieldName LIKE '%'+@SearchTerm+'%' OR FieldCode LIKE '%'+@SearchTerm+'%'", "EntityType ASC, SortOrder ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<PolicyCustomFieldDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PolicyCustomFieldDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreatePolicyCustomFieldRequest r, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO Policy.PolicyCustomField (CustomFieldId,TenantId,FieldCode,FieldName,EntityType,FieldType,DefaultValue,DropdownOptions,IsRequired,IsSearchable,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@FieldCode,@FieldName,@EntityType,@FieldType,@DefaultValue,@DropdownOptions,@IsRequired,@IsSearchable,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.FieldCode, r.FieldName, r.EntityType, r.FieldType, r.DefaultValue, r.DropdownOptions, r.IsRequired, r.IsSearchable, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdatePolicyCustomFieldRequest r, CancellationToken ct = default)
    {
        const string sql = "UPDATE Policy.PolicyCustomField SET FieldCode=@FieldCode,FieldName=@FieldName,EntityType=@EntityType,FieldType=@FieldType,DefaultValue=@DefaultValue,DropdownOptions=@DropdownOptions,IsRequired=@IsRequired,IsSearchable=@IsSearchable,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE CustomFieldId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.FieldCode, r.FieldName, r.EntityType, r.FieldType, r.DefaultValue, r.DropdownOptions, r.IsRequired, r.IsSearchable, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE Policy.PolicyCustomField SET IsDeleted=1 WHERE CustomFieldId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}
