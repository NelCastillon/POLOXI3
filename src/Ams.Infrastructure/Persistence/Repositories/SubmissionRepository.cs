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
    SELECT s.SubmissionId,
           s.TenantId,
           s.AccountId,
           a.AccountName,
           s.OpportunityId,
           COALESCE(o.OpportunityName, s.SubmissionNumber) AS OpportunityName,
           s.SubmissionNumber,
           s.LineOfBusiness,
           s.Status,
           s.Priority,
           s.AssignedToUserId,
           COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
           s.EffectiveDate,
           s.ExpirationDate,
           s.TargetPremium,
           (SELECT COUNT(1) FROM Submissions.SubmissionMarket sm WHERE sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0) AS MarketCount,
           (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0) AS QuoteCount,
           s.CreatedDateUtc,
           s.ModifiedDateUtc
    FROM   Submissions.Submission s
    JOIN   Client.Account a ON a.AccountId = s.AccountId
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
    LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
    WHERE  s.TenantId = @TenantId
      AND  s.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR s.SubmissionNumber LIKE '%' + @SearchTerm + '%' OR s.LineOfBusiness LIKE '%' + @SearchTerm + '%' OR a.AccountName LIKE '%' + @SearchTerm + '%' OR o.OpportunityName LIKE '%' + @SearchTerm + '%')
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
    SELECT s.LineOfBusiness, s.Status
    FROM   Submissions.Submission s
    JOIN   Client.Account a ON a.AccountId = s.AccountId
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
    WHERE  s.TenantId = @TenantId
      AND  s.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR s.SubmissionNumber LIKE '%' + @SearchTerm + '%' OR s.LineOfBusiness LIKE '%' + @SearchTerm + '%' OR a.AccountName LIKE '%' + @SearchTerm + '%' OR o.OpportunityName LIKE '%' + @SearchTerm + '%')
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
SELECT s.SubmissionId,
       s.TenantId,
       s.AccountId,
       a.AccountName,
       s.OpportunityId,
       COALESCE(o.OpportunityName, s.SubmissionNumber) AS OpportunityName,
       s.SubmissionNumber,
       s.LineOfBusiness,
       s.Status,
       s.Priority,
       s.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
       s.EffectiveDate,
       s.ExpirationDate,
       s.TargetPremium,
       (SELECT COUNT(1) FROM Submissions.SubmissionMarket sm WHERE sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0) AS MarketCount,
       (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0) AS QuoteCount,
       s.CreatedDateUtc,
       s.ModifiedDateUtc
FROM   Submissions.Submission s
JOIN   Client.Account a ON a.AccountId = s.AccountId
LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
WHERE  s.SubmissionId = @Id AND s.IsDeleted = 0;";
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

    public async Task<SubmissionActionResult> SubmitToMarketAsync(Guid id, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = COALESCE(@CarrierIdIn, (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CarrierName));
IF @CarrierId IS NULL THROW 52000, 'No carrier is available for this tenant.', 1;

DECLARE @MarketId UNIQUEIDENTIFIER = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0);
IF @MarketId IS NULL
BEGIN
    SET @MarketId = NEWID();
    INSERT INTO Submissions.SubmissionMarket (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, DeclineReason, AddedDateUtc, RespondedDateUtc, IsDeleted)
    VALUES (@MarketId, @SubmissionId, @CarrierId, N'Submitted', 80, 1, NULL, SYSUTCDATETIME(), NULL, 0);
END
ELSE
BEGIN
    UPDATE Submissions.SubmissionMarket
    SET Status = N'Submitted', DeclineReason = NULL, RespondedDateUtc = NULL
    WHERE SubmissionMarketId = @MarketId;
END

UPDATE Submissions.Submission
SET Status = CASE WHEN Status IN (N'Bound', N'Declined', N'Withdrawn') THEN Status ELSE N'In Review' END,
    MarketCount = (SELECT COUNT(1) FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'SubmitToMarket', COALESCE(@Notes, N'Submitted to market.'), SYSUTCDATETIME(), 0);

SELECT @MarketId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var marketId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, CarrierIdIn = request.CarrierId, request.Notes }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(marketId, "Submission sent to market.");
    }

    public async Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = COALESCE(@CarrierIdIn, (SELECT TOP 1 CarrierId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC), (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CarrierName));
IF @CarrierId IS NULL THROW 52001, 'No carrier is available for quote request.', 1;

DECLARE @QuoteId UNIQUEIDENTIFIER = NEWID();
DECLARE @Premium DECIMAL(18,2) = COALESCE(@AnnualPremium, (SELECT NULLIF(TargetPremium, 0) FROM Submissions.Submission WHERE SubmissionId = @SubmissionId), 50000);

