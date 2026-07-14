using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Opportunities;
using Dapper;
using System.Data;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class OpportunityRepository : IOpportunityRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private const string OpportunitySelect = @"
SELECT o.OpportunityId, o.TenantId, o.OpportunityNumber, o.AccountId,
       a.AccountName, o.OpportunityName, o.EstimatedAmount,
       o.StatusCodeId AS StatusCode, o.OwnerUserId,
       o.CloseDate, o.WinProbability, o.ForecastCategoryCode, o.LeadId,
       COALESCE(s.StageName, o.StageName, o.ForecastCategoryCode) AS StageName,
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

DECLARE @OpportunityNumber NVARCHAR(50) = NULLIF(LTRIM(RTRIM(@RequestedOpportunityNumber)), N'');

IF @OpportunityNumber IS NULL
BEGIN
    DECLARE @NextNumber INT = ISNULL((SELECT COUNT(1) FROM CRM.Opportunity WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IsDeleted = 0), 0) + 1;
    SET @OpportunityNumber = CONCAT(N'OPP-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', FORMAT(@NextNumber, N'00000'));

    WHILE EXISTS (SELECT 1 FROM CRM.Opportunity WHERE TenantId = @TenantId AND OpportunityNumber = @OpportunityNumber AND IsDeleted = 0)
    BEGIN
        SET @NextNumber += 1;
        SET @OpportunityNumber = CONCAT(N'OPP-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', FORMAT(@NextNumber, N'00000'));
    END;
END;

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
    @ForecastCategoryCode, (SELECT StageName FROM CRM.OpportunityStage WHERE OpportunityStageId = @OpportunityStageId), @OpportunityStageId, 1, SYSUTCDATETIME(), @CreatedByUserId, 0
);

