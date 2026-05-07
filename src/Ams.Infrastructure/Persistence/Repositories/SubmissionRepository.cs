using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Submissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SubmissionRepository : ISubmissionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public SubmissionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    // ── Submission Register ───────────────────────────────────────────

    private const string SubmissionColumns = @"
        s.SubmissionId, s.TenantId, s.AccountId, a.AccountName, s.OpportunityId, o.OpportunityName,
        s.SubmissionNumber, s.LineOfBusiness, s.Status, s.Priority,
        s.AssignedToUserId, u.FullName AS AssignedToUserName,
        s.EffectiveDate, s.ExpirationDate, s.TargetPremium,
        s.MarketCount, s.QuoteCount, s.CreatedDateUtc, s.ModifiedDateUtc";

    public async Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT o.OpportunityId AS SubmissionId,
           o.TenantId,
           o.AccountId,
           a.AccountName,
           o.OpportunityId,
           o.OpportunityName,
           'SUB-' + RIGHT(REPLACE(CONVERT(varchar(36), o.OpportunityId), '-', ''), 8) AS SubmissionNumber,
           COALESCE(NULLIF(o.ForecastCategoryCode, ''), 'General Liability') AS LineOfBusiness,
           CASE
               WHEN o.Stage = 'Closed Won' OR o.StatusCodeId = 5 THEN 'Bound'
               WHEN o.Stage = 'Closed Lost' OR o.StatusCodeId = 6 THEN 'Declined'
               WHEN o.Stage IN ('Proposal', 'Negotiation') THEN 'Quoted'
               WHEN o.Stage = 'Needs Analysis' THEN 'In Review'
               ELSE 'New'
           END AS Status,
           CASE
               WHEN o.EstimatedAmount >= 100000 THEN 'High'
               WHEN o.EstimatedAmount >= 25000 THEN 'Normal'
               ELSE 'Low'
           END AS Priority,
           o.OwnerUserId AS AssignedToUserId,
           NULL AS AssignedToUserName,
           CAST(COALESCE(o.CloseDate, o.EstimatedCloseDate, DATEADD(day, 30, SYSUTCDATETIME())) AS datetime2) AS EffectiveDate,
           DATEADD(year, 1, CAST(COALESCE(o.CloseDate, o.EstimatedCloseDate, DATEADD(day, 30, SYSUTCDATETIME())) AS datetime2)) AS ExpirationDate,
           o.EstimatedAmount AS TargetPremium,
           0 AS MarketCount,
           (SELECT COUNT(1) FROM CRM.Quote q WHERE q.OpportunityId = o.OpportunityId AND q.IsDeleted = 0) AS QuoteCount,
           o.CreatedDateUtc,
           o.ModifiedDateUtc
    FROM   CRM.Opportunity o
    JOIN   Client.Account a ON a.AccountId = o.AccountId
    WHERE  o.TenantId = @TenantId
      AND  o.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR o.OpportunityName LIKE '%' + @SearchTerm + '%' OR o.OpportunityNumber LIKE '%' + @SearchTerm + '%' OR a.AccountName LIKE '%' + @SearchTerm + '%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = '' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = '' OR LineOfBusiness = @LineOfBusiness)
)
SELECT * FROM Filtered
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

;WITH Cte AS
(
    SELECT COALESCE(NULLIF(o.ForecastCategoryCode, ''), 'General Liability') AS LineOfBusiness,
           CASE
               WHEN o.Stage = 'Closed Won' OR o.StatusCodeId = 5 THEN 'Bound'
               WHEN o.Stage = 'Closed Lost' OR o.StatusCodeId = 6 THEN 'Declined'
               WHEN o.Stage IN ('Proposal', 'Negotiation') THEN 'Quoted'
               WHEN o.Stage = 'Needs Analysis' THEN 'In Review'
               ELSE 'New'
           END AS Status
    FROM   CRM.Opportunity o
    JOIN   Client.Account a ON a.AccountId = o.AccountId
    WHERE  o.TenantId = @TenantId
      AND  o.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR o.OpportunityName LIKE '%' + @SearchTerm + '%' OR o.OpportunityNumber LIKE '%' + @SearchTerm + '%' OR a.AccountName LIKE '%' + @SearchTerm + '%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = '' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = '' OR LineOfBusiness = @LineOfBusiness)
)
SELECT COUNT(1) FROM Filtered;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId       = tenantId,
            SearchTerm     = searchTerm,
            Status         = status,
            LineOfBusiness = lineOfBusiness,
            Offset         = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize       = pageSize,
        }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SubmissionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SubmissionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT o.OpportunityId AS SubmissionId,
       o.TenantId,
       o.AccountId,
       a.AccountName,
       o.OpportunityId,
       o.OpportunityName,
       'SUB-' + RIGHT(REPLACE(CONVERT(varchar(36), o.OpportunityId), '-', ''), 8) AS SubmissionNumber,
       COALESCE(NULLIF(o.ForecastCategoryCode, ''), 'General Liability') AS LineOfBusiness,
       CASE
           WHEN o.Stage = 'Closed Won' OR o.StatusCodeId = 5 THEN 'Bound'
           WHEN o.Stage = 'Closed Lost' OR o.StatusCodeId = 6 THEN 'Declined'
           WHEN o.Stage IN ('Proposal', 'Negotiation') THEN 'Quoted'
           WHEN o.Stage = 'Needs Analysis' THEN 'In Review'
           ELSE 'New'
       END AS Status,
       CASE
           WHEN o.EstimatedAmount >= 100000 THEN 'High'
           WHEN o.EstimatedAmount >= 25000 THEN 'Normal'
           ELSE 'Low'
       END AS Priority,
       o.OwnerUserId AS AssignedToUserId,
       NULL AS AssignedToUserName,
       CAST(COALESCE(o.CloseDate, o.EstimatedCloseDate, DATEADD(day, 30, SYSUTCDATETIME())) AS datetime2) AS EffectiveDate,
       DATEADD(year, 1, CAST(COALESCE(o.CloseDate, o.EstimatedCloseDate, DATEADD(day, 30, SYSUTCDATETIME())) AS datetime2)) AS ExpirationDate,
       o.EstimatedAmount AS TargetPremium,
       0 AS MarketCount,
       (SELECT COUNT(1) FROM CRM.Quote q WHERE q.OpportunityId = o.OpportunityId AND q.IsDeleted = 0) AS QuoteCount,
       o.CreatedDateUtc,
       o.ModifiedDateUtc
