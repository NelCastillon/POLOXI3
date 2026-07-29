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

DECLARE @PolicySql NVARCHAR(MAX) = N'
SELECT p.PolicyId,
       p.SubmissionId,
       p.QuoteId,
       p.TenantId,
       p.AccountId,
       COALESCE(a.AccountName, s.SubmissionNumber, p.PolicyNumber) AS AccountName,
       p.CarrierId,
       COALESCE(c.CarrierName, N'''') AS CarrierName,
       p.PolicyNumber,
       CASE WHEN p.Status = N''Bound'' THEN N''Active'' ELSE p.Status END AS Status,
       COALESCE(NULLIF(s.LineOfBusiness, N''''), ol.LineOfBusiness, N'''') AS LineOfBusiness,
       COALESCE(NULLIF(s.Priority, N''''), ol.Priority, N'''') AS Priority,
       p.AnnualPremium,
       p.AnnualPremium AS WrittenPremium,
       p.EffectiveDate,
       p.ExpirationDate,
       p.BoundDateUtc,
       s.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName, u.Email) AS AssignedToUserName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, u.Email, N'''') AS ProducerName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, u.Email, N'''') AS CsrName,
       (SELECT COUNT(1) FROM Compliance.PolicyDocument d WHERE d.TenantId = p.TenantId AND d.IsDeleted = 0 AND d.PolicyCode = p.PolicyNumber) AS DocumentCount,
       (SELECT COUNT(1) FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0) AS ActivityCount,
       (SELECT COUNT(1) FROM Policy.PolicyEndorsement e WHERE e.TenantId = p.TenantId AND e.PolicyNumber = p.PolicyNumber AND e.IsDeleted = 0) AS EndorsementCount,
       COALESCE(NULLIF(lastRenewal.Notes, N''''), CASE
           WHEN DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) BETWEEN 0 AND 90 THEN N''Pre-Renewal''
           WHEN p.ExpirationDate < SYSUTCDATETIME() THEN N''Expired''
           ELSE N''Not Started''
       END) AS RenewalStage,
       COALESCE(lastAction.Notes, CONCAT(N''Policy bound '', CONVERT(nvarchar(10), p.BoundDateUtc, 101), N'' from submission '', COALESCE(s.SubmissionNumber, N''''))) AS LastAction,
       DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) AS DaysToExpiration,
       CAST(CASE WHEN p.IsDeleted = 0 AND p.ExpirationDate >= SYSUTCDATETIME() AND p.Status IN (N''Bound'', N''Active'', N''In Force'') THEN 1 ELSE 0 END AS bit) AS IsActive
FROM Submissions.BoundPolicy p
JOIN CRM.Opportunity o ON o.OpportunityId = @PolicyOpportunityId AND o.TenantId = p.TenantId AND o.IsDeleted = 0
LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
OUTER APPLY (SELECT TOP 1 LineOfBusiness, Priority FROM CRM.OpportunityLine line WHERE line.OpportunityId = @PolicyOpportunityId AND line.IsDeleted = 0 AND (s.LineOfBusiness IS NULL OR s.LineOfBusiness = N'''' OR line.LineOfBusiness = s.LineOfBusiness) ORDER BY line.CreatedDateUtc DESC) ol
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 ORDER BY al.CreatedDateUtc DESC) lastAction
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 AND al.ActionCode = N''RenewalStage'' ORDER BY al.CreatedDateUtc DESC) lastRenewal
' + CASE WHEN OBJECT_ID(N'CRM.OpportunityBoundPolicy', N'U') IS NOT NULL THEN N'
LEFT JOIN CRM.OpportunityBoundPolicy link ON link.PolicyId = p.PolicyId AND link.TenantId = p.TenantId AND link.OpportunityId = o.OpportunityId AND link.IsDeleted = 0
' ELSE N'' END + N'
WHERE p.IsDeleted = 0
  AND p.AccountId = o.AccountId
  AND (s.OpportunityId = o.OpportunityId' + CASE WHEN OBJECT_ID(N'CRM.OpportunityBoundPolicy', N'U') IS NOT NULL THEN N' OR link.OpportunityBoundPolicyId IS NOT NULL' ELSE N'' END + N')
ORDER BY CASE WHEN p.ExpirationDate >= SYSUTCDATETIME() AND p.Status IN (N''Bound'', N''Active'', N''In Force'') THEN 0 ELSE 1 END,
         p.ExpirationDate ASC,
         p.BoundDateUtc DESC;';

EXEC sp_executesql @PolicySql, N'@PolicyOpportunityId UNIQUEIDENTIFIER', @PolicyOpportunityId = @Id;

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

    public async Task<PagedResult<OpportunityCompetitorLookupDto>> SearchCompetitorLookupsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Competitors AS (
    SELECT LTRIM(RTRIM(Name)) AS Name,
           COUNT(DISTINCT OpportunityId) AS OpportunityCount,
           MAX(COALESCE(ModifiedDateUtc, CreatedDateUtc)) AS LastUsedDateUtc
    FROM CRM.OpportunityCompetitor
    WHERE TenantId = @TenantId
      AND IsDeleted = 0
      AND NULLIF(LTRIM(RTRIM(Name)), N'') IS NOT NULL
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Name LIKE '%' + @SearchTerm + '%')
    GROUP BY LTRIM(RTRIM(Name))
)
SELECT Name, OpportunityCount, LastUsedDateUtc
FROM Competitors
ORDER BY LastUsedDateUtc DESC, Name ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(*)
FROM (
    SELECT LTRIM(RTRIM(Name)) AS Name
    FROM CRM.OpportunityCompetitor
    WHERE TenantId = @TenantId
      AND IsDeleted = 0
      AND NULLIF(LTRIM(RTRIM(Name)), N'') IS NOT NULL
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Name LIKE '%' + @SearchTerm + '%')
    GROUP BY LTRIM(RTRIM(Name))
) c;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<OpportunityCompetitorLookupDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<OpportunityCompetitorLookupDto>
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
IF @StageName = N'Closed Won'
   AND NOT EXISTS
   (
       SELECT 1
       FROM CRM.Opportunity o
       INNER JOIN Submissions.Submission s ON s.OpportunityId = o.OpportunityId AND s.TenantId = o.TenantId AND s.IsDeleted = 0
       INNER JOIN Submissions.BoundPolicy p ON p.SubmissionId = s.SubmissionId AND p.TenantId = o.TenantId AND p.IsDeleted = 0
       WHERE o.OpportunityId = @OpportunityId
         AND o.IsDeleted = 0
   )
   AND NOT EXISTS
   (
       SELECT 1
       FROM CRM.Opportunity o
       INNER JOIN CRM.OpportunityBoundPolicy link ON link.OpportunityId = o.OpportunityId AND link.TenantId = o.TenantId AND link.IsDeleted = 0
       INNER JOIN Submissions.BoundPolicy p ON p.PolicyId = link.PolicyId AND p.TenantId = o.TenantId AND p.IsDeleted = 0
       WHERE o.OpportunityId = @OpportunityId
         AND o.IsDeleted = 0
   )
    THROW 51005, 'Closed Won requires a bound policy. Select a quote, capture customer authorization, complete the bind request, and wait for carrier confirmation first.', 1;