IF OBJECT_ID(N'CRM.OpportunityLine', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM CRM.OpportunityLine WHERE OpportunityId = @OpportunityId AND IsDeleted = 0)
BEGIN
    DECLARE @PrimaryLineName NVARCHAR(100) = NULLIF(LTRIM(RTRIM(CASE WHEN CHARINDEX(N' - ', @OpportunityName) > 0 THEN RIGHT(@OpportunityName, CHARINDEX(N' - ', REVERSE(@OpportunityName)) - 1) ELSE @OpportunityName END)), N'');
    IF @PrimaryLineName IS NULL
        SELECT TOP 1 @PrimaryLineName = LobName
        FROM Agency.LineOfBusiness
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
        ORDER BY SortOrder, LobName;

    IF @PrimaryLineName IS NULL SET @PrimaryLineName = N'Opportunity';

    DECLARE @OpportunityLineId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO CRM.OpportunityLine
        (OpportunityLineId, TenantId, OpportunityId, LineOfBusiness, Carrier, EstPremium, Priority, Status, IsPrimary, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (@OpportunityLineId, @TenantId, @OpportunityId, LEFT(@PrimaryLineName, 100), NULL, @EstimatedAmount, N'Medium', N'Draft', 1, SYSUTCDATETIME(), @CreatedByUserId, 0);

    IF COL_LENGTH(N'CRM.Opportunity', N'PrimaryOpportunityLineId') IS NOT NULL
    BEGIN
        UPDATE CRM.Opportunity
        SET PrimaryOpportunityLineId = @OpportunityLineId
        WHERE OpportunityId = @OpportunityId;
    END;
END;";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            OpportunityId = id,
            request.TenantId,
            RequestedOpportunityNumber = request.OpportunityNumber,
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

        await RecordWorkflowEventAsync(cn, id, request.TenantId, "Created", "Opportunity created", "Opportunity was created with a system-generated opportunity number.", "Opportunity", id, request.CreatedByUserId, cancellationToken);

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

SELECT OpportunityLineId, TenantId, OpportunityId, LineOfBusiness, Carrier, EstPremium, Priority,
       COALESCE(Status, N'Draft') AS Status, COALESCE(IsPrimary, CONVERT(bit, 0)) AS IsPrimary,
       TargetEffectiveDate, AssignedToUserId, CreatedDateUtc, ModifiedDateUtc
FROM CRM.OpportunityLine
WHERE OpportunityId = @Id AND IsDeleted = 0
ORDER BY IsPrimary DESC, CreatedDateUtc;

SELECT a.ActivityId, a.TenantId, a.OpportunityId, a.ActivityTypeCode, a.Subject, a.Notes, a.ActivityDate, a.CreatedByUserId,
       COALESCE(NULLIF(LTRIM(RTRIM(u.FullName)), N''), NULLIF(LTRIM(RTRIM(CONCAT(u.FirstName, N' ', u.LastName))), N''), u.UserName, u.Email) AS CreatedByName,
       a.CreatedDateUtc, a.ModifiedDateUtc
FROM CRM.OpportunityActivity a
LEFT JOIN IAM.[User] u ON u.UserId = a.CreatedByUserId
WHERE a.OpportunityId = @Id AND a.IsDeleted = 0
ORDER BY a.ActivityDate DESC;

SELECT SubmissionId, TenantId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, TargetPremium, QuoteCount, CreatedDateUtc, ModifiedDateUtc
FROM
(
    SELECT s.SubmissionId, s.TenantId, s.OpportunityId, s.SubmissionNumber, s.LineOfBusiness, s.Status, s.TargetPremium,
           (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0) AS QuoteCount,
           s.CreatedDateUtc, s.ModifiedDateUtc
    FROM Submissions.Submission s
    WHERE s.OpportunityId = @Id AND s.IsDeleted = 0

    UNION ALL

    SELECT s.SubmissionId, s.TenantId, s.OpportunityId, s.SubmissionNumber, s.LineOfBusiness, s.Status, s.TargetPremium,
           (SELECT COUNT(1) FROM CRM.Quote q WHERE q.OpportunityId = s.OpportunityId AND q.IsDeleted = 0) AS QuoteCount,
           s.CreatedDateUtc, s.ModifiedDateUtc
    FROM CRM.OpportunitySubmission s
    WHERE s.OpportunityId = @Id
      AND s.IsDeleted = 0
      AND NOT EXISTS (SELECT 1 FROM Submissions.Submission ss WHERE ss.SubmissionId = s.SubmissionId AND ss.IsDeleted = 0)
) s
ORDER BY CreatedDateUtc DESC;

SELECT QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, TotalAmount, ValidUntilDate, StatusCode, CreatedDateUtc, ModifiedDateUtc
FROM CRM.Quote
WHERE OpportunityId = @Id AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;

SELECT p.PolicyId,
       p.SubmissionId,
       p.QuoteId,
       p.TenantId,
       p.AccountId,
       COALESCE(a.AccountName, s.SubmissionNumber, p.PolicyNumber) AS AccountName,
       p.CarrierId,
       COALESCE(c.CarrierName, N'') AS CarrierName,
       p.PolicyNumber,
       CASE WHEN p.Status = N'Bound' THEN N'Active' ELSE p.Status END AS Status,
       COALESCE(NULLIF(s.LineOfBusiness, N''), ol.LineOfBusiness, N'') AS LineOfBusiness,
       COALESCE(NULLIF(s.Priority, N''), ol.Priority, N'') AS Priority,
       p.AnnualPremium,
       p.AnnualPremium AS WrittenPremium,
       p.EffectiveDate,
       p.ExpirationDate,
       p.BoundDateUtc,
       s.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName, u.Email) AS AssignedToUserName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, u.Email, N'') AS ProducerName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, u.Email, N'') AS CsrName,
       (SELECT COUNT(1) FROM Compliance.PolicyDocument d WHERE d.TenantId = p.TenantId AND d.IsDeleted = 0 AND d.PolicyCode = p.PolicyNumber) AS DocumentCount,
       (SELECT COUNT(1) FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0) AS ActivityCount,
       (SELECT COUNT(1) FROM Policy.PolicyEndorsement e WHERE e.TenantId = p.TenantId AND e.PolicyNumber = p.PolicyNumber AND e.IsDeleted = 0) AS EndorsementCount,
       COALESCE(NULLIF(lastRenewal.Notes, N''), CASE
           WHEN DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) BETWEEN 0 AND 90 THEN N'Pre-Renewal'
           WHEN p.ExpirationDate < SYSUTCDATETIME() THEN N'Expired'
           ELSE N'Not Started'
       END) AS RenewalStage,
       COALESCE(lastAction.Notes, CONCAT(N'Policy bound ', CONVERT(nvarchar(10), p.BoundDateUtc, 101), N' from submission ', COALESCE(s.SubmissionNumber, N''))) AS LastAction,
       DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) AS DaysToExpiration,
       CAST(CASE WHEN p.IsDeleted = 0 AND p.ExpirationDate >= SYSUTCDATETIME() AND p.Status IN (N'Bound', N'Active', N'In Force') THEN 1 ELSE 0 END AS bit) AS IsActive
