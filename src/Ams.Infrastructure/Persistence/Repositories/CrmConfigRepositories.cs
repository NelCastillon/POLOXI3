using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CrmConfig;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class LeadSourceRepository : ILeadSourceRepository
{
    private readonly ISqlConnectionFactory _cf;
    public LeadSourceRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string Cols = "LeadSourceId, TenantId, SourceCode, SourceName, IsActive, CreatedDateUtc";

    public async Task<LeadSourceDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<LeadSourceDto>(new CommandDefinition(
            $"SELECT {Cols} FROM CRM.LeadSource WHERE LeadSourceId = @Id;",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<LeadSourceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("CRM.LeadSource", Cols,
            "SourceName LIKE '%' + @SearchTerm + '%' OR SourceCode LIKE '%' + @SearchTerm + '%'", "SourceName ASC", hasSoftDelete: false);
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: ct));
        var items = (await multi.ReadAsync<LeadSourceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<LeadSourceDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateLeadSourceRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO CRM.LeadSource (LeadSourceId,TenantId,SourceCode,SourceName,IsActive,CreatedDateUtc)
VALUES (@Id,@TenantId,@SourceCode,@SourceName,1,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.SourceCode, r.SourceName }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateLeadSourceRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE CRM.LeadSource SET SourceCode=@SourceCode,SourceName=@SourceName,IsActive=@IsActive WHERE LeadSourceId=@Id;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.SourceCode, r.SourceName, r.IsActive }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("DELETE FROM CRM.LeadSource WHERE LeadSourceId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class LeadStatusRepository : ILeadStatusRepository
{
    private readonly ISqlConnectionFactory _cf;
    public LeadStatusRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "LeadStatusId, TenantId, StatusCode, StatusName, StatusType, Description, ColorHex, IsDefault, IsActive, SortOrder, CreatedDateUtc";

    public async Task<LeadStatusDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<LeadStatusDto>(new CommandDefinition($"SELECT {Cols} FROM CRM.LeadStatus WHERE LeadStatusId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<LeadStatusDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("CRM.LeadStatus", Cols, "StatusName LIKE '%'+@SearchTerm+'%' OR StatusCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<LeadStatusDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<LeadStatusDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateLeadStatusRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO CRM.LeadStatus (LeadStatusId,TenantId,StatusCode,StatusName,StatusType,Description,ColorHex,IsDefault,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@StatusCode,@StatusName,@StatusType,@Description,@ColorHex,@IsDefault,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.StatusCode, r.StatusName, r.StatusType, r.Description, r.ColorHex, r.IsDefault, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateLeadStatusRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE CRM.LeadStatus SET StatusCode=@StatusCode,StatusName=@StatusName,StatusType=@StatusType,Description=@Description,ColorHex=@ColorHex,IsDefault=@IsDefault,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE LeadStatusId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.StatusCode, r.StatusName, r.StatusType, r.Description, r.ColorHex, r.IsDefault, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.LeadStatus SET IsDeleted=1 WHERE LeadStatusId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class OpportunityStageRepository : IOpportunityStageRepository
{
    private readonly ISqlConnectionFactory _cf;
    public OpportunityStageRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive";

    public async Task<OpportunityStageDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<OpportunityStageDto>(new CommandDefinition($"SELECT {Cols} FROM CRM.OpportunityStage WHERE OpportunityStageId=@Id;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<OpportunityStageDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("CRM.OpportunityStage", Cols, "StageName LIKE '%'+@SearchTerm+'%' OR StageCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC", hasSoftDelete: false);
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<OpportunityStageDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<OpportunityStageDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateOpportunityStageRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO CRM.OpportunityStage (OpportunityStageId,TenantId,StageCode,StageName,SortOrder,ProbabilityPercent,IsClosedStage,IsWonStage,IsActive) VALUES (@Id,@TenantId,@StageCode,@StageName,@SortOrder,@ProbabilityPercent,@IsClosedStage,@IsWonStage,1);";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.StageCode, r.StageName, r.SortOrder, r.ProbabilityPercent, r.IsClosedStage, r.IsWonStage }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateOpportunityStageRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE CRM.OpportunityStage SET StageCode=@StageCode,StageName=@StageName,SortOrder=@SortOrder,ProbabilityPercent=@ProbabilityPercent,IsClosedStage=@IsClosedStage,IsWonStage=@IsWonStage,IsActive=@IsActive WHERE OpportunityStageId=@Id;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.StageCode, r.StageName, r.SortOrder, r.ProbabilityPercent, r.IsClosedStage, r.IsWonStage, r.IsActive }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("DELETE FROM CRM.OpportunityStage WHERE OpportunityStageId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class OpportunityForecastCategoryRepository : IOpportunityForecastCategoryRepository
{
    private readonly ISqlConnectionFactory _cf;
    public OpportunityForecastCategoryRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "OpportunityForecastCategoryId, TenantId, CategoryCode, CategoryName, SortOrder, DefaultProbabilityPercent, IsClosedCategory, IsDefault, IsActive, CreatedDateUtc";

    public async Task<OpportunityForecastCategoryDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<OpportunityForecastCategoryDto>(new CommandDefinition($"SELECT {Cols} FROM CRM.OpportunityForecastCategory WHERE OpportunityForecastCategoryId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<OpportunityForecastCategoryDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("CRM.OpportunityForecastCategory", Cols, "CategoryName LIKE '%'+@SearchTerm+'%' OR CategoryCode LIKE '%'+@SearchTerm+'%'", "SortOrder ASC, CategoryName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<OpportunityForecastCategoryDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<OpportunityForecastCategoryDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateOpportunityForecastCategoryRequest r, CancellationToken ct = default)
    {
        const string sql = @"
IF @IsDefault = 1
    UPDATE CRM.OpportunityForecastCategory SET IsDefault = 0, ModifiedDateUtc = SYSUTCDATETIME() WHERE TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO CRM.OpportunityForecastCategory (OpportunityForecastCategoryId,TenantId,CategoryCode,CategoryName,SortOrder,DefaultProbabilityPercent,IsClosedCategory,IsDefault,IsActive,IsDeleted,CreatedDateUtc)
VALUES (@Id,@TenantId,@CategoryCode,@CategoryName,@SortOrder,@DefaultProbabilityPercent,@IsClosedCategory,@IsDefault,1,0,SYSUTCDATETIME());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.CategoryCode, r.CategoryName, r.SortOrder, r.DefaultProbabilityPercent, r.IsClosedCategory, r.IsDefault }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateOpportunityForecastCategoryRequest r, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM CRM.OpportunityForecastCategory WHERE OpportunityForecastCategoryId = @Id AND IsDeleted = 0);
IF @IsDefault = 1 AND @TenantId IS NOT NULL
    UPDATE CRM.OpportunityForecastCategory SET IsDefault = 0, ModifiedDateUtc = SYSUTCDATETIME() WHERE TenantId = @TenantId AND OpportunityForecastCategoryId <> @Id AND IsDeleted = 0;

UPDATE CRM.OpportunityForecastCategory
SET CategoryCode=@CategoryCode,CategoryName=@CategoryName,SortOrder=@SortOrder,DefaultProbabilityPercent=@DefaultProbabilityPercent,IsClosedCategory=@IsClosedCategory,IsDefault=@IsDefault,IsActive=@IsActive,ModifiedDateUtc=SYSUTCDATETIME()
WHERE OpportunityForecastCategoryId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.CategoryCode, r.CategoryName, r.SortOrder, r.DefaultProbabilityPercent, r.IsClosedCategory, r.IsDefault, r.IsActive }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.OpportunityForecastCategory SET IsDeleted=1, IsActive=0, ModifiedDateUtc=SYSUTCDATETIME() WHERE OpportunityForecastCategoryId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class PipelineSettingRepository : IPipelineSettingRepository
{
    private readonly ISqlConnectionFactory _cf;
    public PipelineSettingRepository(ISqlConnectionFactory cf) => _cf = cf;

    public async Task<List<PipelineSettingDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<PipelineSettingDto>(new CommandDefinition("SELECT PipelineSettingId,TenantId,SettingKey,SettingValue,SettingType,Category,Description,CreatedDateUtc FROM CRM.PipelineSetting WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY Category,SettingKey;", new { TenantId = tenantId }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task UpdateAsync(Guid id, UpdatePipelineSettingRequest r, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.PipelineSetting SET SettingValue=@SettingValue,ModifiedDateUtc=GETUTCDATE() WHERE PipelineSettingId=@Id;", new { Id = id, r.SettingValue }, cancellationToken: ct));
    }
}

public sealed class DuplicateRuleRepository : IDuplicateRuleRepository
{
    private readonly ISqlConnectionFactory _cf;
    public DuplicateRuleRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "DuplicateRuleId, TenantId, RuleName, EntityType, MatchFields, MatchThreshold, ActionOnMatch, Description, IsActive, CreatedDateUtc";

    public async Task<DuplicateRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<DuplicateRuleDto>(new CommandDefinition($"SELECT {Cols} FROM CRM.DuplicateRule WHERE DuplicateRuleId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<DuplicateRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("CRM.DuplicateRule", Cols, "RuleName LIKE '%'+@SearchTerm+'%' OR EntityType LIKE '%'+@SearchTerm+'%'", "EntityType ASC, RuleName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<DuplicateRuleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<DuplicateRuleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateDuplicateRuleRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO CRM.DuplicateRule (DuplicateRuleId,TenantId,RuleName,EntityType,MatchFields,MatchThreshold,ActionOnMatch,Description,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@RuleName,@EntityType,@MatchFields,@MatchThreshold,@ActionOnMatch,@Description,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.RuleName, r.EntityType, r.MatchFields, r.MatchThreshold, r.ActionOnMatch, r.Description }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateDuplicateRuleRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE CRM.DuplicateRule SET RuleName=@RuleName,EntityType=@EntityType,MatchFields=@MatchFields,MatchThreshold=@MatchThreshold,ActionOnMatch=@ActionOnMatch,Description=@Description,IsActive=@IsActive,ModifiedDateUtc=GETUTCDATE() WHERE DuplicateRuleId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.RuleName, r.EntityType, r.MatchFields, r.MatchThreshold, r.ActionOnMatch, r.Description, r.IsActive }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.DuplicateRule SET IsDeleted=1 WHERE DuplicateRuleId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class AssignmentRuleRepository : IAssignmentRuleRepository
{
    private readonly ISqlConnectionFactory _cf;
    public AssignmentRuleRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "AssignmentRuleId, TenantId, RuleName, EntityType, AssignmentMethod, Criteria, AssignToUserId, AssignToTeam, Priority, Description, IsActive, CreatedDateUtc";

    public async Task<AssignmentRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<AssignmentRuleDto>(new CommandDefinition($"SELECT {Cols} FROM CRM.AssignmentRule WHERE AssignmentRuleId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<AssignmentRuleDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("CRM.AssignmentRule", Cols, "RuleName LIKE '%'+@SearchTerm+'%' OR EntityType LIKE '%'+@SearchTerm+'%'", "Priority ASC, RuleName ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<AssignmentRuleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AssignmentRuleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateAssignmentRuleRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO CRM.AssignmentRule (AssignmentRuleId,TenantId,RuleName,EntityType,AssignmentMethod,Criteria,AssignToUserId,AssignToTeam,Priority,Description,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@RuleName,@EntityType,@AssignmentMethod,@Criteria,@AssignToUserId,@AssignToTeam,@Priority,@Description,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.RuleName, r.EntityType, r.AssignmentMethod, r.Criteria, r.AssignToUserId, r.AssignToTeam, r.Priority, r.Description }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateAssignmentRuleRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE CRM.AssignmentRule SET RuleName=@RuleName,EntityType=@EntityType,AssignmentMethod=@AssignmentMethod,Criteria=@Criteria,AssignToUserId=@AssignToUserId,AssignToTeam=@AssignToTeam,Priority=@Priority,Description=@Description,IsActive=@IsActive,ModifiedDateUtc=GETUTCDATE() WHERE AssignmentRuleId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.RuleName, r.EntityType, r.AssignmentMethod, r.Criteria, r.AssignToUserId, r.AssignToTeam, r.Priority, r.Description, r.IsActive }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.AssignmentRule SET IsDeleted=1 WHERE AssignmentRuleId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}

public sealed class LeadActivityOutcomeRepository : ILeadActivityOutcomeRepository
{
    private readonly ISqlConnectionFactory _cf;
    public LeadActivityOutcomeRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "ActivityOutcomeId, TenantId, ActivityTypeCode, OutcomeCode, OutcomeName, Description, SortOrder, IsActive";

    public async Task<LeadActivityOutcomeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<LeadActivityOutcomeDto>(new CommandDefinition($"SELECT {Cols} FROM CRM.LeadActivityOutcome WHERE ActivityOutcomeId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<LeadActivityOutcomeDto>> SearchAsync(Guid tenantId, string? searchTerm, string? activityTypeCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        const string sql = @"
SELECT ActivityOutcomeId, TenantId, ActivityTypeCode, OutcomeCode, OutcomeName, Description, SortOrder, IsActive
FROM CRM.LeadActivityOutcome
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@ActivityTypeCode IS NULL OR @ActivityTypeCode = '' OR ActivityTypeCode = @ActivityTypeCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR OutcomeName LIKE '%' + @SearchTerm + '%' OR OutcomeCode LIKE '%' + @SearchTerm + '%' OR ActivityTypeCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%')
ORDER BY ActivityTypeCode ASC, SortOrder ASC, OutcomeName ASC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM CRM.LeadActivityOutcome
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@ActivityTypeCode IS NULL OR @ActivityTypeCode = '' OR ActivityTypeCode = @ActivityTypeCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR OutcomeName LIKE '%' + @SearchTerm + '%' OR OutcomeCode LIKE '%' + @SearchTerm + '%' OR ActivityTypeCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%');";

        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm ?? string.Empty,
            ActivityTypeCode = activityTypeCode ?? string.Empty,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: ct));
        var items = (await multi.ReadAsync<LeadActivityOutcomeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<LeadActivityOutcomeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateLeadActivityOutcomeRequest r, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO CRM.LeadActivityOutcome (ActivityOutcomeId,TenantId,ActivityTypeCode,OutcomeCode,OutcomeName,Description,SortOrder,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES (@Id,@TenantId,@ActivityTypeCode,@OutcomeCode,@OutcomeName,@Description,@SortOrder,1,SYSUTCDATETIME(),@CreatedByUserId,0);";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, ActivityTypeCode = r.ActivityTypeCode.Trim(), OutcomeCode = r.OutcomeCode.Trim(), OutcomeName = r.OutcomeName.Trim(), Description = TrimOrNull(r.Description), r.SortOrder, r.CreatedByUserId }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateLeadActivityOutcomeRequest r, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE CRM.LeadActivityOutcome
SET ActivityTypeCode=@ActivityTypeCode,
    OutcomeCode=@OutcomeCode,
    OutcomeName=@OutcomeName,
    Description=@Description,
    SortOrder=@SortOrder,
    IsActive=@IsActive,
    IsDeleted=0,
    ModifiedDateUtc=SYSUTCDATETIME(),
    ModifiedByUserId=@ModifiedByUserId
WHERE ActivityOutcomeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ActivityTypeCode = r.ActivityTypeCode.Trim(), OutcomeCode = r.OutcomeCode.Trim(), OutcomeName = r.OutcomeName.Trim(), Description = TrimOrNull(r.Description), r.SortOrder, r.IsActive, r.ModifiedByUserId }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE CRM.LeadActivityOutcome
SET IsDeleted=1,
    IsActive=0,
    ModifiedDateUtc=SYSUTCDATETIME(),
    ModifiedByUserId=@ModifiedByUserId
WHERE ActivityOutcomeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: ct));
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class LeadActivityTypeRepository : ILeadActivityTypeRepository
{
    private readonly ISqlConnectionFactory _cf;
    public LeadActivityTypeRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "ActivityTypeId, TenantId, ActivityTypeCode, ActivityTypeName, IconCssClass, Description, SortOrder, IsActive, CreatedDateUtc";

    public async Task<LeadActivityTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<LeadActivityTypeDto>(new CommandDefinition($"SELECT {Cols} FROM CRM.LeadActivityType WHERE ActivityTypeId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<LeadActivityTypeDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        const string sql = @"
SELECT ActivityTypeId, TenantId, ActivityTypeCode, ActivityTypeName, IconCssClass, Description, SortOrder, IsActive, CreatedDateUtc
FROM CRM.LeadActivityType
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR ActivityTypeName LIKE '%' + @SearchTerm + '%' OR ActivityTypeCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%')
ORDER BY SortOrder ASC, ActivityTypeName ASC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM CRM.LeadActivityType
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR ActivityTypeName LIKE '%' + @SearchTerm + '%' OR ActivityTypeCode LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%');";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm ?? string.Empty,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: ct));
        var items = (await multi.ReadAsync<LeadActivityTypeDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<LeadActivityTypeDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateLeadActivityTypeRequest r, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO CRM.LeadActivityType (ActivityTypeId,TenantId,ActivityTypeCode,ActivityTypeName,IconCssClass,Description,SortOrder,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES (@Id,@TenantId,@ActivityTypeCode,@ActivityTypeName,@IconCssClass,@Description,@SortOrder,1,SYSUTCDATETIME(),@CreatedByUserId,0);";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, ActivityTypeCode = r.ActivityTypeCode.Trim(), ActivityTypeName = r.ActivityTypeName.Trim(), IconCssClass = TrimOrNull(r.IconCssClass), Description = TrimOrNull(r.Description), r.SortOrder, r.CreatedByUserId }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateLeadActivityTypeRequest r, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE CRM.LeadActivityType
SET ActivityTypeCode=@ActivityTypeCode,
    ActivityTypeName=@ActivityTypeName,
    IconCssClass=@IconCssClass,
    Description=@Description,
    SortOrder=@SortOrder,
    IsActive=@IsActive,
    IsDeleted=0,
    ModifiedDateUtc=SYSUTCDATETIME(),
    ModifiedByUserId=@ModifiedByUserId
WHERE ActivityTypeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ActivityTypeCode = r.ActivityTypeCode.Trim(), ActivityTypeName = r.ActivityTypeName.Trim(), IconCssClass = TrimOrNull(r.IconCssClass), Description = TrimOrNull(r.Description), r.SortOrder, r.IsActive, r.ModifiedByUserId }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE CRM.LeadActivityType
SET IsDeleted=1,
    IsActive=0,
    ModifiedDateUtc=SYSUTCDATETIME(),
    ModifiedByUserId=@ModifiedByUserId
WHERE ActivityTypeId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: ct));
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CrmCustomFieldRepository : ICrmCustomFieldRepository
{
    private readonly ISqlConnectionFactory _cf;
    public CrmCustomFieldRepository(ISqlConnectionFactory cf) => _cf = cf;
    private const string Cols = "CustomFieldId, TenantId, FieldCode, FieldName, EntityType, FieldType, DefaultValue, DropdownOptions, IsRequired, IsSearchable, IsActive, SortOrder, CreatedDateUtc";

    public async Task<CrmCustomFieldDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<CrmCustomFieldDto>(new CommandDefinition($"SELECT {Cols} FROM CRM.CrmCustomField WHERE CustomFieldId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<CrmCustomFieldDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("CRM.CrmCustomField", Cols, "FieldName LIKE '%'+@SearchTerm+'%' OR FieldCode LIKE '%'+@SearchTerm+'%'", "EntityType ASC, SortOrder ASC");
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? "", Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        var items = (await multi.ReadAsync<CrmCustomFieldDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<CrmCustomFieldDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateCrmCustomFieldRequest r, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO CRM.CrmCustomField (CustomFieldId,TenantId,FieldCode,FieldName,EntityType,FieldType,DefaultValue,DropdownOptions,IsRequired,IsSearchable,SortOrder,IsActive,IsDeleted,CreatedDateUtc) VALUES (@Id,@TenantId,@FieldCode,@FieldName,@EntityType,@FieldType,@DefaultValue,@DropdownOptions,@IsRequired,@IsSearchable,@SortOrder,1,0,GETUTCDATE());";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.FieldCode, r.FieldName, r.EntityType, r.FieldType, r.DefaultValue, r.DropdownOptions, r.IsRequired, r.IsSearchable, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateCrmCustomFieldRequest r, CancellationToken ct = default)
    {
        const string sql = @"UPDATE CRM.CrmCustomField SET FieldCode=@FieldCode,FieldName=@FieldName,EntityType=@EntityType,FieldType=@FieldType,DefaultValue=@DefaultValue,DropdownOptions=@DropdownOptions,IsRequired=@IsRequired,IsSearchable=@IsSearchable,IsActive=@IsActive,SortOrder=@SortOrder,ModifiedDateUtc=GETUTCDATE() WHERE CustomFieldId=@Id AND IsDeleted=0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.FieldCode, r.FieldName, r.EntityType, r.FieldType, r.DefaultValue, r.DropdownOptions, r.IsRequired, r.IsSearchable, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE CRM.CrmCustomField SET IsDeleted=1 WHERE CustomFieldId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}