FROM   CRM.Opportunity o
JOIN   Client.Account a ON a.AccountId = o.AccountId
WHERE  o.OpportunityId = @Id AND o.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SubmissionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Submissions.Submission
    (SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, Priority,
     AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium, MarketCount, QuoteCount,
     CreatedDateUtc, IsDeleted)
VALUES
    (@SubmissionId, @TenantId, @AccountId, @OpportunityId,
     'SUB-' + FORMAT(GETUTCDATE(), 'yyyyMMdd') + '-' + RIGHT('0000' + CAST(NEXT VALUE FOR Submissions.SubmissionSeq AS VARCHAR), 4),
     @LineOfBusiness, 'Draft', @Priority,
     @AssignedToUserId, @EffectiveDate, @ExpirationDate, @TargetPremium, 0, 0,
     GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SubmissionId     = id,
            request.TenantId,
            request.AccountId,
            request.OpportunityId,
            request.LineOfBusiness,
            request.Priority,
            request.AssignedToUserId,
            request.EffectiveDate,
            request.ExpirationDate,
            request.TargetPremium,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET    LineOfBusiness  = @LineOfBusiness,
       Status          = @Status,
       Priority        = @Priority,
       EffectiveDate   = @EffectiveDate,
       ExpirationDate  = @ExpirationDate,
       TargetPremium   = @TargetPremium,
       AssignedToUserId = @AssignedToUserId,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.LineOfBusiness,
            request.Status,
            request.Priority,
            request.EffectiveDate,
            request.ExpirationDate,
            request.TargetPremium,
            request.AssignedToUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task AssignAsync(Guid id, AssignSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET    AssignedToUserId = @AssignedToUserId,
       ModifiedDateUtc  = GETUTCDATE()
WHERE  SubmissionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AssignedToUserId }, cancellationToken: cancellationToken));
    }

    // ── Markets ───────────────────────────────────────────────────────

    private const string MarketColumns = "sm.SubmissionMarketId, sm.SubmissionId, sm.CarrierId, c.CarrierName, sm.Status, sm.AppetiteScore, sm.IsRecommended, sm.DeclineReason, sm.AddedDateUtc, sm.RespondedDateUtc";

    public async Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT sm.SubmissionMarketId, sm.SubmissionId, sm.CarrierId, c.CarrierName,
       sm.Status, sm.AppetiteScore, sm.IsRecommended, sm.DeclineReason, sm.AddedDateUtc, sm.RespondedDateUtc
FROM   Submissions.SubmissionMarket sm
JOIN   Core.Carrier                 c  ON c.CarrierId = sm.CarrierId
WHERE  sm.SubmissionId = @SubmissionId AND sm.IsDeleted = 0
ORDER BY sm.AppetiteScore DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionMarketDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.CarrierId, c.CarrierName, s.LineOfBusiness,
       ar.AppetiteScore, 1 AS IsRecommended, NULL AS DeclineReason,
       GETUTCDATE() AS AddedDateUtc, NULL AS RespondedDateUtc,
       NEWID() AS SubmissionMarketId, @SubmissionId AS SubmissionId, 'Suggested' AS Status
FROM   Submissions.Submission s
JOIN   Core.AppetiteRule      ar ON ar.LineOfBusiness = s.LineOfBusiness AND ar.IsDeleted = 0
JOIN   Core.Carrier           c  ON c.CarrierId       = ar.CarrierId     AND c.IsDeleted  = 0
WHERE  s.SubmissionId = @SubmissionId AND s.IsDeleted = 0
  AND  ar.AppetiteScore >= 60
ORDER BY ar.AppetiteScore DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionMarketDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Submissions.SubmissionMarket
    (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, IsDeleted)