FROM Submissions.BoundPolicy p
JOIN CRM.Opportunity o ON o.AccountId = p.AccountId AND o.OpportunityId = @Id AND o.TenantId = p.TenantId AND o.IsDeleted = 0
LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
OUTER APPLY (SELECT TOP 1 LineOfBusiness, Priority FROM CRM.OpportunityLine line WHERE line.OpportunityId = @Id AND line.IsDeleted = 0 AND (s.LineOfBusiness IS NULL OR s.LineOfBusiness = N'' OR line.LineOfBusiness = s.LineOfBusiness) ORDER BY line.CreatedDateUtc DESC) ol
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 ORDER BY al.CreatedDateUtc DESC) lastAction
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 AND al.ActionCode = N'RenewalStage' ORDER BY al.CreatedDateUtc DESC) lastRenewal
WHERE p.IsDeleted = 0
ORDER BY CASE WHEN p.ExpirationDate >= SYSUTCDATETIME() AND p.Status IN (N'Bound', N'Active', N'In Force') THEN 0 ELSE 1 END,
         p.ExpirationDate ASC,
         p.BoundDateUtc DESC;

SELECT CompetitorId, TenantId, OpportunityId, Name, Strength, CreatedDateUtc, ModifiedDateUtc
FROM CRM.OpportunityCompetitor
WHERE OpportunityId = @Id AND IsDeleted = 0
ORDER BY CreatedDateUtc;

SELECT WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail,
       RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc
FROM CRM.OpportunityWorkflowEvent
WHERE OpportunityId = @Id AND IsDeleted = 0
ORDER BY EventDateUtc DESC, CreatedDateUtc DESC;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        var opportunity = await multi.ReadSingleOrDefaultAsync<OpportunityDto>();
        if (opportunity is null) return null;

        var lines = (await multi.ReadAsync<OpportunityLineDto>()).AsList();
        var activities = (await multi.ReadAsync<OpportunityActivityDto>()).AsList();
        var submissions = (await multi.ReadAsync<OpportunitySubmissionDto>()).AsList();
        var quotes = (await multi.ReadAsync<QuoteDto>()).AsList();
        var currentPolicies = (await multi.ReadAsync<OpportunityCurrentPolicyDto>()).AsList();
        var competitors = (await multi.ReadAsync<OpportunityCompetitorDto>()).AsList();
        var workflowEvents = (await multi.ReadAsync<OpportunityWorkflowEventDto>()).AsList();

        await AttachSubmissionLinesAsync(cn, submissions, cancellationToken);

        return new OpportunityDetailDto
        {
            Opportunity = opportunity,
            Lines = lines,
            Activities = activities,
            Submissions = submissions,
            Quotes = quotes,
            CurrentPolicies = currentPolicies,
            Competitors = competitors,
            WorkflowEvents = workflowEvents
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
            COALESCE(s.StageName, o.StageName, o.ForecastCategoryCode) AS StageName,
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

    public async Task<OpportunityConversionLaunchDto?> GetConversionLaunchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1
       o.OpportunityId,
       o.TenantId,
       lc.LeadConversionId,
       COALESCE(lc.LeadId, o.LeadId) AS LeadId,
       COALESCE(lc.AccountId, o.AccountId) AS AccountId,
       lc.SubmissionId,
       COALESCE(lc.SourceLeadNumber, l.LeadNumber) AS SourceLeadNumber,
       COALESCE(a.AccountName, lc.AccountNameSnapshot) AS AccountName,
       COALESCE(o.OpportunityName, lc.OpportunityNameSnapshot) AS OpportunityName,
       o.OpportunityNumber,
       lc.SubmissionNumber,
       lc.LineOfBusiness,
       COALESCE(lc.EstimatedAmount, o.EstimatedAmount) AS EstimatedAmount,
       lc.ConvertedDateUtc
FROM CRM.Opportunity o
LEFT JOIN CRM.LeadConversion lc ON lc.OpportunityId = o.OpportunityId AND lc.IsDeleted = 0
LEFT JOIN CRM.Lead l ON l.LeadId = COALESCE(lc.LeadId, o.LeadId)
LEFT JOIN Client.Account a ON a.AccountId = COALESCE(lc.AccountId, o.AccountId)
WHERE o.OpportunityId = @OpportunityId AND o.IsDeleted = 0
ORDER BY lc.ConvertedDateUtc DESC;

SELECT OpportunityConversionLaunchActionId, ActionCode, ActionTitle, ActionDescription, IconCssClass, ButtonCssClass,
       RouteTemplate, SortOrder, IsPrimary, OpensNewContext
FROM CRM.OpportunityConversionLaunchAction
WHERE TenantId = (SELECT TenantId FROM CRM.Opportunity WHERE OpportunityId = @OpportunityId)
  AND IsActive = 1
  AND IsDeleted = 0
ORDER BY SortOrder, ActionTitle;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { OpportunityId = id }, cancellationToken: cancellationToken));
        var launch = await multi.ReadSingleOrDefaultAsync<OpportunityConversionLaunchDto>();
        if (launch is null)
        {
            return null;
        }

        var actions = (await multi.ReadAsync<OpportunityConversionLaunchActionRow>()).AsList();
        launch.Actions = actions
            .Select(action => new OpportunityConversionLaunchActionDto
            {
                OpportunityConversionLaunchActionId = action.OpportunityConversionLaunchActionId,
                ActionCode = action.ActionCode,
                ActionTitle = action.ActionTitle,
                ActionDescription = action.ActionDescription,
                IconCssClass = action.IconCssClass,
                ButtonCssClass = action.ButtonCssClass,
                Route = ResolveLaunchRoute(action.RouteTemplate, launch),
                SortOrder = action.SortOrder,
                IsPrimary = action.IsPrimary,
                OpensNewContext = action.OpensNewContext,
                IsAvailable = IsLaunchActionAvailable(action.ActionCode, launch)
            })
            .ToList();

        return launch;
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
        await RecordWorkflowEventAsync(cn, id, null, "Updated", "Opportunity updated", $"Opportunity was updated with stage {request.StageName} and forecast {request.ForecastCategoryCode}.", "Opportunity", id, request.ModifiedByUserId, cancellationToken);
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
        await RecordWorkflowEventAsync(cn, id, null, "Stage", $"Moved to {request.Stage}", $"Opportunity stage changed to {request.Stage}.", "Opportunity", id, request.ModifiedByUserId, cancellationToken);
    }

    public async Task<Guid> UpsertLineAsync(UpsertOpportunityLineRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF @OpportunityLineId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM CRM.OpportunityLine WHERE OpportunityLineId = @OpportunityLineId AND OpportunityId = @OpportunityId AND IsDeleted = 0)
    THROW 51001, 'Opportunity line does not belong to this opportunity.', 1;