INSERT INTO Submissions.Quote (QuoteId, SubmissionId, CarrierId, QuoteNumber, Status, AnnualPremium, Deductible, [Limit], CoverageNotes, QuotedDateUtc, ExpiresDateUtc, CreatedDateUtc, IsDeleted)
VALUES (@QuoteId, @SubmissionId, @CarrierId, N'QT-' + CONVERT(NVARCHAR(8), SYSUTCDATETIME(), 112) + N'-' + RIGHT(REPLACE(CONVERT(NVARCHAR(36), @QuoteId), N'-', N''), 6), N'Requested', @Premium, COALESCE(@Deductible, 2500), COALESCE(@Limit, 1000000), COALESCE(@CoverageNotes, N'Enterprise quote requested from submissions register.'), SYSUTCDATETIME(), DATEADD(day, 30, SYSUTCDATETIME()), SYSUTCDATETIME(), 0);

UPDATE Submissions.Submission
SET Status = N'Quoted',
    QuoteCount = (SELECT COUNT(1) FROM Submissions.Quote WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    TargetPremium = COALESCE(TargetPremium, @Premium),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'RequestQuote', COALESCE(@CoverageNotes, N'Quote requested.'), SYSUTCDATETIME(), 0);

SELECT @QuoteId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var quoteId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, CarrierIdIn = request.CarrierId, request.AnnualPremium, request.Deductible, request.Limit, request.CoverageNotes }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(quoteId, "Quote requested.");
    }

    public async Task<SubmissionActionResult> CopyAsync(Guid id, CopySubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewSubmissionId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Submissions.Submission (SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, Priority, AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium, MarketCount, QuoteCount, CreatedDateUtc, IsDeleted)
SELECT @NewSubmissionId, TenantId, AccountId, OpportunityId,
       N'SUB-' + CONVERT(NVARCHAR(8), SYSUTCDATETIME(), 112) + N'-' + RIGHT(REPLACE(CONVERT(NVARCHAR(36), @NewSubmissionId), N'-', N''), 6),
       COALESCE(NULLIF(@LineOfBusiness, N''), LineOfBusiness),
       N'New',
       COALESCE(NULLIF(@Priority, N''), Priority),
       AssignedToUserId,
       COALESCE(@EffectiveDate, DATEADD(year, 1, EffectiveDate)),
       DATEADD(year, 1, COALESCE(@EffectiveDate, DATEADD(year, 1, EffectiveDate))),
       TargetPremium,
       0,
       0,
       SYSUTCDATETIME(),
       0
FROM Submissions.Submission
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52002, 'Submission was not found for copy.', 1;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @NewSubmissionId, @TenantId, N'Copy', N'Copied from source submission.', SYSUTCDATETIME(), 0);

SELECT @NewSubmissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, request.EffectiveDate, request.LineOfBusiness, request.Priority }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(newId, "Submission copied.");
    }

    public async Task<SubmissionActionResult> DeclineAsync(Guid id, DeclineSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET Status = N'Declined', ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52003, 'Submission was not found for decline.', 1;

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN Status IN (N'Bound', N'Declined') THEN Status ELSE N'Declined' END,
    DeclineReason = COALESCE(NULLIF(DeclineReason, N''), @Reason),
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME())
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'Decline', @Reason, SYSUTCDATETIME(), 0);

SELECT @SubmissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var declinedId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, request.Reason }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(declinedId, "Submission declined.");
    }

    public async Task<SubmissionActionResult> CreatePolicyAsync(Guid id, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var submission = await GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Submission was not found for policy creation.");
        var quote = request.QuoteId.HasValue ? await GetQuoteByIdAsync(request.QuoteId.Value, cancellationToken) : null;
        if (quote is null)
        {
            var quotes = await GetQuoteComparisonAsync(id, cancellationToken);
            quote = quotes.OrderByDescending(q => q.AnnualPremium).FirstOrDefault();
        }

        if (quote is null)
        {
            var quoteResult = await RequestQuoteAsync(id, new RequestSubmissionQuoteRequest(request.TenantId, request.CarrierId, request.AnnualPremium, null, null, "Quote generated for policy creation."), cancellationToken);
            quote = await GetQuoteByIdAsync(quoteResult.Id, cancellationToken);
        }

        if (quote is null)
            throw new InvalidOperationException("Unable to create or resolve a quote for policy creation.");

        var policyId = await BindPolicyAsync(new BindPolicyRequest(id, quote.QuoteId, request.TenantId, submission.AccountId, request.CarrierId ?? quote.CarrierId, request.AnnualPremium ?? quote.AnnualPremium, request.EffectiveDate ?? submission.EffectiveDate, request.ExpirationDate ?? submission.ExpirationDate), cancellationToken);
        return new SubmissionActionResult(policyId, "Policy created from submission.");
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
