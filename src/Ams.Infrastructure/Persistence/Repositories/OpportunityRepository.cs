using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Opportunities;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class OpportunityRepository : IOpportunityRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private const string OpportunitySelect = @"
SELECT o.OpportunityId, o.TenantId, o.OpportunityNumber, o.AccountId,
       a.AccountName, o.OpportunityName, o.EstimatedAmount,
       o.StatusCodeId AS StatusCode, o.OwnerUserId,
       o.CloseDate, o.WinProbability, o.ForecastCategoryCode, o.LeadId,
       COALESCE(s.StageName, o.StageName, o.ForecastCategoryCode, N'Qualification') AS StageName,
       o.Description, o.CreatedDateUtc, o.ModifiedDateUtc,
       COALESCE(NULLIF(LTRIM(RTRIM(u.FullName)), N''), NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, N' ', u.LastName))), N''), u.UserName, u.Email) AS OwnerName,
       l.LeadNumber AS SourceLead
FROM CRM.Opportunity o
LEFT JOIN Client.Account a ON a.AccountId = o.AccountId
LEFT JOIN CRM.OpportunityStage s ON s.OpportunityStageId = o.OpportunityStageId
LEFT JOIN IAM.[User] u ON u.UserId = o.OwnerUserId
LEFT JOIN CRM.Lead l ON l.LeadId = o.LeadId";

    public OpportunityRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @OpportunityStageId UNIQUEIDENTIFIER =
(
    SELECT TOP 1 OpportunityStageId
    FROM CRM.OpportunityStage
    WHERE TenantId = @TenantId
      AND IsActive = 1
      AND (
          StageName IN (N'Qualification', N'Qualify', N'Prospect')
          OR StageCode IN (N'QUALIFICATION', N'QUALIFY', N'PROSPECT')
      )
    ORDER BY SortOrder, StageName
);

IF @OpportunityStageId IS NULL
    SELECT TOP 1 @OpportunityStageId = OpportunityStageId
    FROM CRM.OpportunityStage
    WHERE TenantId = @TenantId AND IsActive = 1
    ORDER BY SortOrder, StageName;

INSERT INTO CRM.Opportunity
(
    OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName,
    EstimatedAmount, OwnerUserId, CloseDate, LeadId, WinProbability,
    ForecastCategoryCode, StageName, OpportunityStageId, StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @OpportunityId, @TenantId, @OpportunityNumber, @AccountId, @OpportunityName,
    @EstimatedAmount, @OwnerUserId, @CloseDate, @LeadId, @WinProbability,
    @ForecastCategoryCode, COALESCE((SELECT StageName FROM CRM.OpportunityStage WHERE OpportunityStageId = @OpportunityStageId), N'Qualification'), @OpportunityStageId, 1, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            OpportunityId = id,
            request.TenantId,
            request.OpportunityNumber,
            request.AccountId,
            request.OpportunityName,
            request.EstimatedAmount,
            request.OwnerUserId,
            request.CloseDate,
            request.LeadId,
            request.WinProbability,
            request.ForecastCategoryCode,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<OpportunityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = OpportunitySelect + "\nWHERE o.OpportunityId = @Id AND o.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<OpportunityDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<OpportunityDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = OpportunitySelect + @"
WHERE o.OpportunityId = @Id AND o.IsDeleted = 0;

SELECT OpportunityLineId, TenantId, OpportunityId, LineOfBusiness, Carrier, EstPremium, Priority, CreatedDateUtc, ModifiedDateUtc
FROM CRM.OpportunityLine
WHERE OpportunityId = @Id AND IsDeleted = 0
ORDER BY CreatedDateUtc;

SELECT a.ActivityId, a.TenantId, a.OpportunityId, a.ActivityTypeCode, a.Subject, a.Notes, a.ActivityDate, a.CreatedByUserId,
       COALESCE(NULLIF(LTRIM(RTRIM(u.FullName)), N''), NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, N' ', u.LastName))), N''), u.UserName, u.Email) AS CreatedByName,
       a.CreatedDateUtc, a.ModifiedDateUtc
FROM CRM.OpportunityActivity a
LEFT JOIN IAM.[User] u ON u.UserId = a.CreatedByUserId
WHERE a.OpportunityId = @Id AND a.IsDeleted = 0
ORDER BY a.ActivityDate DESC;