IF @OpportunityLineId IS NULL
BEGIN
    SET @OpportunityLineId = NEWID();

    IF @IsPrimary = 1
    BEGIN
        UPDATE CRM.OpportunityLine
        SET IsPrimary = 0, ModifiedByUserId = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE OpportunityId = @OpportunityId AND IsDeleted = 0 AND IsPrimary = 1;
    END;

    INSERT INTO CRM.OpportunityLine
        (OpportunityLineId, TenantId, OpportunityId, LineOfBusiness, Carrier, EstPremium, Priority, Status, IsPrimary, TargetEffectiveDate, AssignedToUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (@OpportunityLineId, @TenantId, @OpportunityId, @LineOfBusiness, @Carrier, @EstPremium, @Priority, @Status, @IsPrimary, @TargetEffectiveDate, @AssignedToUserId, SYSUTCDATETIME(), @UserId, 0);
END
ELSE
BEGIN
    IF @IsPrimary = 1
    BEGIN
        UPDATE CRM.OpportunityLine
        SET IsPrimary = 0, ModifiedByUserId = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE OpportunityId = @OpportunityId AND OpportunityLineId <> @OpportunityLineId AND IsDeleted = 0 AND IsPrimary = 1;
    END;

    UPDATE CRM.OpportunityLine
    SET LineOfBusiness = @LineOfBusiness,
        Carrier = @Carrier,
        EstPremium = @EstPremium,
        Priority = @Priority,
        Status = @Status,
        IsPrimary = @IsPrimary,
        TargetEffectiveDate = @TargetEffectiveDate,
        AssignedToUserId = @AssignedToUserId,
        ModifiedByUserId = @UserId,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE OpportunityLineId = @OpportunityLineId AND IsDeleted = 0;
END;

IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityLine WHERE OpportunityId = @OpportunityId AND IsDeleted = 0 AND IsPrimary = 1)
BEGIN
    UPDATE CRM.OpportunityLine
    SET IsPrimary = 1, ModifiedByUserId = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
    WHERE OpportunityLineId = @OpportunityLineId AND IsDeleted = 0;