UPDATE CRM.Opportunity
SET OpportunityName = @OpportunityName,
    EstimatedAmount = @EstimatedAmount,
    CloseDate = @CloseDate,
    WinProbability = @WinProbability,
    ForecastCategoryCode = @ForecastCategoryCode,
    StageName = @StageName,
    OpportunityStageId = COALESCE((
        SELECT TOP 1 stage.OpportunityStageId
        FROM CRM.OpportunityStage stage
        WHERE stage.TenantId = CRM.Opportunity.TenantId
          AND stage.IsActive = 1
          AND (stage.StageName = @StageName OR stage.StageCode = UPPER(REPLACE(@StageName, N' ', N'_')))
        ORDER BY stage.SortOrder, stage.StageName
    ), OpportunityStageId),
    OwnerUserId = COALESCE(@OwnerUserId, OwnerUserId),
    Description = @Description,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE OpportunityId = @OpportunityId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { OpportunityId = id, request.OpportunityName, request.EstimatedAmount, request.CloseDate, request.WinProbability, request.ForecastCategoryCode, request.StageName, request.OwnerUserId, request.Description, request.ModifiedByUserId }, cancellationToken: cancellationToken));
        await RecordWorkflowEventAsync(cn, id, null, "Updated", "Opportunity updated", $"Opportunity was updated with stage {request.StageName} and forecast {request.ForecastCategoryCode}.", "Opportunity", id, request.ModifiedByUserId, cancellationToken);
    }

    public async Task<OpportunityStageUpdateResult> UpdateStageAsync(Guid id, UpdateOpportunityStageRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON;

DECLARE @TenantId UNIQUEIDENTIFIER;
DECLARE @AccountId UNIQUEIDENTIFIER;
DECLARE @OpportunityNumber NVARCHAR(50);
DECLARE @OpportunityName NVARCHAR(200);
DECLARE @EstimatedAmount DECIMAL(18,2);
DECLARE @CloseDate DATETIME2;
DECLARE @OwnerUserId UNIQUEIDENTIFIER;
DECLARE @OpportunityStageId UNIQUEIDENTIFIER;

DECLARE @PolicyId UNIQUEIDENTIFIER = NULL;
DECLARE @PolicyNumber NVARCHAR(50) = NULL;
DECLARE @PolicyCreated BIT = 0;
DECLARE @PolicyAlreadyExists BIT = 0;
DECLARE @Message NVARCHAR(500) = CONCAT(N'Opportunity marked ', @Stage, N'.');

BEGIN TRY
    BEGIN TRANSACTION;

    SELECT @TenantId = o.TenantId,
           @AccountId = o.AccountId,
           @OpportunityNumber = o.OpportunityNumber,
           @OpportunityName = o.OpportunityName,
           @EstimatedAmount = o.EstimatedAmount,
           @CloseDate = o.CloseDate,
           @OwnerUserId = o.OwnerUserId
    FROM CRM.Opportunity o WITH (UPDLOCK, HOLDLOCK)
    WHERE o.OpportunityId = @OpportunityId AND o.IsDeleted = 0;

    IF @TenantId IS NULL
        THROW 51001, 'Opportunity was not found.', 1;

    IF @Stage = N'Closed Won'
       AND NOT EXISTS
       (
           SELECT 1
           FROM Submissions.Submission s
           INNER JOIN Submissions.BoundPolicy p ON p.SubmissionId = s.SubmissionId AND p.TenantId = s.TenantId AND p.IsDeleted = 0
           WHERE s.OpportunityId = @OpportunityId
             AND s.TenantId = @TenantId
             AND s.IsDeleted = 0
       )
       AND NOT EXISTS
       (
           SELECT 1
           FROM CRM.OpportunityBoundPolicy link
           INNER JOIN Submissions.BoundPolicy p ON p.PolicyId = link.PolicyId AND p.TenantId = link.TenantId AND p.IsDeleted = 0
           WHERE link.OpportunityId = @OpportunityId
             AND link.TenantId = @TenantId
             AND link.IsDeleted = 0
       )
        THROW 51005, 'Closed Won requires a bound policy. Select a quote, capture customer authorization, complete the bind request, and wait for carrier confirmation first.', 1;

    SELECT TOP 1 @OpportunityStageId = OpportunityStageId
    FROM CRM.OpportunityStage
    WHERE TenantId = @TenantId
      AND IsActive = 1
      AND (StageName = @Stage OR StageCode = UPPER(REPLACE(@Stage, N' ', N'_')))
    ORDER BY SortOrder, StageName;

    UPDATE CRM.Opportunity
    SET StageName = @Stage,
        OpportunityStageId = COALESCE(@OpportunityStageId, OpportunityStageId),
        ForecastCategoryCode = CASE WHEN @Stage IN (N'Closed Won', N'Closed Lost') THEN N'Closed' ELSE ForecastCategoryCode END,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ModifiedByUserId
    WHERE OpportunityId = @OpportunityId AND IsDeleted = 0;

    IF @CreateBoundPolicy = 1 AND @Stage = N'Closed Won'
    BEGIN
        SELECT TOP 1 @PolicyId = p.PolicyId,
                     @PolicyNumber = p.PolicyNumber
        FROM CRM.OpportunityBoundPolicy link WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN Submissions.BoundPolicy p WITH (UPDLOCK, HOLDLOCK) ON p.PolicyId = link.PolicyId AND p.IsDeleted = 0
        WHERE link.OpportunityId = @OpportunityId
          AND link.TenantId = @TenantId
          AND link.IsDeleted = 0
        ORDER BY p.BoundDateUtc DESC;

        IF @PolicyId IS NULL
        BEGIN
            SELECT TOP 1 @PolicyId = p.PolicyId,
                         @PolicyNumber = p.PolicyNumber
            FROM Submissions.BoundPolicy p WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
            WHERE s.OpportunityId = @OpportunityId
              AND p.TenantId = @TenantId
              AND p.IsDeleted = 0
            ORDER BY p.BoundDateUtc DESC;

            IF @PolicyId IS NOT NULL
            BEGIN
                INSERT INTO CRM.OpportunityBoundPolicy
                    (OpportunityBoundPolicyId, TenantId, OpportunityId, OpportunitySubmissionId, SubmissionId, QuoteId, PolicyId, PolicyNumber, BindingStatus, BoundDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
                SELECT NEWID(), p.TenantId, @OpportunityId, source.SubmissionId, p.SubmissionId, p.QuoteId, p.PolicyId, p.PolicyNumber, p.Status, p.BoundDateUtc, SYSUTCDATETIME(), @ModifiedByUserId, 0
                FROM Submissions.BoundPolicy p
                INNER JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
                OUTER APPLY
                (
                    SELECT TOP 1 os.SubmissionId
                    FROM CRM.OpportunitySubmission os
                    WHERE os.TenantId = p.TenantId
                      AND os.OpportunityId = @OpportunityId
                      AND os.IsDeleted = 0
                      AND (os.LineOfBusiness = s.LineOfBusiness OR os.SubmissionNumber = s.SubmissionNumber)
                    ORDER BY CASE WHEN os.Status = N'Bound' THEN 0 ELSE 1 END, os.ModifiedDateUtc DESC, os.CreatedDateUtc DESC
                ) source
                WHERE p.PolicyId = @PolicyId
                  AND NOT EXISTS (SELECT 1 FROM CRM.OpportunityBoundPolicy existing WHERE existing.PolicyId = p.PolicyId AND existing.IsDeleted = 0);
            END
        END

        IF @PolicyId IS NOT NULL
        BEGIN
            SET @PolicyAlreadyExists = 1;
            SET @Message = CONCAT(N'Opportunity marked Closed Won. Bound policy ', @PolicyNumber, N' already exists.');
        END
        ELSE
        BEGIN
            DECLARE @SourceOpportunitySubmissionId UNIQUEIDENTIFIER = NULL;
            DECLARE @SubmissionId UNIQUEIDENTIFIER = NULL;
            DECLARE @QuoteId UNIQUEIDENTIFIER = NEWID();
            DECLARE @CarrierId UNIQUEIDENTIFIER = NULL;
            DECLARE @SubmissionNumber NVARCHAR(50) = NULL;
            DECLARE @LineOfBusiness NVARCHAR(100) = NULL;
            DECLARE @Priority NVARCHAR(50) = NULL;
            DECLARE @CarrierName NVARCHAR(200) = NULL;
            DECLARE @AnnualPremium DECIMAL(18,2) = NULL;
            DECLARE @EffectiveDate DATETIME2 = NULL;
            DECLARE @ExpirationDate DATETIME2 = NULL;
            DECLARE @QuoteNumber NVARCHAR(50) = NULL;
            DECLARE @SubmissionMarketId UNIQUEIDENTIFIER = NULL;
            DECLARE @QuoteRequestId UNIQUEIDENTIFIER = NEWID();

            SELECT TOP 1 @SourceOpportunitySubmissionId = s.SubmissionId,
                         @SubmissionNumber = NULLIF(LTRIM(RTRIM(s.SubmissionNumber)), N''),
                         @LineOfBusiness = NULLIF(LTRIM(RTRIM(s.LineOfBusiness)), N''),
                         @AnnualPremium = NULLIF(s.TargetPremium, 0)
            FROM CRM.OpportunitySubmission s WITH (UPDLOCK, HOLDLOCK)
            WHERE s.OpportunityId = @OpportunityId
              AND s.TenantId = @TenantId
              AND s.IsDeleted = 0
            ORDER BY CASE s.Status WHEN N'Quoted' THEN 0 WHEN N'In Review' THEN 1 WHEN N'InReview' THEN 1 WHEN N'Draft' THEN 2 ELSE 3 END,
                     s.ModifiedDateUtc DESC,
                     s.CreatedDateUtc DESC;

            SELECT TOP 1 @LineOfBusiness = COALESCE(@LineOfBusiness, NULLIF(LTRIM(RTRIM(line.LineOfBusiness)), N'')),
                         @Priority = NULLIF(LTRIM(RTRIM(line.Priority)), N''),
                         @CarrierName = NULLIF(LTRIM(RTRIM(line.Carrier)), N''),
                         @AnnualPremium = COALESCE(@AnnualPremium, NULLIF(line.EstPremium, 0)),
                         @EffectiveDate = line.TargetEffectiveDate
            FROM CRM.OpportunityLine line
            WHERE line.OpportunityId = @OpportunityId
              AND line.TenantId = @TenantId
              AND line.IsDeleted = 0
              AND (@SourceOpportunitySubmissionId IS NULL OR EXISTS
                  (
                      SELECT 1
                      FROM CRM.OpportunitySubmissionLine sl
                      WHERE sl.SubmissionId = @SourceOpportunitySubmissionId
                        AND sl.OpportunityLineId = line.OpportunityLineId
                        AND sl.IsDeleted = 0
                  ))
            ORDER BY line.IsPrimary DESC, line.EstPremium DESC, line.CreatedDateUtc DESC;

            SELECT @AnnualPremium = COALESCE(@AnnualPremium,
                (SELECT NULLIF(SUM(line.EstPremium), 0)
                 FROM CRM.OpportunityLine line
                 WHERE line.OpportunityId = @OpportunityId AND line.TenantId = @TenantId AND line.IsDeleted = 0));

            SET @LineOfBusiness = COALESCE(@LineOfBusiness, N'Package');
            SET @Priority = COALESCE(@Priority, N'High');
            SET @AnnualPremium = COALESCE(@AnnualPremium, NULLIF(@EstimatedAmount, 0), 0);
            SET @EffectiveDate = COALESCE(@EffectiveDate, @CloseDate, DATEADD(day, 30, CAST(SYSUTCDATETIME() AS date)));
            SET @ExpirationDate = DATEADD(year, 1, @EffectiveDate);

            SELECT TOP 1 @CarrierId = CarrierId
            FROM Core.Carrier WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId
              AND IsDeleted = 0
              AND IsActive = 1
              AND (@CarrierName IS NULL OR CarrierName = @CarrierName OR CarrierCode = @CarrierName)
            ORDER BY CASE WHEN @CarrierName IS NOT NULL AND (CarrierName = @CarrierName OR CarrierCode = @CarrierName) THEN 0 ELSE 1 END,
                     IsActive DESC,
                     CarrierName;

            IF @CarrierId IS NULL
                THROW 51002, 'A bound policy requires an active carrier. Add a carrier to the selected opportunity line before marking the opportunity won.', 1;

            SELECT TOP 1 @SubmissionId = SubmissionId
            FROM Submissions.Submission WITH (UPDLOCK, HOLDLOCK)
            WHERE TenantId = @TenantId
              AND OpportunityId = @OpportunityId
              AND IsDeleted = 0
              AND LineOfBusiness = @LineOfBusiness
            ORDER BY CASE WHEN Status = N'Bound' THEN 0 WHEN Status = N'Quotes Received' THEN 1 WHEN Status = N'Marketing' THEN 2 ELSE 3 END,
                     CreatedDateUtc DESC;

            IF @SubmissionId IS NULL
            BEGIN
                SET @SubmissionId = NEWID();

                IF @SubmissionNumber IS NULL
                    SET @SubmissionNumber = CONCAT(N'SUB-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(N'00000' + CAST(NEXT VALUE FOR Submissions.SubmissionSeq AS NVARCHAR(10)), 5));

                WHILE EXISTS (SELECT 1 FROM Submissions.Submission WHERE TenantId = @TenantId AND SubmissionNumber = @SubmissionNumber AND IsDeleted = 0)
                    SET @SubmissionNumber = CONCAT(N'SUB-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(N'00000' + CAST(NEXT VALUE FOR Submissions.SubmissionSeq AS NVARCHAR(10)), 5));

                INSERT INTO Submissions.Submission
                (
                    SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness,
                    Status, Priority, AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium,
                    MarketCount, QuoteCount, CreatedDateUtc, CreatedByUserId, IsDeleted
                )
                VALUES
                (
                    @SubmissionId, @TenantId, @AccountId, @OpportunityId, @SubmissionNumber, @LineOfBusiness,
                    N'Bound', @Priority, @OwnerUserId, @EffectiveDate, @ExpirationDate, @AnnualPremium,
                    1, 1, SYSUTCDATETIME(), @ModifiedByUserId, 0
                );
            END
            ELSE
            BEGIN
                UPDATE Submissions.Submission
                SET Status = N'Bound',
                    TargetPremium = COALESCE(TargetPremium, @AnnualPremium),
                    EffectiveDate = COALESCE(EffectiveDate, @EffectiveDate),
                    ExpirationDate = COALESCE(ExpirationDate, @ExpirationDate),
                    MarketCount = CASE WHEN MarketCount < 1 THEN 1 ELSE MarketCount END,
                    QuoteCount = CASE WHEN QuoteCount < 1 THEN 1 ELSE QuoteCount END,
                    ModifiedDateUtc = SYSUTCDATETIME(),
                    ModifiedByUserId = @ModifiedByUserId
                WHERE SubmissionId = @SubmissionId;
            END

            SELECT TOP 1 @SubmissionMarketId = SubmissionMarketId
            FROM Submissions.SubmissionMarket WITH (UPDLOCK, HOLDLOCK)
            WHERE SubmissionId = @SubmissionId
              AND CarrierId = @CarrierId
              AND IsDeleted = 0
            ORDER BY AddedDateUtc DESC;

            IF @SubmissionMarketId IS NULL
            BEGIN
                SET @SubmissionMarketId = NEWID();
                INSERT INTO Submissions.SubmissionMarket
                    (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, RespondedDateUtc, IsDeleted)
                VALUES
                    (@SubmissionMarketId, @SubmissionId, @CarrierId, N'Quoted', 100, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), 0);
            END
            ELSE
            BEGIN
                UPDATE Submissions.SubmissionMarket
                SET Status = CASE WHEN Status = N'Bound' THEN Status ELSE N'Quoted' END,
                    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME()),
                    ModifiedDateUtc = SYSUTCDATETIME(),
                    ModifiedByUserId = @ModifiedByUserId
                WHERE SubmissionMarketId = @SubmissionMarketId;
            END;

            SET @QuoteNumber = CONCAT(N'QT-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @QuoteId), N'-', N''), 6));

            INSERT INTO Submissions.QuoteRequest
                (QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
                 RequestedPremium, CoverageNotes, CarrierReferenceNumber, DeliveryMethodCode, AssignedUnderwriterName, AssignedUnderwriterEmail, AssignedUnderwriterPhone, DueDateUtc, CorrelationId, ResponseDateUtc,
                 RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, ClosedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (@QuoteRequestId, @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, N'InitialRequest', N'ManualUnderwriter', N'Package',
                 @AnnualPremium, CONCAT(N'Market quote response recorded from closed-won opportunity ', @OpportunityNumber, N'.'), @QuoteNumber,
                 N'ManualUnderwriter',
                 (SELECT TOP 1 UnderwriterName FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0),
                 (SELECT TOP 1 UnderwriterEmail FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0),
                 (SELECT TOP 1 UnderwriterPhone FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0),
                 (SELECT TOP 1 DueDateUtc FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0),
                 CONCAT(N'QR-', CONVERT(NVARCHAR(36), @QuoteRequestId)), SYSUTCDATETIME(),
                 COALESCE((SELECT MAX(RequestVersion) FROM Submissions.QuoteRequest WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0), 0) + 1,
                 N'Quoted', SYSUTCDATETIME(), @ModifiedByUserId, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);

            INSERT INTO Submissions.QuoteRequestHistory
                (QuoteRequestHistoryId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
                 RequestedPremium, CoverageNotes, RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (NEWID(), @TenantId, @SubmissionId, @SubmissionMarketId, @CarrierId, N'InitialRequest', N'ManualUnderwriter', N'Package',
                 @AnnualPremium, CONCAT(N'Market quote response recorded from closed-won opportunity ', @OpportunityNumber, N'.'),
                 (SELECT RequestVersion FROM Submissions.QuoteRequest WHERE QuoteRequestId = @QuoteRequestId), N'Quoted', SYSUTCDATETIME(), @ModifiedByUserId, SYSUTCDATETIME(), @ModifiedByUserId, 0);

            INSERT INTO Submissions.Quote
                (QuoteId, SubmissionId, SubmissionMarketId, QuoteRequestId, CarrierId, QuoteNumber, Status, AnnualPremium, Deductible, [Limit], CoverageNotes, QuotedDateUtc, ExpiresDateUtc, QuoteRequestDateUtc, QuoteReceivedDateUtc, ResponseVersion, ResponseSourceCode, CarrierReferenceNumber, CreatedDateUtc, IsDeleted)
            VALUES
                (@QuoteId, @SubmissionId, @SubmissionMarketId, @QuoteRequestId, @CarrierId, @QuoteNumber, N'Bound', @AnnualPremium, NULL, NULL, CONCAT(N'Market quote response recorded when opportunity ', @OpportunityNumber, N' was marked Closed Won.'), SYSUTCDATETIME(), DATEADD(day, 30, SYSUTCDATETIME()), SYSUTCDATETIME(), SYSUTCDATETIME(), 1, N'ManualEntry', @QuoteNumber, SYSUTCDATETIME(), 0);

            INSERT INTO Submissions.SubmissionLine
                (SubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LineOfBusiness, TargetPremium, CreatedDateUtc, CreatedByUserId, IsDeleted)
            SELECT NEWID(), line.TenantId, @SubmissionId, @OpportunityId, line.OpportunityLineId, line.LineOfBusiness, COALESCE(NULLIF(sl.TargetPremium, 0), line.EstPremium, 0), SYSUTCDATETIME(), @ModifiedByUserId, 0
            FROM CRM.OpportunityLine line
            LEFT JOIN CRM.OpportunitySubmissionLine sl ON sl.SubmissionId = @SourceOpportunitySubmissionId AND sl.OpportunityLineId = line.OpportunityLineId AND sl.IsDeleted = 0
            WHERE line.TenantId = @TenantId
              AND line.OpportunityId = @OpportunityId
              AND line.IsDeleted = 0
              AND (@SourceOpportunitySubmissionId IS NULL OR sl.OpportunitySubmissionLineId IS NOT NULL OR NOT EXISTS (SELECT 1 FROM CRM.OpportunitySubmissionLine sourceLine WHERE sourceLine.SubmissionId = @SourceOpportunitySubmissionId AND sourceLine.IsDeleted = 0))
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM Submissions.SubmissionLine existing
                  WHERE existing.SubmissionId = @SubmissionId
                    AND existing.OpportunityLineId = line.OpportunityLineId
                    AND existing.IsDeleted = 0
              );

            INSERT INTO Submissions.QuoteLine
                (QuoteLineId, TenantId, QuoteId, SubmissionId, SubmissionLineId, OpportunityLineId, LineOfBusiness, QuotedPremium,
                 IsBindable, CoverageNotes, Status, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
            SELECT NEWID(), submissionLine.TenantId, @QuoteId, @SubmissionId, submissionLine.SubmissionLineId, submissionLine.OpportunityLineId,
                   submissionLine.LineOfBusiness,
                   ROUND(CASE WHEN totals.TotalTargetPremium > 0 THEN @AnnualPremium * submissionLine.TargetPremium / totals.TotalTargetPremium
                              ELSE @AnnualPremium / NULLIF(totals.LineCount, 0) END, 2),
                   1, CONCAT(N'Bound line synchronized from opportunity ', @OpportunityNumber, N'.'), N'Bound',
                   ROW_NUMBER() OVER (ORDER BY submissionLine.LineOfBusiness, submissionLine.SubmissionLineId), SYSUTCDATETIME(), @ModifiedByUserId, 0
            FROM Submissions.SubmissionLine submissionLine
            CROSS APPLY
            (
                SELECT SUM(CASE WHEN candidate.TargetPremium > 0 THEN candidate.TargetPremium ELSE 0 END) AS TotalTargetPremium,
                       COUNT(1) AS LineCount
                FROM Submissions.SubmissionLine candidate
                WHERE candidate.SubmissionId = @SubmissionId AND candidate.TenantId = @TenantId AND candidate.IsDeleted = 0
            ) totals
            WHERE submissionLine.SubmissionId = @SubmissionId
              AND submissionLine.TenantId = @TenantId
              AND submissionLine.IsDeleted = 0;

            SET @PolicyId = NEWID();
            SET @PolicyNumber = CONCAT(N'POL-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(N'00000' + CAST(NEXT VALUE FOR Submissions.PolicySeq AS NVARCHAR(10)), 5));

            INSERT INTO Submissions.BoundPolicy
                (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId, PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IsDeleted)
            VALUES
                (@PolicyId, @SubmissionId, @QuoteId, @TenantId, @AccountId, @CarrierId, @PolicyNumber, N'Bound', @AnnualPremium, @EffectiveDate, @ExpirationDate, SYSUTCDATETIME(), 0);

            INSERT INTO Submissions.PolicyLine
                (PolicyLineId, TenantId, PolicyId, SubmissionId, QuoteId, OpportunityLineId, LineOfBusiness, WrittenPremium, Status, CreatedDateUtc, IsDeleted)
            SELECT NEWID(), line.TenantId, @PolicyId, @SubmissionId, @QuoteId, line.OpportunityLineId, line.LineOfBusiness, COALESCE(NULLIF(sl.TargetPremium, 0), line.EstPremium, 0), N'Bound', SYSUTCDATETIME(), 0
            FROM CRM.OpportunityLine line
            LEFT JOIN CRM.OpportunitySubmissionLine sl ON sl.SubmissionId = @SourceOpportunitySubmissionId AND sl.OpportunityLineId = line.OpportunityLineId AND sl.IsDeleted = 0
            WHERE line.TenantId = @TenantId
              AND line.OpportunityId = @OpportunityId
              AND line.IsDeleted = 0
              AND (@SourceOpportunitySubmissionId IS NULL OR sl.OpportunitySubmissionLineId IS NOT NULL OR NOT EXISTS (SELECT 1 FROM CRM.OpportunitySubmissionLine sourceLine WHERE sourceLine.SubmissionId = @SourceOpportunitySubmissionId AND sourceLine.IsDeleted = 0));

            INSERT INTO CRM.OpportunityBoundPolicy
                (OpportunityBoundPolicyId, TenantId, OpportunityId, OpportunitySubmissionId, SubmissionId, QuoteId, PolicyId, PolicyNumber, BindingStatus, BoundDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (NEWID(), @TenantId, @OpportunityId, @SourceOpportunitySubmissionId, @SubmissionId, @QuoteId, @PolicyId, @PolicyNumber, N'Bound', SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);

            UPDATE Submissions.Submission
            SET Status = N'Bound',
                QuoteCount = CASE WHEN QuoteCount < 1 THEN 1 ELSE QuoteCount END,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @ModifiedByUserId
            WHERE SubmissionId = @SubmissionId;

            UPDATE CRM.OpportunitySubmission
            SET Status = N'Bound',
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @ModifiedByUserId
            WHERE SubmissionId = @SourceOpportunitySubmissionId;

            INSERT INTO Submissions.SubmissionActionLog
                (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
            VALUES
                (NEWID(), @SubmissionId, @TenantId, N'OpportunityWonPolicyBound', CONCAT(N'Bound policy ', @PolicyNumber, N' created when opportunity ', @OpportunityNumber, N' was marked Closed Won.'), SYSUTCDATETIME(), 0);

            SET @PolicyCreated = 1;
            SET @Message = CONCAT(N'Opportunity marked Closed Won. Bound policy ', @PolicyNumber, N' was created.');
        END
    END
    ELSE IF @CreateBoundPolicy = 1 AND @Stage <> N'Closed Won'
    BEGIN
        SET @Message = CONCAT(N'Opportunity moved to ', @Stage, N'. Bound policy creation only runs for Closed Won opportunities.');
    END
    ELSE IF @CreateBoundPolicy = 0 AND @Stage = N'Closed Won'
    BEGIN
        SET @Message = N'Opportunity marked Closed Won. Policy binding was deferred and a follow-up action was created.';
    END

    IF OBJECT_ID(N'CRM.OpportunityWorkflowEvent', N'U') IS NOT NULL
    BEGIN
        INSERT INTO CRM.OpportunityWorkflowEvent
        (
            WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail,
            RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted
        )
        VALUES
        (
            NEWID(), @TenantId, @OpportunityId, N'Stage', CONCAT(N'Moved to ', @Stage), CONCAT(N'Opportunity stage changed to ', @Stage, N'.'),
            N'Opportunity', @OpportunityId, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0
        );

        IF @PolicyCreated = 1 OR @PolicyAlreadyExists = 1
        BEGIN
            UPDATE CRM.OpportunityWorkflowEvent
            SET IsDeleted = 1,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @ModifiedByUserId
            WHERE TenantId = @TenantId
              AND OpportunityId = @OpportunityId
              AND EventType = N'PolicyBindingRequired'
              AND IsDeleted = 0;

            INSERT INTO CRM.OpportunityWorkflowEvent
            (
                WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail,
                RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted
            )
            VALUES
            (
                NEWID(), @TenantId, @OpportunityId, N'PolicyBound', CASE WHEN @PolicyCreated = 1 THEN N'Bound policy created' ELSE N'Bound policy already exists' END, @Message,
                N'BoundPolicy', @PolicyId, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0
            );
        END

        IF @CreateBoundPolicy = 0 AND @Stage = N'Closed Won'
           AND NOT EXISTS
           (
               SELECT 1
               FROM CRM.OpportunityWorkflowEvent existing
               WHERE existing.OpportunityId = @OpportunityId
                 AND existing.TenantId = @TenantId
                 AND existing.EventType = N'PolicyBindingRequired'
                 AND existing.IsDeleted = 0
           )
        BEGIN
            INSERT INTO CRM.OpportunityWorkflowEvent
            (
                WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail,
                RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted
            )
            VALUES
            (
                NEWID(), @TenantId, @OpportunityId, N'PolicyBindingRequired', N'Policy binding required',
                N'Opportunity was marked Closed Won without creating a bound policy. Operations must create or bind the policy from the opportunity submission workflow.',
                N'Opportunity', @OpportunityId, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0
            );
        END
    END

    COMMIT TRANSACTION;

    SELECT @OpportunityId AS OpportunityId,
           @Stage AS Stage,
           @PolicyId AS PolicyId,
           @PolicyNumber AS PolicyNumber,
           @PolicyCreated AS PolicyCreated,
           @PolicyAlreadyExists AS PolicyAlreadyExists,
           @Message AS Message;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<OpportunityStageUpdateResult>(new CommandDefinition(sql, new
        {
            OpportunityId = id,
            request.Stage,
            request.ModifiedByUserId,
            request.CreateBoundPolicy
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> UpsertLineAsync(UpsertOpportunityLineRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OpportunityId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 51000, 'Opportunity was not found for the selected tenant.', 1;

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

UPDATE Submissions.SubmissionLine
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
IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OpportunityId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 51000, 'Opportunity was not found for the selected tenant.', 1;

IF @ActivityId IS NOT NULL AND NOT EXISTS
(
    SELECT 1
    FROM CRM.OpportunityActivity
    WHERE ActivityId = @ActivityId
      AND OpportunityId = @OpportunityId
      AND TenantId = @TenantId
      AND IsDeleted = 0
)
    THROW 51001, 'Activity does not belong to this opportunity.', 1;

IF @ActivityId IS NULL
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
        const string schemaSql = @"
IF OBJECT_ID(N'Submissions.Submission', N'U') IS NULL OR COL_LENGTH(N'Submissions.Submission', N'IsDeleted') IS NULL
    THROW 51010, 'Submissions schema is not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'CRM.OpportunitySubmissionLine', N'U') IS NULL OR COL_LENGTH(N'CRM.OpportunitySubmissionLine', N'IsDeleted') IS NULL
    THROW 51011, 'Opportunity submission line schema is not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'Submissions.SubmissionLine', N'U') IS NULL OR COL_LENGTH(N'Submissions.SubmissionLine', N'IsDeleted') IS NULL
    THROW 51012, 'Canonical submission line schema is not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'Submissions.SubmissionAutomationRule', N'U') IS NULL
    THROW 51013, 'Submission automation schema is not normalized. Run database migrations before creating submissions.', 1;

IF COL_LENGTH(N'Submissions.SubmissionAutomationRule', N'FollowUpTaskTypeCode') IS NULL
    THROW 51019, 'Submission automation rule columns are not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'Submissions.SubmissionMarketRule', N'U') IS NULL
    THROW 51014, 'Submission market automation schema is not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'Submissions.SubmissionDocumentChecklist', N'U') IS NULL
    THROW 51015, 'Submission document checklist schema is not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'DMS.Document', N'U') IS NULL
    THROW 51016, 'Document schema is not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'Submissions.SubmissionMarketDocument', N'U') IS NULL
    THROW 51017, 'Submission market document schema is not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'Submissions.SubmissionMarketDispatch', N'U') IS NULL
    THROW 51018, 'Submit-to-market dispatch schema is not normalized. Run database migrations before creating submissions.', 1;

IF OBJECT_ID(N'Agency.CarrierSetting', N'U') IS NULL
    THROW 51020, 'Carrier submit-to-market settings schema is not normalized. Run database migrations before creating submissions.', 1;

";

        const string sql = @"

DECLARE @Number NVARCHAR(50) = NULLIF(LTRIM(RTRIM(@SubmissionNumber)), N'');
DECLARE @AccountId UNIQUEIDENTIFIER;
DECLARE @OwnerUserId UNIQUEIDENTIFIER;
DECLARE @EffectiveDate DATETIME2;
DECLARE @ExpirationDate DATETIME2;
DECLARE @SubmissionStageId UNIQUEIDENTIFIER;

SELECT @AccountId = o.AccountId,
       @OwnerUserId = o.OwnerUserId,
       @EffectiveDate = COALESCE(primaryLine.TargetEffectiveDate, o.CloseDate, DATEADD(day, 30, CAST(SYSUTCDATETIME() AS date)))
FROM CRM.Opportunity o
OUTER APPLY
(
    SELECT TOP 1 line.TargetEffectiveDate
    FROM CRM.OpportunityLine line
    WHERE line.OpportunityId = o.OpportunityId
      AND line.IsDeleted = 0
      AND line.OpportunityLineId IN @OpportunityLineIds
    ORDER BY line.IsPrimary DESC, line.EstPremium DESC, line.CreatedDateUtc
) primaryLine
WHERE o.OpportunityId = @OpportunityId
  AND o.TenantId = @TenantId
  AND o.IsDeleted = 0;

IF @AccountId IS NULL
    THROW 51001, 'Opportunity was not found for submission creation.', 1;

IF @SubmissionId IS NOT NULL AND NOT EXISTS
(
    SELECT 1
    FROM CRM.OpportunitySubmission
    WHERE SubmissionId = @SubmissionId
      AND OpportunityId = @OpportunityId
      AND TenantId = @TenantId
      AND IsDeleted = 0
)
    THROW 51003, 'Submission does not belong to this opportunity.', 1;

SET @ExpirationDate = DATEADD(year, 1, @EffectiveDate);

IF @Number IS NULL
BEGIN
    DECLARE @NumberLockResult INT;
    DECLARE @NumberLockResource NVARCHAR(255) = CONCAT(N'OpportunitySubmissionNumber:', CONVERT(NVARCHAR(36), @TenantId));

    EXEC @NumberLockResult = sys.sp_getapplock
        @Resource = @NumberLockResource,
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 10000;

    IF @NumberLockResult < 0
        THROW 51002, 'Unable to reserve a submission number. Please retry.', 1;

    DECLARE @NextNumber INT = ISNULL((SELECT COUNT(1) FROM CRM.OpportunitySubmission WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND IsDeleted = 0), 0) + 1;
    SET @Number = CONCAT(N'SUB-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', FORMAT(@NextNumber, N'00000'));

    WHILE EXISTS (SELECT 1 FROM CRM.OpportunitySubmission WHERE TenantId = @TenantId AND SubmissionNumber = @Number AND IsDeleted = 0)
       OR EXISTS (SELECT 1 FROM Submissions.Submission WHERE TenantId = @TenantId AND SubmissionNumber = @Number)
    BEGIN
        SET @NextNumber += 1;
        SET @Number = CONCAT(N'SUB-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', FORMAT(@NextNumber, N'00000'));
    END;
END;

IF @SubmissionId IS NULL OR NOT EXISTS (SELECT 1 FROM CRM.OpportunitySubmission WHERE SubmissionId = @SubmissionId AND IsDeleted = 0)
BEGIN
    SET @SubmissionId = NEWID();
    INSERT INTO CRM.OpportunitySubmission (SubmissionId, TenantId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, TargetPremium, CreatedByUserId, CreatedDateUtc, IsDeleted)
    VALUES (@SubmissionId, @TenantId, @OpportunityId, @Number, @LineOfBusiness, @Status, @TargetPremium, @UserId, SYSUTCDATETIME(), 0);

    IF NOT EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = @SubmissionId)
    BEGIN
        INSERT INTO Submissions.Submission
        (
            SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness,
            Status, Priority, AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium,
            MarketCount, QuoteCount, CreatedDateUtc, CreatedByUserId
        )
        VALUES
        (
            @SubmissionId, @TenantId, @AccountId, @OpportunityId, @Number, @LineOfBusiness,
            @Status, N'Normal', @OwnerUserId, @EffectiveDate, @ExpirationDate, @TargetPremium,
            0, 0, SYSUTCDATETIME(), @UserId
        );
    END
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

    IF EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = @SubmissionId)
    BEGIN
        UPDATE Submissions.Submission
        SET LineOfBusiness = @LineOfBusiness,
            Status = @Status,
            TargetPremium = @TargetPremium,
            EffectiveDate = COALESCE(EffectiveDate, @EffectiveDate),
            ExpirationDate = COALESCE(ExpirationDate, @ExpirationDate),
            ModifiedByUserId = @UserId,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE SubmissionId = @SubmissionId;
    END
    ELSE
    BEGIN
        INSERT INTO Submissions.Submission
        (
            SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness,
            Status, Priority, AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium,
            MarketCount, QuoteCount, CreatedDateUtc, CreatedByUserId
        )
        VALUES
        (
            @SubmissionId, @TenantId, @AccountId, @OpportunityId, @Number, @LineOfBusiness,
            @Status, N'Normal', @OwnerUserId, @EffectiveDate, @ExpirationDate, @TargetPremium,
            0, 0, SYSUTCDATETIME(), @UserId
        );
    END
END

SELECT TOP 1 @SubmissionStageId = OpportunityStageId
FROM CRM.OpportunityStage
WHERE TenantId = @TenantId
  AND IsActive = 1
  AND (StageCode IN (N'SUBMISSION', N'MARKETING', N'PROPOSAL') OR StageName IN (N'Submission', N'Marketing', N'Proposal'))
ORDER BY CASE
    WHEN StageCode = N'SUBMISSION' OR StageName = N'Submission' THEN 0
    WHEN StageCode = N'MARKETING' OR StageName = N'Marketing' THEN 1
    ELSE 2
END, SortOrder, StageName;

UPDATE CRM.Opportunity
SET StageName = CASE
        WHEN StageName IN (N'Closed Won', N'Closed Lost') THEN StageName
        ELSE COALESCE((SELECT StageName FROM CRM.OpportunityStage WHERE OpportunityStageId = @SubmissionStageId), N'Submission')
    END,
    OpportunityStageId = CASE
        WHEN StageName IN (N'Closed Won', N'Closed Lost') THEN OpportunityStageId
        ELSE COALESCE(@SubmissionStageId, OpportunityStageId)
    END,
    ForecastCategoryCode = CASE
        WHEN ForecastCategoryCode IN (N'Closed', N'Won', N'Lost') THEN ForecastCategoryCode
        ELSE N'Pipeline'
    END,
    ModifiedByUserId = @UserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE OpportunityId = @OpportunityId
  AND TenantId = @TenantId
  AND IsDeleted = 0;

SELECT @SubmissionId;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var isNew = request.SubmissionId is null;
        var requestedLineIds = request.OpportunityLineIds?.Where(lineId => lineId != Guid.Empty).Distinct().ToArray() ?? [];
        using var tx = cn.BeginTransaction();
        try
        {
            await cn.ExecuteAsync(new CommandDefinition(schemaSql, transaction: tx, cancellationToken: cancellationToken));
            var id = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.SubmissionId, request.TenantId, request.OpportunityId, request.SubmissionNumber, request.LineOfBusiness, request.Status, request.TargetPremium, request.UserId, OpportunityLineIds = requestedLineIds }, tx, cancellationToken: cancellationToken));
            await SyncOpportunitySubmissionLinesAsync(cn, request, id, tx, cancellationToken);
            if (isNew)
                await SyncSubmissionPostCreateAutomationAsync(cn, id, request.TenantId, request.OpportunityId, request.UserId, tx, cancellationToken);
            await RecordWorkflowEventAsync(cn, request.OpportunityId, request.TenantId, isNew ? "Submission" : "SubmissionUpdated", isNew ? "Submission created" : "Submission updated", $"{request.LineOfBusiness} submission is {request.Status} with target premium {request.TargetPremium:C0}.", "Submission", id, request.UserId, tx, cancellationToken);
            tx.Commit();
            return id;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task DeleteSubmissionAsync(Guid submissionId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0)
   OR EXISTS (SELECT 1 FROM Submissions.QuoteRequest WHERE SubmissionId = @SubmissionId AND IsDeleted = 0)
   OR EXISTS (SELECT 1 FROM Submissions.Quote WHERE SubmissionId = @SubmissionId AND IsDeleted = 0)
   OR EXISTS (SELECT 1 FROM Submissions.BoundPolicy WHERE SubmissionId = @SubmissionId AND IsDeleted = 0)
    THROW 51004, 'Submission cannot be deleted after market, quote, or binding workflow has started.', 1;

BEGIN TRANSACTION;

UPDATE CRM.OpportunitySubmissionLine
SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;

UPDATE Submissions.SubmissionLine
SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;

UPDATE CRM.OpportunitySubmission
SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;

UPDATE Submissions.Submission
SET IsDeleted = 1, ModifiedByUserId = @ModifiedByUserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;

COMMIT TRANSACTION;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var target = await cn.QuerySingleOrDefaultAsync<WorkflowTarget>(new CommandDefinition("SELECT TenantId, OpportunityId FROM CRM.OpportunitySubmission WHERE SubmissionId = @SubmissionId;", new { SubmissionId = submissionId }, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
        if (target is not null)
            await RecordWorkflowEventAsync(cn, target.OpportunityId, target.TenantId, "SubmissionDeleted", "Submission deleted", "An opportunity submission was deleted.", "Submission", submissionId, modifiedByUserId, cancellationToken);
    }

    public async Task<Guid> UpsertCompetitorAsync(UpsertOpportunityCompetitorRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE OpportunityId = @OpportunityId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 51000, 'Opportunity was not found for the selected tenant.', 1;

IF @CompetitorId IS NOT NULL AND NOT EXISTS
(
    SELECT 1
    FROM CRM.OpportunityCompetitor
    WHERE CompetitorId = @CompetitorId
      AND OpportunityId = @OpportunityId
      AND TenantId = @TenantId
      AND IsDeleted = 0
)
    THROW 51001, 'Competitor does not belong to this opportunity.', 1;

IF @CompetitorId IS NULL
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
        => await RecordWorkflowEventAsync(cn, opportunityId, tenantId, eventType, eventTitle, eventDetail, relatedEntityName, relatedEntityId, userId, null, cancellationToken);

    private static async Task RecordWorkflowEventAsync(IDbConnection cn, Guid opportunityId, Guid? tenantId, string eventType, string eventTitle, string? eventDetail, string? relatedEntityName, Guid? relatedEntityId, Guid? userId, IDbTransaction? transaction, CancellationToken cancellationToken)
    {
        var resolvedTenantId = tenantId ?? await cn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT TenantId FROM CRM.Opportunity WHERE OpportunityId = @OpportunityId;",
            new { OpportunityId = opportunityId }, transaction, cancellationToken: cancellationToken));

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
        }, transaction, cancellationToken: cancellationToken));
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

    private static async Task SyncOpportunitySubmissionLinesAsync(IDbConnection cn, UpsertOpportunitySubmissionRequest request, Guid submissionId, IDbTransaction transaction, CancellationToken cancellationToken)
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
                new { request.OpportunityId, request.LineOfBusiness }, transaction, cancellationToken: cancellationToken))).ToArray();
        }

        if (requestedLineIds.Length == 0)
            return;

        const string softDeleteSql = @"
UPDATE CRM.OpportunitySubmissionLine
SET IsDeleted = 1, ModifiedByUserId = @UserId, ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId
  AND IsDeleted = 0
  AND OpportunityLineId NOT IN @OpportunityLineIds;";

        await cn.ExecuteAsync(new CommandDefinition(softDeleteSql, new { SubmissionId = submissionId, OpportunityLineIds = requestedLineIds, request.UserId }, transaction, cancellationToken: cancellationToken));

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

        await cn.ExecuteAsync(new CommandDefinition(upsertSql, new { SubmissionId = submissionId, request.OpportunityId, OpportunityLineIds = requestedLineIds, SelectedLineCount = requestedLineIds.Length, request.TargetPremium, request.UserId }, transaction, cancellationToken: cancellationToken));

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

        await cn.ExecuteAsync(new CommandDefinition(summarySql, new { SubmissionId = submissionId, request.UserId }, transaction, cancellationToken: cancellationToken));

        const string canonicalLineSyncSql = @"
UPDATE Submissions.SubmissionLine
SET IsDeleted = 1,
    ModifiedByUserId = @UserId,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId
  AND IsDeleted = 0
  AND OpportunityLineId NOT IN @OpportunityLineIds;

MERGE Submissions.SubmissionLine AS target
USING
(
    SELECT line.TenantId, @SubmissionId AS SubmissionId, line.OpportunityId, line.OpportunityLineId, line.LineOfBusiness, line.TargetPremium
    FROM CRM.OpportunitySubmissionLine line
    WHERE line.SubmissionId = @SubmissionId
      AND line.IsDeleted = 0
) AS source
ON target.SubmissionId = source.SubmissionId AND target.OpportunityLineId = source.OpportunityLineId
WHEN MATCHED THEN UPDATE SET
    target.LineOfBusiness = source.LineOfBusiness,
    target.TargetPremium = source.TargetPremium,
    target.IsDeleted = 0,
    target.ModifiedByUserId = @UserId,
    target.ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (SubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LineOfBusiness, TargetPremium, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), source.TenantId, source.SubmissionId, source.OpportunityId, source.OpportunityLineId, source.LineOfBusiness, source.TargetPremium, SYSUTCDATETIME(), @UserId, 0);

UPDATE Submissions.Submission
SET LineOfBusiness = crm.LineOfBusiness,
    TargetPremium = crm.TargetPremium,
    ModifiedByUserId = @UserId,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM CRM.OpportunitySubmission crm
WHERE Submissions.Submission.SubmissionId = crm.SubmissionId
  AND crm.SubmissionId = @SubmissionId
  AND Submissions.Submission.IsDeleted = 0;";

        await cn.ExecuteAsync(new CommandDefinition(canonicalLineSyncSql, new { SubmissionId = submissionId, OpportunityLineIds = requestedLineIds, request.UserId }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task SyncSubmissionPostCreateAutomationAsync(IDbConnection cn, Guid submissionId, Guid tenantId, Guid opportunityId, Guid? userId, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @LineOfBusiness NVARCHAR(100);
DECLARE @SubmissionNumber NVARCHAR(50);
DECLARE @AccountId UNIQUEIDENTIFIER;
DECLARE @AssignedToUserId UNIQUEIDENTIFIER;
DECLARE @OpportunityOwnerUserId UNIQUEIDENTIFIER;
DECLARE @CreatedByUserId UNIQUEIDENTIFIER;

SELECT @LineOfBusiness = s.LineOfBusiness,
       @SubmissionNumber = s.SubmissionNumber,
       @AccountId = s.AccountId,
       @AssignedToUserId = s.AssignedToUserId,
       @CreatedByUserId = s.CreatedByUserId
FROM Submissions.Submission s
WHERE s.SubmissionId = @SubmissionId
  AND s.TenantId = @TenantId
  AND s.IsDeleted = 0;

SELECT @OpportunityOwnerUserId = o.OwnerUserId
FROM CRM.Opportunity o
WHERE o.OpportunityId = @OpportunityId
  AND o.TenantId = @TenantId
  AND o.IsDeleted = 0;

IF @LineOfBusiness IS NULL
    RETURN;

DECLARE @Rule TABLE
(
    AutomationRuleId UNIQUEIDENTIFIER NOT NULL,
    AutoCreateDocuments BIT NOT NULL,
    AutoSelectMarkets BIT NOT NULL,
    AutoSubmitToMarket BIT NOT NULL,
    AutoAssignOwner BIT NOT NULL,
    AutoCreateFollowUpTask BIT NOT NULL,
    DefaultOwnerStrategy NVARCHAR(80) NOT NULL,
    FollowUpTaskTitle NVARCHAR(200) NOT NULL,
    FollowUpTaskDescription NVARCHAR(1000) NULL,
    FollowUpTaskTypeCode NVARCHAR(80) NOT NULL,
    FollowUpTaskStageCode NVARCHAR(50) NOT NULL,
    FollowUpTaskStatusCode NVARCHAR(50) NOT NULL,
    FollowUpTaskPriorityCode NVARCHAR(50) NOT NULL,
    FollowUpTaskDueDays INT NOT NULL
);

INSERT INTO @Rule
SELECT TOP 1 r.AutomationRuleId,
       r.AutoCreateDocuments,
       r.AutoSelectMarkets,
       r.AutoSubmitToMarket,
       r.AutoAssignOwner,
       r.AutoCreateFollowUpTask,
       r.DefaultOwnerStrategy,
       r.FollowUpTaskTitle,
       r.FollowUpTaskDescription,
       r.FollowUpTaskTypeCode,
       r.FollowUpTaskStageCode,
       r.FollowUpTaskStatusCode,
       r.FollowUpTaskPriorityCode,
       r.FollowUpTaskDueDays
FROM Submissions.SubmissionAutomationRule r
WHERE r.TenantId = @TenantId
  AND r.IsDeleted = 0
  AND r.IsActive = 1
  AND (r.LineOfBusiness = @LineOfBusiness OR r.LineOfBusiness = N'*')
ORDER BY CASE WHEN r.LineOfBusiness = @LineOfBusiness THEN 0 ELSE 1 END,
         CASE WHEN r.RuleCode = N'DEFAULT_POST_CREATE' THEN 0 ELSE 1 END,
         r.CreatedDateUtc DESC;

IF NOT EXISTS (SELECT 1 FROM @Rule)
    RETURN;

DECLARE @ResolvedOwnerUserId UNIQUEIDENTIFIER = COALESCE(@AssignedToUserId, @OpportunityOwnerUserId, @UserId, @CreatedByUserId);

IF EXISTS (SELECT 1 FROM @Rule WHERE AutoAssignOwner = 1) AND @ResolvedOwnerUserId IS NOT NULL
BEGIN
    UPDATE Submissions.Submission
    SET AssignedToUserId = COALESCE(AssignedToUserId, @ResolvedOwnerUserId),
        ModifiedByUserId = @UserId,
        ModifiedDateUtc = SYSUTCDATETIME()
    WHERE SubmissionId = @SubmissionId
      AND TenantId = @TenantId
      AND IsDeleted = 0;

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'CRM.OpportunitySubmission') AND name = N'AssignedToUserId')
    BEGIN
        EXEC sp_executesql N'
        UPDATE CRM.OpportunitySubmission
        SET AssignedToUserId = COALESCE(AssignedToUserId, @ResolvedOwnerUserId),
            ModifiedByUserId = @UserId,
            ModifiedDateUtc = SYSUTCDATETIME()
        WHERE SubmissionId = @SubmissionId
          AND TenantId = @TenantId
          AND IsDeleted = 0;',
        N'@SubmissionId UNIQUEIDENTIFIER, @TenantId UNIQUEIDENTIFIER, @ResolvedOwnerUserId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER',
        @SubmissionId = @SubmissionId, @TenantId = @TenantId, @ResolvedOwnerUserId = @ResolvedOwnerUserId, @UserId = @UserId;
    END;
END;

IF EXISTS (SELECT 1 FROM @Rule WHERE AutoCreateDocuments = 1)
BEGIN
    INSERT INTO Submissions.SubmissionDocumentChecklist
        (DocumentChecklistId, SubmissionId, TenantId, DocumentRequirementId, CategoryCode, DisplayName, IsRequired, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @SubmissionId, @TenantId, req.DocumentRequirementId, req.CategoryCode, req.DisplayName, req.IsRequired, N'Needed', SYSUTCDATETIME(), @UserId, 0
    FROM Submissions.SubmissionDocumentRequirement req
    WHERE req.TenantId = @TenantId
      AND req.IsDeleted = 0
      AND req.IsActive = 1
      AND (req.LineOfBusiness = @LineOfBusiness OR req.LineOfBusiness = N'*')
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.SubmissionDocumentChecklist existing
          WHERE existing.SubmissionId = @SubmissionId
            AND existing.CategoryCode = req.CategoryCode
            AND existing.IsDeleted = 0
      );

    INSERT INTO DMS.Document
        (DocumentId, TenantId, DocumentTypeCode, CategoryCode, EntityName, EntityId, FileName, StoragePath, ContentType, FileSizeBytes, VersionNumber, StatusCode, Description, Tags, UploadedByName, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, N'SubmissionRequirement', c.CategoryCode, N'Submission', @SubmissionId,
           LEFT(CONCAT(@SubmissionNumber, N' - ', c.DisplayName, N'.placeholder'), 260),
           N'', NULL, 0, 1, N'Needed', CONCAT(N'Placeholder generated from submission document requirement: ', c.DisplayName), N'Submission,Required', N'System', SYSUTCDATETIME(), @UserId, 0
    FROM Submissions.SubmissionDocumentChecklist c
    WHERE c.SubmissionId = @SubmissionId
      AND c.TenantId = @TenantId
      AND c.IsDeleted = 0
      AND c.DocumentId IS NULL
      AND NOT EXISTS
      (
          SELECT 1
          FROM DMS.Document existing
          WHERE existing.TenantId = @TenantId
            AND existing.EntityName = N'Submission'
            AND existing.EntityId = @SubmissionId
            AND existing.CategoryCode = c.CategoryCode
            AND existing.IsDeleted = 0
      );

    UPDATE c
    SET DocumentId = d.DocumentId,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @UserId
    FROM Submissions.SubmissionDocumentChecklist c
    INNER JOIN DMS.Document d ON d.TenantId = c.TenantId
        AND d.EntityName = N'Submission'
        AND d.EntityId = c.SubmissionId
        AND d.CategoryCode = c.CategoryCode
        AND d.IsDeleted = 0
    WHERE c.SubmissionId = @SubmissionId
      AND c.TenantId = @TenantId
      AND c.IsDeleted = 0
      AND c.DocumentId IS NULL;
END;

IF EXISTS (SELECT 1 FROM @Rule WHERE AutoSelectMarkets = 1)
BEGIN
    INSERT INTO Submissions.SubmissionMarket
        (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, IsDeleted, TenantId, Notes, NextActionDateUtc, SubmittedDateUtc, SubmittedByUserId, CreatedByUserId)
    SELECT NEWID(), @SubmissionId, mr.CarrierId,
           CASE WHEN automationRule.AutoSubmitToMarket = 1 AND mr.SubmitByDefault = 1 THEN N'Submitted' ELSE N'Pending' END,
           mr.AppetiteScore,
           mr.IsRecommended,
           SYSUTCDATETIME(),
           0,
           @TenantId,
           mr.Notes,
           DATEADD(day, 3, SYSUTCDATETIME()),
           CASE WHEN automationRule.AutoSubmitToMarket = 1 AND mr.SubmitByDefault = 1 THEN SYSUTCDATETIME() ELSE NULL END,
           CASE WHEN automationRule.AutoSubmitToMarket = 1 AND mr.SubmitByDefault = 1 THEN @UserId ELSE NULL END,
           @UserId
    FROM Submissions.SubmissionMarketRule mr
    CROSS JOIN @Rule automationRule
    WHERE mr.TenantId = @TenantId
      AND mr.IsDeleted = 0
      AND mr.IsActive = 1
      AND (mr.LineOfBusiness = @LineOfBusiness OR mr.LineOfBusiness = N'*')
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.SubmissionMarket existing
          WHERE existing.SubmissionId = @SubmissionId
            AND existing.CarrierId = mr.CarrierId
            AND existing.IsDeleted = 0
      );

    UPDATE s
    SET MarketCount = marketSummary.MarketCount,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @UserId
    FROM Submissions.Submission s
    CROSS APPLY (SELECT COUNT(1) AS MarketCount FROM Submissions.SubmissionMarket sm WHERE sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0) marketSummary
    WHERE s.SubmissionId = @SubmissionId
      AND s.TenantId = @TenantId;
END;

IF EXISTS (SELECT 1 FROM @Rule WHERE AutoCreateDocuments = 1)
BEGIN
    INSERT INTO Submissions.SubmissionMarketDocument
        (SubmissionMarketDocumentId, SubmissionMarketId, SubmissionId, TenantId, DocumentId, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), sm.SubmissionMarketId, @SubmissionId, @TenantId, d.DocumentId, SYSUTCDATETIME(), @UserId, 0
    FROM Submissions.SubmissionMarket sm
    INNER JOIN DMS.Document d ON d.TenantId = @TenantId AND d.EntityName = N'Submission' AND d.EntityId = @SubmissionId AND d.IsDeleted = 0
    WHERE sm.SubmissionId = @SubmissionId
      AND sm.IsDeleted = 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.SubmissionMarketDocument existing
          WHERE existing.SubmissionMarketId = sm.SubmissionMarketId
            AND existing.DocumentId = d.DocumentId
            AND existing.IsDeleted = 0
      );
END;

IF EXISTS (SELECT 1 FROM @Rule WHERE AutoSubmitToMarket = 1)
BEGIN
    IF OBJECT_ID(N'Core.Carrier', N'U') IS NOT NULL
    BEGIN
        EXEC sp_executesql N'
        INSERT INTO Submissions.SubmissionActionLog
            (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource)
        SELECT NEWID(), @SubmissionId, @TenantId, N''SubmitToMarket'', CONCAT(N''Automatically submitted to '', COALESCE(c.CarrierName, N''selected market''), N'' from post-create automation.''), SYSUTCDATETIME(), 0, @UserId, N''SubmissionMarket'', sm.SubmissionMarketId, N''PostCreateAutomation''
        FROM Submissions.SubmissionMarket sm
        LEFT JOIN Core.Carrier c ON c.CarrierId = sm.CarrierId
        WHERE sm.SubmissionId = @SubmissionId
          AND sm.IsDeleted = 0
          AND sm.Status IN (N''Submitted'', N''Awaiting Response'', N''In Review'', N''Under Review'')
          AND NOT EXISTS
          (
              SELECT 1
              FROM Submissions.SubmissionActionLog existing
              WHERE existing.SubmissionId = @SubmissionId
                AND existing.ActionCode = N''SubmitToMarket''
                AND existing.RelatedEntityId = sm.SubmissionMarketId
                AND existing.IsDeleted = 0
          );',
        N'@SubmissionId UNIQUEIDENTIFIER, @TenantId UNIQUEIDENTIFIER, @UserId UNIQUEIDENTIFIER',
        @SubmissionId = @SubmissionId, @TenantId = @TenantId, @UserId = @UserId;
    END
    ELSE
    BEGIN
        INSERT INTO Submissions.SubmissionActionLog
            (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource)
        SELECT NEWID(), @SubmissionId, @TenantId, N'SubmitToMarket', N'Automatically submitted to selected market from post-create automation.', SYSUTCDATETIME(), 0, @UserId, N'SubmissionMarket', sm.SubmissionMarketId, N'PostCreateAutomation'
        FROM Submissions.SubmissionMarket sm
        WHERE sm.SubmissionId = @SubmissionId
          AND sm.IsDeleted = 0
          AND sm.Status IN (N'Submitted', N'Awaiting Response', N'In Review', N'Under Review')
          AND NOT EXISTS
          (
              SELECT 1
              FROM Submissions.SubmissionActionLog existing
              WHERE existing.SubmissionId = @SubmissionId
                AND existing.ActionCode = N'SubmitToMarket'
                AND existing.RelatedEntityId = sm.SubmissionMarketId
                AND existing.IsDeleted = 0
          );
    END;

    INSERT INTO Submissions.SubmissionMarketDispatch
        (SubmissionMarketDispatchId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, DispatchChannelCode, DispatchStatusCode, Recipient, Subject, PayloadJson, AttemptCount, MaxAttemptCount, NextAttemptDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, @SubmissionId, sm.SubmissionMarketId, sm.CarrierId,
           CASE WHEN COALESCE(NULLIF(sm.SubmissionMethodCode, N''), NULLIF(carrierChannel.SettingValue, N''), NULLIF(carrierChannel.DefaultValue, N''), deliveryChannel.ChannelCode, NULLIF(defaultChannel.SettingValue, N''), NULLIF(defaultChannel.DefaultValue, N''), N'InternalQueue') = N'CarrierApi' THEN N'API' ELSE COALESCE(NULLIF(sm.SubmissionMethodCode, N''), NULLIF(carrierChannel.SettingValue, N''), NULLIF(carrierChannel.DefaultValue, N''), deliveryChannel.ChannelCode, NULLIF(defaultChannel.SettingValue, N''), NULLIF(defaultChannel.DefaultValue, N''), N'InternalQueue') END,
           N'Pending',
           COALESCE(NULLIF(carrierEmail.SettingValue, N''), NULLIF(carrierEmail.DefaultValue, N''), NULLIF(deliveryEmail.SettingValue, N''), NULLIF(deliveryEmail.DefaultValue, N''), NULLIF(carrierPortal.SettingValue, N''), NULLIF(carrierPortal.DefaultValue, N''), NULLIF(deliveryPortal.SettingValue, N''), NULLIF(deliveryPortal.DefaultValue, N''), NULLIF(defaultRecipient.SettingValue, N''), NULLIF(defaultRecipient.DefaultValue, N'')),
           LEFT(REPLACE(COALESCE(NULLIF(deliverySubject.SettingValue, N''), NULLIF(deliverySubject.DefaultValue, N''), NULLIF(subjectTemplate.SettingValue, N''), NULLIF(subjectTemplate.DefaultValue, N''), N'Submission {SubmissionNumber} ready for market review'), N'{SubmissionNumber}', COALESCE(@SubmissionNumber, N'')), 300),
           CONCAT(N'{',
               N'""tenantId"":""', CONVERT(NVARCHAR(36), @TenantId), N'"",',
               N'""submissionId"":""', CONVERT(NVARCHAR(36), @SubmissionId), N'"",',
               N'""submissionMarketId"":""', CONVERT(NVARCHAR(36), sm.SubmissionMarketId), N'"",',
               N'""carrierId"":""', CONVERT(NVARCHAR(36), sm.CarrierId), N'"",',
               N'""submissionNumber"":""', STRING_ESCAPE(COALESCE(@SubmissionNumber, N''), 'json'), N'"",',
               N'""lineOfBusiness"":""', STRING_ESCAPE(COALESCE(@LineOfBusiness, N''), 'json'), N'"",',
               N'""documentIds"":', COALESCE(documentPayload.DocumentIdsJson, N'[]'),
           N'}'),
           0, COALESCE(TRY_CONVERT(INT, COALESCE(NULLIF(maxAttempts.SettingValue, N''), NULLIF(maxAttempts.DefaultValue, N''))), 3), SYSUTCDATETIME(), SYSUTCDATETIME(), @UserId, 0
    FROM Submissions.SubmissionMarket sm
    OUTER APPLY
    (
        SELECT CONCAT(N'[', STRING_AGG(CONCAT(N'""', CONVERT(NVARCHAR(36), d.DocumentId), N'""'), N','), N']') AS DocumentIdsJson
        FROM Submissions.SubmissionMarketDocument d
        WHERE d.SubmissionMarketId = sm.SubmissionMarketId
          AND d.IsDeleted = 0
    ) documentPayload
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = sm.CarrierId AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_CHANNEL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) carrierChannel
    OUTER APPLY (SELECT TOP 1 ChannelCode = CASE WHEN ISJSON(COALESCE(SettingValue, DefaultValue)) = 1 AND JSON_VALUE(COALESCE(SettingValue, DefaultValue), '$[0]') IS NOT NULL THEN JSON_VALUE(COALESCE(SettingValue, DefaultValue), '$[0]') ELSE COALESCE(NULLIF(SettingValue, N''), NULLIF(DefaultValue, N'')) END FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = sm.CarrierId AND SettingCode = N'CARRIER_DELIVERY_CHANNEL_PRIORITY' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) deliveryChannel
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_CHANNEL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) defaultChannel
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = sm.CarrierId AND SettingCode = N'SUBMIT_TO_MARKET_EMAIL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) carrierEmail
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = sm.CarrierId AND SettingCode = N'CARRIER_DELIVERY_EMAIL_TO' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) deliveryEmail
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = sm.CarrierId AND SettingCode = N'SUBMIT_TO_MARKET_PORTAL_URL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) carrierPortal
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = sm.CarrierId AND SettingCode = N'CARRIER_DELIVERY_PORTAL_URL' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) deliveryPortal
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_RECIPIENT' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) defaultRecipient
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_SUBJECT_TEMPLATE' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) subjectTemplate
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = sm.CarrierId AND SettingCode = N'CARRIER_DELIVERY_EMAIL_SUBJECT_TEMPLATE' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) deliverySubject
    OUTER APPLY (SELECT TOP 1 SettingValue, DefaultValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_MAX_ATTEMPTS' AND IsActive = 1 AND IsDeleted = 0 ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC) maxAttempts
    WHERE sm.SubmissionId = @SubmissionId
      AND sm.IsDeleted = 0
      AND sm.Status IN (N'Submitted', N'Awaiting Response', N'In Review', N'Under Review')
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.SubmissionMarketDispatch existing
          WHERE existing.SubmissionMarketId = sm.SubmissionMarketId
            AND existing.IsDeleted = 0
      );