SELECT s.SubmissionId, s.TenantId, s.OpportunityId, s.SubmissionNumber, s.LineOfBusiness, s.Status, s.TargetPremium,
       (SELECT COUNT(1) FROM CRM.Quote q WHERE q.OpportunityId = s.OpportunityId AND q.IsDeleted = 0) AS QuoteCount,
       s.CreatedDateUtc, s.ModifiedDateUtc
FROM CRM.OpportunitySubmission s
WHERE s.OpportunityId = @Id AND s.IsDeleted = 0
ORDER BY s.CreatedDateUtc DESC;

SELECT QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, CreatedDateUtc, ModifiedDateUtc
FROM CRM.Quote
WHERE OpportunityId = @Id AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;

SELECT CompetitorId, TenantId, OpportunityId, Name, Strength, CreatedDateUtc, ModifiedDateUtc
FROM CRM.OpportunityCompetitor
WHERE OpportunityId = @Id AND IsDeleted = 0
ORDER BY CreatedDateUtc;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        var opportunity = await multi.ReadSingleOrDefaultAsync<OpportunityDto>();
        if (opportunity is null) return null;

        return new OpportunityDetailDto
        {
            Opportunity = opportunity,
            Lines = (await multi.ReadAsync<OpportunityLineDto>()).AsList(),
            Activities = (await multi.ReadAsync<OpportunityActivityDto>()).AsList(),
            Submissions = (await multi.ReadAsync<OpportunitySubmissionDto>()).AsList(),
            Quotes = (await multi.ReadAsync<QuoteDto>()).AsList(),
            Competitors = (await multi.ReadAsync<OpportunityCompetitorDto>()).AsList()
        };
    }

    public async Task<PagedResult<OpportunityDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT o.OpportunityId, o.TenantId, o.OpportunityNumber, o.AccountId,
           a.AccountName, o.OpportunityName, o.EstimatedAmount,
           o.StatusCodeId AS StatusCode, o.OwnerUserId,
            o.CloseDate, o.WinProbability, o.ForecastCategoryCode, o.LeadId,
            COALESCE(s.StageName, o.StageName, o.ForecastCategoryCode, N'Qualification') AS StageName,
            o.Description, o.CreatedDateUtc, o.ModifiedDateUtc,
            COALESCE(NULLIF(LTRIM(RTRIM(u.FullName)), N''), NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, N' ', u.LastName))), N''), u.UserName, u.Email) AS OwnerName,
            l.LeadNumber AS SourceLead
    FROM CRM.Opportunity o
    LEFT JOIN Client.Account a ON a.AccountId = o.AccountId
    LEFT JOIN CRM.OpportunityStage s ON s.OpportunityStageId = o.OpportunityStageId
    LEFT JOIN IAM.[User] u ON u.UserId = o.OwnerUserId
    LEFT JOIN CRM.Lead l ON l.LeadId = o.LeadId
    WHERE o.TenantId = @TenantId AND o.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR o.OpportunityName LIKE '%' + @SearchTerm + '%'
           OR o.OpportunityNumber LIKE '%' + @SearchTerm + '%'
           OR a.AccountName LIKE '%' + @SearchTerm + '%'
      )
)
SELECT * FROM Paged ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM CRM.Opportunity o
WHERE o.TenantId = @TenantId AND o.IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR o.OpportunityName LIKE '%' + @SearchTerm + '%'
       OR o.OpportunityNumber LIKE '%' + @SearchTerm + '%'
  );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<OpportunityDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<OpportunityDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task UpdateAsync(Guid id, UpdateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.Opportunity
SET OpportunityName = @OpportunityName,
    EstimatedAmount = @EstimatedAmount,
    CloseDate = @CloseDate,
    WinProbability = @WinProbability,
    ForecastCategoryCode = @ForecastCategoryCode,
    StageName = @StageName,
    OwnerUserId = @OwnerUserId,
    Description = @Description,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE OpportunityId = @OpportunityId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { OpportunityId = id, request.OpportunityName, request.EstimatedAmount, request.CloseDate, request.WinProbability, request.ForecastCategoryCode, request.StageName, request.OwnerUserId, request.Description, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStageAsync(Guid id, UpdateOpportunityStageRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.Opportunity
SET StageName = @Stage,
    ForecastCategoryCode = CASE WHEN @Stage IN (N'Closed Won', N'Closed Lost') THEN N'Closed' ELSE ForecastCategoryCode END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE OpportunityId = @OpportunityId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { OpportunityId = id, request.Stage, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> UpsertActivityAsync(UpsertOpportunityActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF @ActivityId IS NULL OR NOT EXISTS (SELECT 1 FROM CRM.OpportunityActivity WHERE ActivityId = @ActivityId AND IsDeleted = 0)
BEGIN
    SET @ActivityId = NEWID();
    INSERT INTO CRM.OpportunityActivity (ActivityId, TenantId, OpportunityId, ActivityTypeCode, Subject, Notes, ActivityDate, CreatedByUserId, CreatedDateUtc, IsDeleted)
    VALUES (@ActivityId, @TenantId, @OpportunityId, @ActivityTypeCode, @Subject, @Notes, @ActivityDate, @UserId, SYSUTCDATETIME(), 0);
END
ELSE
BEGIN
    UPDATE CRM.OpportunityActivity
    SET ActivityTypeCode = @ActivityTypeCode,
        Subject = @Subject,
        Notes = @Notes,
        ActivityDate = @ActivityDate,
        ModifiedByUserId = @UserId,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE ActivityId = @ActivityId AND IsDeleted = 0;
END
SELECT @ActivityId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.ActivityId, request.TenantId, request.OpportunityId, request.ActivityTypeCode, request.Subject, request.Notes, request.ActivityDate, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteActivityAsync(Guid activityId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.OpportunityActivity SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE ActivityId = @ActivityId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ActivityId = activityId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> UpsertSubmissionAsync(UpsertOpportunitySubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @Number NVARCHAR(50) = COALESCE(NULLIF(@SubmissionNumber, N''), CONCAT(N'SUB-', FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmmss')));
IF @SubmissionId IS NULL OR NOT EXISTS (SELECT 1 FROM CRM.OpportunitySubmission WHERE SubmissionId = @SubmissionId AND IsDeleted = 0)
BEGIN
    SET @SubmissionId = NEWID();
    INSERT INTO CRM.OpportunitySubmission (SubmissionId, TenantId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, TargetPremium, CreatedByUserId, CreatedDateUtc, IsDeleted)
    VALUES (@SubmissionId, @TenantId, @OpportunityId, @Number, @LineOfBusiness, @Status, @TargetPremium, @UserId, SYSUTCDATETIME(), 0);
END
ELSE
BEGIN
    UPDATE CRM.OpportunitySubmission
    SET LineOfBusiness = @LineOfBusiness,
        Status = @Status,
        TargetPremium = @TargetPremium,
        ModifiedByUserId = @UserId,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;
END
SELECT @SubmissionId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.SubmissionId, request.TenantId, request.OpportunityId, request.SubmissionNumber, request.LineOfBusiness, request.Status, request.TargetPremium, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteSubmissionAsync(Guid submissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.OpportunitySubmission SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE SubmissionId = @SubmissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> UpsertCompetitorAsync(UpsertOpportunityCompetitorRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF @CompetitorId IS NULL OR NOT EXISTS (SELECT 1 FROM CRM.OpportunityCompetitor WHERE CompetitorId = @CompetitorId AND IsDeleted = 0)
BEGIN
    SET @CompetitorId = NEWID();
    INSERT INTO CRM.OpportunityCompetitor (CompetitorId, TenantId, OpportunityId, Name, Strength, CreatedByUserId, CreatedDateUtc, IsDeleted)
    VALUES (@CompetitorId, @TenantId, @OpportunityId, @Name, @Strength, @UserId, SYSUTCDATETIME(), 0);
END
ELSE
BEGIN
    UPDATE CRM.OpportunityCompetitor
    SET Name = @Name,
        Strength = @Strength,
        ModifiedByUserId = @UserId,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE CompetitorId = @CompetitorId AND IsDeleted = 0;
END
SELECT @CompetitorId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.CompetitorId, request.TenantId, request.OpportunityId, request.Name, request.Strength, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteCompetitorAsync(Guid competitorId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.OpportunityCompetitor SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE CompetitorId = @CompetitorId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CompetitorId = competitorId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