END;

UPDATE o
SET PrimaryOpportunityLineId = primaryLine.OpportunityLineId,
    EstimatedAmount = COALESCE(lineTotals.EstimatedAmount, o.EstimatedAmount),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @UserId
FROM CRM.Opportunity o
OUTER APPLY (SELECT TOP 1 OpportunityLineId FROM CRM.OpportunityLine line WHERE line.OpportunityId = o.OpportunityId AND line.IsDeleted = 0 ORDER BY line.IsPrimary DESC, line.EstPremium DESC, line.CreatedDateUtc) primaryLine
OUTER APPLY (SELECT SUM(line.EstPremium) AS EstimatedAmount FROM CRM.OpportunityLine line WHERE line.OpportunityId = o.OpportunityId AND line.IsDeleted = 0) lineTotals
WHERE o.OpportunityId = @OpportunityId AND o.IsDeleted = 0;

SELECT @OpportunityLineId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var isNew = request.OpportunityLineId is null;
        var id = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.OpportunityLineId, request.TenantId, request.OpportunityId, request.LineOfBusiness, request.Carrier, request.EstPremium, request.Priority, request.Status, request.IsPrimary, request.TargetEffectiveDate, request.AssignedToUserId, request.UserId }, cancellationToken: cancellationToken));
        await RecordWorkflowEventAsync(cn, request.OpportunityId, request.TenantId, isNew ? "LineAdded" : "LineUpdated", isNew ? "Coverage line added" : "Coverage line updated", $"{request.LineOfBusiness} line is {request.Status} with estimated premium {request.EstPremium:C0}.", "OpportunityLine", id, request.UserId, cancellationToken);
        return id;
    }

    public async Task SetPrimaryLineAsync(Guid opportunityId, Guid opportunityLineId, Guid? userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityLine WHERE OpportunityId = @OpportunityId AND OpportunityLineId = @OpportunityLineId AND IsDeleted = 0)
    THROW 51002, 'Primary line does not belong to this opportunity.', 1;

UPDATE CRM.OpportunityLine
SET IsPrimary = 0,
    ModifiedByUserId = @UserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE OpportunityId = @OpportunityId AND OpportunityLineId <> @OpportunityLineId AND IsDeleted = 0 AND IsPrimary = 1;

UPDATE CRM.OpportunityLine
SET IsPrimary = 1,
    ModifiedByUserId = @UserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE OpportunityId = @OpportunityId AND OpportunityLineId = @OpportunityLineId AND IsDeleted = 0;