END;

IF OBJECT_ID(N'OPS.TaskItem', N'U') IS NOT NULL AND EXISTS (SELECT 1 FROM @Rule WHERE AutoCreateFollowUpTask = 1)
BEGIN
    DECLARE @TaskNumber NVARCHAR(50) = CONCAT(N'TASK-', FORMAT(SYSUTCDATETIME(), N'yyyyMMddHHmmss'), N'-', RIGHT(CONVERT(NVARCHAR(36), @SubmissionId), 6));

    INSERT INTO OPS.TaskItem
        (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CompletedDate, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, @TaskNumber, automationRule.FollowUpTaskTitle,
           COALESCE(automationRule.FollowUpTaskDescription, CONCAT(N'Follow up on submission ', @SubmissionNumber, N'.')),
           automationRule.FollowUpTaskTypeCode, automationRule.FollowUpTaskStageCode, automationRule.FollowUpTaskPriorityCode, automationRule.FollowUpTaskStatusCode, N'Submission', @SubmissionId, @AccountId, @ResolvedOwnerUserId,
           DATEADD(day, automationRule.FollowUpTaskDueDays, CAST(SYSUTCDATETIME() AS date)), NULL, SYSUTCDATETIME(), @UserId, NULL, NULL, 0
    FROM @Rule automationRule
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM OPS.TaskItem existing
        WHERE existing.TenantId = @TenantId
          AND existing.RelatedEntityName = N'Submission'
          AND existing.RelatedEntityId = @SubmissionId
          AND existing.TaskTypeCode = automationRule.FollowUpTaskTypeCode
          AND existing.IsDeleted = 0
    );
END;

INSERT INTO Submissions.SubmissionActionLog
    (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource)
SELECT NEWID(), @SubmissionId, @TenantId, N'PostCreateAutomation', N'Post-create automation synchronized documents, markets, submit-to-market actions, owner assignment, and follow-up work from database configuration.', SYSUTCDATETIME(), 0, @UserId, N'Submission', @SubmissionId, N'PostCreateAutomation'
WHERE NOT EXISTS
(
    SELECT 1
    FROM Submissions.SubmissionActionLog existing
    WHERE existing.SubmissionId = @SubmissionId
      AND existing.ActionCode = N'PostCreateAutomation'
      AND existing.IsDeleted = 0
);";

        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId, OpportunityId = opportunityId, UserId = userId }, transaction, cancellationToken: cancellationToken));
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