VALUES
    (@SubmissionMarketId, @SubmissionId, @CarrierId, 'Pending', 0, 0, GETUTCDATE(), 0);

UPDATE Submissions.Submission
SET    MarketCount     = MarketCount + 1,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = @SubmissionId;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SubmissionMarketId = id,
            request.SubmissionId,
            request.CarrierId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionMarket
SET    Status           = @Status,
       DeclineReason    = @DeclineReason,
       RespondedDateUtc = GETUTCDATE()
WHERE  SubmissionMarketId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = submissionMarketId, request.Status, request.DeclineReason }, cancellationToken: cancellationToken));
    }

    public async Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionMarket SET IsDeleted = 1 WHERE SubmissionMarketId = @Id;

UPDATE Submissions.Submission
SET    MarketCount     = CASE WHEN MarketCount > 0 THEN MarketCount - 1 ELSE 0 END,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = (SELECT SubmissionId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @Id);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = submissionMarketId }, cancellationToken: cancellationToken));
    }

    // ── Quotes ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT q.QuoteId, q.SubmissionId, q.CarrierId, c.CarrierName,
       q.QuoteNumber, q.Status, q.AnnualPremium, q.Deductible, q.Limit,
       q.CoverageNotes, q.QuotedDateUtc, q.ExpiresDateUtc
FROM   Submissions.Quote q
JOIN   Core.Carrier      c ON c.CarrierId = q.CarrierId
WHERE  q.SubmissionId = @SubmissionId AND q.IsDeleted = 0
ORDER BY q.AnnualPremium ASC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<QuoteComparisonDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT q.QuoteId, q.SubmissionId, q.CarrierId, c.CarrierName,
       q.QuoteNumber, q.Status, q.AnnualPremium, q.Deductible, q.Limit,
       q.CoverageNotes, q.QuotedDateUtc, q.ExpiresDateUtc
FROM   Submissions.Quote q
JOIN   Core.Carrier      c ON c.CarrierId = q.CarrierId
WHERE  q.QuoteId = @QuoteId AND q.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<QuoteComparisonDto>(new CommandDefinition(sql, new { QuoteId = quoteId }, cancellationToken: cancellationToken));
    }

    // ── Proposals ─────────────────────────────────────────────────────

    public async Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT ProposalId, SubmissionId, TenantId, Title, Status, PdfUrl, HtmlContent, CreatedDateUtc, GeneratedDateUtc
FROM   Submissions.Proposal
WHERE  ProposalId = @ProposalId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ProposalDto>(new CommandDefinition(sql, new { ProposalId = proposalId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Submissions.Proposal
    (ProposalId, SubmissionId, TenantId, Title, Status, CreatedDateUtc, IsDeleted)
VALUES
    (@ProposalId, @SubmissionId, @TenantId, @Title, 'Generating', GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ProposalId   = id,
            request.SubmissionId,
            request.TenantId,
            request.Title,
        }, cancellationToken: cancellationToken));
        return id;
    }

    // ── Appetite ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.CarrierId, c.CarrierName, ar.LineOfBusiness,
       ar.AppetiteScore AS MatchScore,
       CASE
           WHEN ar.AppetiteScore >= 80 THEN 'Strong'
           WHEN ar.AppetiteScore >= 60 THEN 'Moderate'
           ELSE 'Weak'
       END AS MatchLevel,
       NULL AS Notes
FROM   Core.AppetiteRule ar
JOIN   Core.Carrier      c ON c.CarrierId = ar.CarrierId AND c.IsDeleted = 0
WHERE  ar.TenantId      = @TenantId
  AND  ar.IsDeleted     = 0
  AND  ar.LineOfBusiness = @LineOfBusiness
  AND  (@State IS NULL OR @State = '' OR ar.AllowedStates LIKE '%' + @State + '%' OR ar.AllowedStates IS NULL)
ORDER BY ar.AppetiteScore DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await cn.QueryAsync<AppetiteMatchDto>(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.LineOfBusiness,
            request.State,
        }, cancellationToken: cancellationToken))).AsList();
        return rows;
    }

    // ── Bind & Issue ──────────────────────────────────────────────────

    public async Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId,
       PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc
FROM   Submissions.BoundPolicy
WHERE  SubmissionId = @SubmissionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyBindDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Submissions.BoundPolicy
    (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId,
     PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IsDeleted)
VALUES
    (@PolicyId, @SubmissionId, @QuoteId, @TenantId, @AccountId, @CarrierId,
     'POL-' + FORMAT(GETUTCDATE(), 'yyyyMMdd') + '-' + RIGHT('00000' + CAST(NEXT VALUE FOR Submissions.PolicySeq AS VARCHAR), 5),
     'Bound', @AnnualPremium, @EffectiveDate, @ExpirationDate, GETUTCDATE(), 0);

UPDATE Submissions.Submission
SET    Status          = 'Bound',
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = @SubmissionId;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyId       = id,
            request.SubmissionId,
            request.QuoteId,
            request.TenantId,
            request.AccountId,
            request.CarrierId,
            request.AnnualPremium,
            request.EffectiveDate,
            request.ExpirationDate,
        }, cancellationToken: cancellationToken));
        return id;
    }
}