UPDATE CRM.Opportunity
SET PrimaryOpportunityLineId = @OpportunityLineId,
    ModifiedByUserId = @UserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE OpportunityId = @OpportunityId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { OpportunityId = opportunityId, OpportunityLineId = opportunityLineId, UserId = userId }, cancellationToken: cancellationToken));
        await RecordWorkflowEventAsync(cn, opportunityId, null, "PrimaryLine", "Primary line changed", "The opportunity primary coverage line was updated.", "OpportunityLine", opportunityLineId, userId, cancellationToken);
    }

    public async Task DeleteLineAsync(Guid opportunityLineId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @OpportunityId UNIQUEIDENTIFIER = (SELECT OpportunityId FROM CRM.OpportunityLine WHERE OpportunityLineId = @OpportunityLineId);
DECLARE @WasPrimary BIT = COALESCE((SELECT IsPrimary FROM CRM.OpportunityLine WHERE OpportunityLineId = @OpportunityLineId), 0);

UPDATE CRM.OpportunityLine
SET IsDeleted = 1, IsPrimary = 0, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE OpportunityLineId = @OpportunityLineId;

UPDATE CRM.OpportunitySubmissionLine
SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE OpportunityLineId = @OpportunityLineId AND IsDeleted = 0;

IF @OpportunityId IS NOT NULL
BEGIN
    DECLARE @NextPrimary UNIQUEIDENTIFIER = NULL;

    IF @WasPrimary = 1 OR NOT EXISTS (SELECT 1 FROM CRM.OpportunityLine WHERE OpportunityId = @OpportunityId AND IsDeleted = 0 AND IsPrimary = 1)
        SET @NextPrimary = (SELECT TOP 1 OpportunityLineId FROM CRM.OpportunityLine WHERE OpportunityId = @OpportunityId AND IsDeleted = 0 ORDER BY EstPremium DESC, CreatedDateUtc);
    ELSE
        SET @NextPrimary = (SELECT TOP 1 OpportunityLineId FROM CRM.OpportunityLine WHERE OpportunityId = @OpportunityId AND IsDeleted = 0 AND IsPrimary = 1);

    IF @NextPrimary IS NOT NULL
    BEGIN
        UPDATE CRM.OpportunityLine SET IsPrimary = 0 WHERE OpportunityId = @OpportunityId AND IsDeleted = 0 AND OpportunityLineId <> @NextPrimary AND IsPrimary = 1;
        UPDATE CRM.OpportunityLine SET IsPrimary = 1 WHERE OpportunityLineId = @NextPrimary AND IsDeleted = 0;
    END;

    UPDATE CRM.Opportunity
    SET PrimaryOpportunityLineId = @NextPrimary,
        EstimatedAmount = COALESCE((SELECT SUM(EstPremium) FROM CRM.OpportunityLine WHERE OpportunityId = @OpportunityId AND IsDeleted = 0), 0),
        ModifiedByUserId = @ModifiedByUserId,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE OpportunityId = @OpportunityId;
END;

SELECT @OpportunityId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var opportunityId = await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new { OpportunityLineId = opportunityLineId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
        if (opportunityId.HasValue)
            await RecordWorkflowEventAsync(cn, opportunityId.Value, null, "LineDeleted", "Coverage line deleted", "An opportunity coverage line was deleted.", "OpportunityLine", opportunityLineId, modifiedByUserId, cancellationToken);
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
        var isNew = request.ActivityId is null;
        var id = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.ActivityId, request.TenantId, request.OpportunityId, request.ActivityTypeCode, request.Subject, request.Notes, request.ActivityDate, request.UserId }, cancellationToken: cancellationToken));
        await RecordWorkflowEventAsync(cn, request.OpportunityId, request.TenantId, isNew ? "Activity" : "ActivityUpdated", isNew ? "Activity logged" : "Activity updated", request.Subject, "Activity", id, request.UserId, cancellationToken);
        return id;
    }

    public async Task DeleteActivityAsync(Guid activityId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.OpportunityActivity SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE ActivityId = @ActivityId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var target = await cn.QuerySingleOrDefaultAsync<WorkflowTarget>(new CommandDefinition("SELECT TenantId, OpportunityId FROM CRM.OpportunityActivity WHERE ActivityId = @ActivityId;", new { ActivityId = activityId }, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ActivityId = activityId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
        if (target is not null)
            await RecordWorkflowEventAsync(cn, target.OpportunityId, target.TenantId, "ActivityDeleted", "Activity deleted", "An opportunity activity was deleted.", "Activity", activityId, modifiedByUserId, cancellationToken);
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
        var isNew = request.SubmissionId is null;
        var id = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.SubmissionId, request.TenantId, request.OpportunityId, request.SubmissionNumber, request.LineOfBusiness, request.Status, request.TargetPremium, request.UserId }, cancellationToken: cancellationToken));
        await SyncOpportunitySubmissionLinesAsync(cn, request, id, cancellationToken);
        await RecordWorkflowEventAsync(cn, request.OpportunityId, request.TenantId, isNew ? "Submission" : "SubmissionUpdated", isNew ? "Submission created" : "Submission updated", $"{request.LineOfBusiness} submission is {request.Status} with target premium {request.TargetPremium:C0}.", "Submission", id, request.UserId, cancellationToken);
        return id;
    }

    public async Task DeleteSubmissionAsync(Guid submissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.OpportunitySubmission SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE SubmissionId = @SubmissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var target = await cn.QuerySingleOrDefaultAsync<WorkflowTarget>(new CommandDefinition("SELECT TenantId, OpportunityId FROM CRM.OpportunitySubmission WHERE SubmissionId = @SubmissionId;", new { SubmissionId = submissionId }, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
        if (target is not null)
            await RecordWorkflowEventAsync(cn, target.OpportunityId, target.TenantId, "SubmissionDeleted", "Submission deleted", "An opportunity submission was deleted.", "Submission", submissionId, modifiedByUserId, cancellationToken);
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
        var isNew = request.CompetitorId is null;
        var id = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.CompetitorId, request.TenantId, request.OpportunityId, request.Name, request.Strength, request.UserId }, cancellationToken: cancellationToken));
        await RecordWorkflowEventAsync(cn, request.OpportunityId, request.TenantId, isNew ? "Competitor" : "CompetitorUpdated", isNew ? "Competitor added" : "Competitor updated", $"{request.Name} tracked as {request.Strength}.", "Competitor", id, request.UserId, cancellationToken);
        return id;
    }

    public async Task DeleteCompetitorAsync(Guid competitorId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE CRM.OpportunityCompetitor SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME() WHERE CompetitorId = @CompetitorId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var target = await cn.QuerySingleOrDefaultAsync<WorkflowTarget>(new CommandDefinition("SELECT TenantId, OpportunityId FROM CRM.OpportunityCompetitor WHERE CompetitorId = @CompetitorId;", new { CompetitorId = competitorId }, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CompetitorId = competitorId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
        if (target is not null)
            await RecordWorkflowEventAsync(cn, target.OpportunityId, target.TenantId, "CompetitorDeleted", "Competitor deleted", "An opportunity competitor was deleted.", "Competitor", competitorId, modifiedByUserId, cancellationToken);
    }

    private static async Task RecordWorkflowEventAsync(IDbConnection cn, Guid opportunityId, Guid? tenantId, string eventType, string eventTitle, string? eventDetail, string? relatedEntityName, Guid? relatedEntityId, Guid? userId, CancellationToken cancellationToken)
    {
        var resolvedTenantId = tenantId ?? await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT TenantId FROM CRM.Opportunity WHERE OpportunityId = @OpportunityId;",
            new { OpportunityId = opportunityId }, cancellationToken: cancellationToken));

        if (!resolvedTenantId.HasValue || resolvedTenantId.Value == Guid.Empty)
            return;

        const string sql = @"
IF OBJECT_ID(N'CRM.OpportunityWorkflowEvent', N'U') IS NOT NULL
BEGIN
    INSERT INTO CRM.OpportunityWorkflowEvent
    (
        WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail,
        RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted
    )
    VALUES
    (
        NEWID(), @TenantId, @OpportunityId, @EventType, @EventTitle, @EventDetail,
        @RelatedEntityName, @RelatedEntityId, SYSUTCDATETIME(), SYSUTCDATETIME(), @UserId, 0
    );
END;";

        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = resolvedTenantId.Value,
            OpportunityId = opportunityId,
            EventType = eventType,
            EventTitle = eventTitle,
            EventDetail = eventDetail,
            RelatedEntityName = relatedEntityName,
            RelatedEntityId = relatedEntityId,
            UserId = userId
        }, cancellationToken: cancellationToken));
    }

    private static async Task AttachSubmissionLinesAsync(IDbConnection cn, List<OpportunitySubmissionDto> submissions, CancellationToken cancellationToken)
    {
        if (submissions.Count == 0)
            return;

        const string sql = @"
SELECT OpportunitySubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LineOfBusiness, TargetPremium
FROM CRM.OpportunitySubmissionLine
WHERE IsDeleted = 0 AND SubmissionId IN @SubmissionIds
ORDER BY CreatedDateUtc;";

        var rows = (await cn.QueryAsync<OpportunitySubmissionLineDto>(new CommandDefinition(sql, new { SubmissionIds = submissions.Select(s => s.SubmissionId).ToArray() }, cancellationToken: cancellationToken))).AsList();
        var lookup = rows.GroupBy(row => row.SubmissionId).ToDictionary(group => group.Key, group => (IReadOnlyList<OpportunitySubmissionLineDto>)group.ToList());

        foreach (var submission in submissions)
        {
            if (lookup.TryGetValue(submission.SubmissionId, out var lines))
                submission.Lines = lines;
        }
    }

    private static async Task SyncOpportunitySubmissionLinesAsync(IDbConnection cn, UpsertOpportunitySubmissionRequest request, Guid submissionId, CancellationToken cancellationToken)
    {
        var requestedLineIds = request.OpportunityLineIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];

        if (requestedLineIds.Length == 0)
        {
            requestedLineIds = (await cn.QueryAsync<Guid>(new CommandDefinition(@"
SELECT TOP 1 OpportunityLineId
FROM CRM.OpportunityLine
WHERE OpportunityId = @OpportunityId
  AND IsDeleted = 0
  AND (LineOfBusiness = @LineOfBusiness OR IsPrimary = 1)
ORDER BY CASE WHEN LineOfBusiness = @LineOfBusiness THEN 0 ELSE 1 END, IsPrimary DESC, EstPremium DESC, CreatedDateUtc;",
                new { request.OpportunityId, request.LineOfBusiness }, cancellationToken: cancellationToken))).ToArray();
        }

        if (requestedLineIds.Length == 0)
            return;

        const string softDeleteSql = @"
UPDATE CRM.OpportunitySubmissionLine
SET IsDeleted = 1, ModifiedByUserId = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId
  AND IsDeleted = 0
  AND OpportunityLineId NOT IN @OpportunityLineIds;";

        await cn.ExecuteAsync(new CommandDefinition(softDeleteSql, new { SubmissionId = submissionId, OpportunityLineIds = requestedLineIds, request.UserId }, cancellationToken: cancellationToken));

        const string upsertSql = @"
MERGE CRM.OpportunitySubmissionLine AS target
USING
(
    SELECT line.TenantId, @SubmissionId AS SubmissionId, line.OpportunityId, line.OpportunityLineId, line.LineOfBusiness,
           CASE WHEN @SelectedLineCount = 1 THEN @TargetPremium ELSE line.EstPremium END AS TargetPremium
    FROM CRM.OpportunityLine line
    WHERE line.OpportunityId = @OpportunityId
      AND line.IsDeleted = 0
      AND line.OpportunityLineId IN @OpportunityLineIds
) AS source
ON target.SubmissionId = source.SubmissionId AND target.OpportunityLineId = source.OpportunityLineId
WHEN MATCHED THEN UPDATE SET
    target.LineOfBusiness = source.LineOfBusiness,
    target.TargetPremium = source.TargetPremium,
    target.IsDeleted = 0,
    target.ModifiedByUserId = @UserId,
    target.ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (OpportunitySubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LineOfBusiness, TargetPremium, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), source.TenantId, source.SubmissionId, source.OpportunityId, source.OpportunityLineId, source.LineOfBusiness, source.TargetPremium, SYSUTCDATETIME(), @UserId, 0);";

        await cn.ExecuteAsync(new CommandDefinition(upsertSql, new { SubmissionId = submissionId, request.OpportunityId, OpportunityLineIds = requestedLineIds, SelectedLineCount = requestedLineIds.Length, request.TargetPremium, request.UserId }, cancellationToken: cancellationToken));

        const string summarySql = @"
UPDATE CRM.OpportunitySubmission
SET LineOfBusiness = lineSummary.LineLabel,
    TargetPremium = lineSummary.TargetPremium,
    ModifiedByUserId = @UserId,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM CRM.OpportunitySubmission s
CROSS APPLY
(
    SELECT LineLabel = CASE WHEN COUNT(1) = 1 THEN MAX(LineOfBusiness) ELSE CONCAT(COUNT(1), N' line package') END,
           TargetPremium = SUM(TargetPremium)
    FROM CRM.OpportunitySubmissionLine line
    WHERE line.SubmissionId = s.SubmissionId AND line.IsDeleted = 0
) lineSummary
WHERE s.SubmissionId = @SubmissionId;";

        await cn.ExecuteAsync(new CommandDefinition(summarySql, new { SubmissionId = submissionId, request.UserId }, cancellationToken: cancellationToken));
    }

    private static bool IsLaunchActionAvailable(string actionCode, OpportunityConversionLaunchDto launch)
        => actionCode switch
        {
            "REVIEW_ACCOUNT" => launch.AccountId.HasValue,
            "REVIEW_SUBMISSION" => launch.SubmissionId.HasValue,
            "REVIEW_SOURCE_LEAD" => launch.LeadId.HasValue,
            _ => true
        };

    private static string ResolveLaunchRoute(string routeTemplate, OpportunityConversionLaunchDto launch)
    {
        return routeTemplate
            .Replace("{OpportunityId}", launch.OpportunityId.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{LeadConversionId}", launch.LeadConversionId?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{LeadId}", launch.LeadId?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{AccountId}", launch.AccountId?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{SubmissionId}", launch.SubmissionId?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record WorkflowTarget(Guid TenantId, Guid OpportunityId);

    private sealed class OpportunityConversionLaunchActionRow
    {
        public Guid OpportunityConversionLaunchActionId { get; set; }
        public string ActionCode { get; set; } = string.Empty;
        public string ActionTitle { get; set; } = string.Empty;
        public string? ActionDescription { get; set; }
        public string? IconCssClass { get; set; }
        public string? ButtonCssClass { get; set; }
        public string RouteTemplate { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsPrimary { get; set; }
        public bool OpensNewContext { get; set; }
    }
}
