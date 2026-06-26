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

    public async Task<PagedResult<PolicyRegisterDto>> SearchPoliciesAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT p.PolicyId,
           p.SubmissionId,
           p.QuoteId,
           p.TenantId,
           p.AccountId,
           COALESCE(a.AccountName, s.SubmissionNumber, p.PolicyNumber) AS AccountName,
           N'Commercial' AS AccountType,
           p.CarrierId,
           COALESCE(c.CarrierName, N'Bound Carrier') AS CarrierName,
           p.PolicyNumber,
           CASE WHEN p.Status = N'Bound' THEN N'Active' ELSE p.Status END AS Status,
           COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness,
           COALESCE(NULLIF(s.Priority, N''), N'Normal') AS Priority,
           p.AnnualPremium,
           p.AnnualPremium AS WrittenPremium,
           p.EffectiveDate,
           p.ExpirationDate,
           p.BoundDateUtc,
           s.AssignedToUserId,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS AssignedToUserName,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS ProducerName,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS CsrName,
           N'HQ' AS Branch,
           (SELECT COUNT(1) FROM Compliance.PolicyDocument d WHERE d.TenantId = p.TenantId AND d.IsDeleted = 0 AND d.PolicyCode = p.PolicyNumber) AS DocumentCount,
           (SELECT COUNT(1) FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0) AS ActivityCount,
           (SELECT COUNT(1) FROM Policy.PolicyEndorsement e WHERE e.TenantId = p.TenantId AND e.PolicyNumber = p.PolicyNumber AND e.IsDeleted = 0) AS EndorsementCount,
            COALESCE(NULLIF(lastRenewal.Notes, N''), CASE
                WHEN DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) BETWEEN 0 AND 90 THEN N'Pre-Renewal'
                WHEN p.ExpirationDate < SYSUTCDATETIME() THEN N'Expired'
                ELSE N'Not Started'
            END) AS RenewalStage,
            COALESCE(lastAction.Notes, CONCAT(N'Policy bound ', CONVERT(nvarchar(10), p.BoundDateUtc, 101), N' from submission ', COALESCE(s.SubmissionNumber, N''))) AS LastAction
    FROM   Submissions.BoundPolicy p
    LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
    LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
    LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
    LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
    OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 ORDER BY al.CreatedDateUtc DESC) lastAction
    OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 AND al.ActionCode = N'RenewalStage' ORDER BY al.CreatedDateUtc DESC) lastRenewal
    WHERE  p.TenantId = @TenantId
      AND  p.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = N'' OR p.PolicyNumber LIKE N'%' + @SearchTerm + N'%' OR a.AccountName LIKE N'%' + @SearchTerm + N'%' OR c.CarrierName LIKE N'%' + @SearchTerm + N'%' OR s.LineOfBusiness LIKE N'%' + @SearchTerm + N'%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = N'' OR LineOfBusiness = @LineOfBusiness)
)
SELECT * FROM Filtered
ORDER BY BoundDateUtc DESC, ExpirationDate ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

;WITH Cte AS
(
    SELECT CASE WHEN p.Status = N'Bound' THEN N'Active' ELSE p.Status END AS Status,
           COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness
    FROM   Submissions.BoundPolicy p
    LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
    LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
    LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
    WHERE  p.TenantId = @TenantId
      AND  p.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = N'' OR p.PolicyNumber LIKE N'%' + @SearchTerm + N'%' OR a.AccountName LIKE N'%' + @SearchTerm + N'%' OR c.CarrierName LIKE N'%' + @SearchTerm + N'%' OR s.LineOfBusiness LIKE N'%' + @SearchTerm + N'%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = N'' OR LineOfBusiness = @LineOfBusiness)
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

        var items = (await multi.ReadAsync<PolicyRegisterDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PolicyRegisterDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PolicyRegisterDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 p.PolicyId,
       p.SubmissionId,
       p.QuoteId,
       p.TenantId,
       p.AccountId,
       COALESCE(a.AccountName, s.SubmissionNumber, p.PolicyNumber) AS AccountName,
       N'Commercial' AS AccountType,
       p.CarrierId,
       COALESCE(c.CarrierName, N'Bound Carrier') AS CarrierName,
       p.PolicyNumber,
       CASE WHEN p.Status = N'Bound' THEN N'Active' ELSE p.Status END AS Status,
       COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness,
       COALESCE(NULLIF(s.Priority, N''), N'Normal') AS Priority,
       p.AnnualPremium,
       p.AnnualPremium AS WrittenPremium,
       p.EffectiveDate,
       p.ExpirationDate,
       p.BoundDateUtc,
       s.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS AssignedToUserName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS ProducerName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS CsrName,
       N'HQ' AS Branch,
       (SELECT COUNT(1) FROM Compliance.PolicyDocument d WHERE d.TenantId = p.TenantId AND d.IsDeleted = 0 AND d.PolicyCode = p.PolicyNumber) AS DocumentCount,
       (SELECT COUNT(1) FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0) AS ActivityCount,
       (SELECT COUNT(1) FROM Policy.PolicyEndorsement e WHERE e.TenantId = p.TenantId AND e.PolicyNumber = p.PolicyNumber AND e.IsDeleted = 0) AS EndorsementCount,
       COALESCE(NULLIF(lastRenewal.Notes, N''), CASE
           WHEN DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) BETWEEN 0 AND 90 THEN N'Pre-Renewal'
           WHEN p.ExpirationDate < SYSUTCDATETIME() THEN N'Expired'
           ELSE N'Not Started'
       END) AS RenewalStage,
       COALESCE(lastAction.Notes, CONCAT(N'Policy bound ', CONVERT(nvarchar(10), p.BoundDateUtc, 101), N' from submission ', COALESCE(s.SubmissionNumber, N''))) AS LastAction
FROM Submissions.BoundPolicy p
LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 ORDER BY al.CreatedDateUtc DESC) lastAction
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 AND al.ActionCode = N'RenewalStage' ORDER BY al.CreatedDateUtc DESC) lastRenewal
WHERE p.PolicyId = @PolicyId AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyRegisterDto>(new CommandDefinition(sql, new { PolicyId = policyId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreatePolicyRegisterAsync(UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND CarrierName = @CarrierName AND IsDeleted = 0 ORDER BY CreatedDateUtc);
IF @CarrierId IS NULL
BEGIN
    SET @CarrierId = NEWID();
    INSERT INTO Core.Carrier (CarrierId, TenantId, CarrierCode, CarrierName, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@CarrierId, @TenantId, LEFT(REPLACE(UPPER(@CarrierName), N' ', N''), 50), @CarrierName, 1, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;

DECLARE @SubmissionId UNIQUEIDENTIFIER = NEWID();
DECLARE @QuoteId UNIQUEIDENTIFIER = NEWID();
DECLARE @SubmissionNumber NVARCHAR(50) = CONCAT(N'SUB-', FORMAT(GETUTCDATE(), 'yyyyMMdd'), N'-', RIGHT('00000' + CAST(NEXT VALUE FOR Submissions.SubmissionSeq AS VARCHAR), 5));

INSERT INTO Submissions.Submission
    (SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, Priority, AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium, MarketCount, QuoteCount, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@SubmissionId, @TenantId, @AccountId, NULL, @SubmissionNumber, @LineOfBusiness, CASE WHEN @Status IN (N'Active', N'Bound') THEN N'Bound' ELSE @Status END, N'Normal', @ModifiedByUserId, @EffectiveDate, @ExpirationDate, NULLIF(@AnnualPremium, 0), 0, 1, SYSUTCDATETIME(), @ModifiedByUserId, 0);

INSERT INTO Submissions.Quote
    (QuoteId, SubmissionId, CarrierId, QuoteNumber, Status, AnnualPremium, Deductible, [Limit], CoverageNotes, QuotedDateUtc, ExpiresDateUtc, CreatedDateUtc, IsDeleted)
VALUES
    (@QuoteId, @SubmissionId, @CarrierId, CONCAT(N'QT-', FORMAT(GETUTCDATE(), 'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @PolicyId), N'-', N''), 6)), N'Presented', @AnnualPremium, NULL, NULL, @Notes, SYSUTCDATETIME(), DATEADD(day, 30, SYSUTCDATETIME()), SYSUTCDATETIME(), 0);

INSERT INTO Submissions.BoundPolicy
    (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId, PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IsDeleted)
VALUES
    (@PolicyId, @SubmissionId, @QuoteId, @TenantId, @AccountId, @CarrierId, @PolicyNumber, CASE WHEN @Status = N'Active' THEN N'Bound' ELSE @Status END, @AnnualPremium, @EffectiveDate, @ExpirationDate, SYSUTCDATETIME(), 0);

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'PolicyCreated', CONCAT(N'Policy created from register. ', COALESCE(@Notes, N'')), SYSUTCDATETIME(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyId = id,
            request.TenantId,
            request.AccountId,
            request.PolicyNumber,
            request.CarrierName,
            request.LineOfBusiness,
            request.Status,
            request.EffectiveDate,
            request.ExpirationDate,
            request.AnnualPremium,
            request.Notes,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdatePolicyRegisterAsync(Guid policyId, UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER = (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND CarrierName = @CarrierName AND IsDeleted = 0 ORDER BY CreatedDateUtc);
IF @CarrierId IS NULL
BEGIN
    SET @CarrierId = NEWID();
    INSERT INTO Core.Carrier (CarrierId, TenantId, CarrierCode, CarrierName, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@CarrierId, @TenantId, LEFT(REPLACE(UPPER(@CarrierName), N' ', N''), 50), @CarrierName, 1, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;

UPDATE Submissions.BoundPolicy
SET PolicyNumber = @PolicyNumber,
    CarrierId = @CarrierId,
    Status = CASE WHEN @Status = N'Active' THEN N'Bound' ELSE @Status END,
    AnnualPremium = @AnnualPremium,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    @SubmissionId = SubmissionId
WHERE PolicyId = @PolicyId AND TenantId = @TenantId AND IsDeleted = 0;

UPDATE Submissions.Submission
SET AccountId = @AccountId,
    LineOfBusiness = @LineOfBusiness,
    Status = CASE WHEN @Status = N'Active' THEN N'Bound' ELSE @Status END,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    TargetPremium = NULLIF(@AnnualPremium, 0),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'PolicyUpdated', CONCAT(N'Policy edited from register. ', COALESCE(@Notes, N'')), SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyId = policyId,
            request.TenantId,
            request.AccountId,
            request.PolicyNumber,
            request.CarrierName,
            request.LineOfBusiness,
            request.Status,
            request.EffectiveDate,
            request.ExpirationDate,
            request.AnnualPremium,
            request.Notes,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task<SubmissionActionResult> ExecutePolicyRegisterActionAsync(Guid policyId, PolicyRegisterActionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @QuoteId UNIQUEIDENTIFIER;
DECLARE @AccountId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER;
DECLARE @PolicyNumber NVARCHAR(50);
DECLARE @AccountName NVARCHAR(200);
DECLARE @LineOfBusiness NVARCHAR(100);
DECLARE @CarrierName NVARCHAR(200);
DECLARE @AnnualPremium DECIMAL(18,2);
DECLARE @EffectiveDate DATETIME2;
DECLARE @ExpirationDate DATETIME2;

SELECT @SubmissionId = p.SubmissionId,
       @QuoteId = p.QuoteId,
       @AccountId = p.AccountId,
       @CarrierId = p.CarrierId,
       @PolicyNumber = p.PolicyNumber,
       @AccountName = COALESCE(a.AccountName, p.PolicyNumber),
       @LineOfBusiness = COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability'),
       @CarrierName = COALESCE(c.CarrierName, N'Carrier'),
       @AnnualPremium = p.AnnualPremium,
       @EffectiveDate = p.EffectiveDate,
       @ExpirationDate = p.ExpirationDate
FROM Submissions.BoundPolicy p
LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
WHERE p.PolicyId = @PolicyId AND p.TenantId = @TenantId AND p.IsDeleted = 0;

IF @SubmissionId IS NULL THROW 51000, 'Policy was not found.', 1;

DECLARE @ActionCode NVARCHAR(80) = REPLACE(@Action, N' ', N'');
DECLARE @Message NVARCHAR(500) = CONCAT(@Action, N' completed for ', @PolicyNumber, N'.');

IF @Action = N'Cancel Policy'
BEGIN
    UPDATE Submissions.BoundPolicy SET Status = N'Cancelled' WHERE PolicyId = @PolicyId AND TenantId = @TenantId AND IsDeleted = 0;
    UPDATE Submissions.Submission SET Status = N'Cancelled', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;
    INSERT INTO Policy.PolicyCancellation (CancellationId, TenantId, PolicyId, AccountId, CancellationNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, CancellationReason, CancellationType, RequestType, RequestDateUtc, EffectiveDate, CancellationDate, ReturnPremium, PremiumDue, Status, Priority, RequestedByName, AssignedToName, Notes, WorkflowStage, DueDate, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyId, @AccountId, CONCAT(N'CAN-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyCancellation WHERE TenantId = @TenantId), 1), N'0000')), @PolicyNumber, @AccountName, @LineOfBusiness, @CarrierName, COALESCE(NULLIF(@Notes, N''), N'Policy cancelled from register'), N'Pro-Rata', N'Cancellation', SYSUTCDATETIME(), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@ActionDate, SYSUTCDATETIME()), 0, 0, N'Pending', N'Normal', N'Current User', N'Current User', @Notes, N'Cancellation Intake', DATEADD(day, 7, SYSUTCDATETIME()), 0, 0, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END
ELSE IF @Action = N'Renew'
BEGIN
    DECLARE @RenewalPolicyId UNIQUEIDENTIFIER = NEWID();
    DECLARE @RenewalQuoteId UNIQUEIDENTIFIER = NEWID();
    DECLARE @RenewalEffective DATETIME2 = COALESCE(@ActionDate, @ExpirationDate);
    DECLARE @RenewalPremium DECIMAL(18,2) = COALESCE(NULLIF(@Premium, 0), @AnnualPremium);
    INSERT INTO Submissions.Quote (QuoteId, SubmissionId, CarrierId, QuoteNumber, Status, AnnualPremium, CoverageNotes, QuotedDateUtc, ExpiresDateUtc, CreatedDateUtc, IsDeleted)
    VALUES (@RenewalQuoteId, @SubmissionId, @CarrierId, CONCAT(N'QT-REN-', FORMAT(GETUTCDATE(), 'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @RenewalPolicyId), N'-', N''), 6)), N'Presented', @RenewalPremium, @Notes, SYSUTCDATETIME(), DATEADD(day, 30, SYSUTCDATETIME()), SYSUTCDATETIME(), 0);
    INSERT INTO Submissions.BoundPolicy (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId, PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IsDeleted)
    VALUES (@RenewalPolicyId, @SubmissionId, @RenewalQuoteId, @TenantId, @AccountId, @CarrierId, CONCAT(@PolicyNumber, N'-REN-', FORMAT(GETUTCDATE(), 'yyMMdd')), N'Pending', @RenewalPremium, @RenewalEffective, DATEADD(year, 1, @RenewalEffective), SYSUTCDATETIME(), 0);
    SET @Message = CONCAT(N'Renewal policy created for ', @PolicyNumber, N'.');
END
ELSE IF @Action = N'Endorse'
BEGIN
    INSERT INTO Policy.PolicyEndorsement (EndorsementId, TenantId, PolicyId, AccountId, EndorsementNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, EndorsementType, Description, EffectiveDate, RequestedDateUtc, PremiumDelta, Status, Priority, RequestedByName, AssignedToName, WorkflowStage, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyId, @AccountId, CONCAT(N'END-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyEndorsement WHERE TenantId = @TenantId), 1), N'0000')), @PolicyNumber, @AccountName, @LineOfBusiness, @CarrierName, N'Change Endorsement', COALESCE(NULLIF(@Notes, N''), N'Policy endorsement requested from register'), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@Premium, 0), N'Pending', N'Normal', N'Current User', N'Current User', N'Intake', 0, 0, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END
ELSE IF @Action = N'Add Document'
BEGIN
    INSERT INTO Compliance.PolicyDocument (PolicyDocumentId, TenantId, PolicyCode, PolicyTitle, PolicyTypeCode, Version, EffectiveDateUtc, IsActive, StatusCode, Description, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyNumber, COALESCE(NULLIF(@DocumentTitle, N''), CONCAT(N'Policy Document - ', @PolicyNumber)), N'Policy', N'1.0', COALESCE(@ActionDate, SYSUTCDATETIME()), 1, N'Published', @Notes, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, @ActionCode, COALESCE(NULLIF(@Notes, N''), @Message), SYSUTCDATETIME(), 0);

SELECT @Message;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var message = await cn.QuerySingleAsync<string>(new CommandDefinition(sql, new
        {
            PolicyId = policyId,
            request.TenantId,
            request.Action,
            ActionDate = request.EffectiveDate,
            request.Premium,
            request.DocumentTitle,
            request.Notes,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(policyId, message);
    }

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

public sealed class SubmissionReferenceOptionRepository : ISubmissionReferenceOptionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SubmissionReferenceOptionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<SubmissionReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureReferenceDataAsync(connection, tenantId, cancellationToken);

        const string sql = @"
SELECT SubmissionReferenceOptionId, TenantId, OptionGroup, OptionCode, OptionName, Description,
       IsDefault, IsActive, SortOrder, CreatedDateUtc
FROM Submissions.SubmissionReferenceOption
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@OptionGroup IS NULL OR @OptionGroup = '' OR OptionGroup = @OptionGroup)
ORDER BY OptionGroup, SortOrder, OptionName;";

        var items = await connection.QueryAsync<SubmissionReferenceOptionDto>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            OptionGroup = optionGroup,
        }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    private static async Task EnsureReferenceDataAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Submissions') EXEC('CREATE SCHEMA Submissions');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Submissions.SubmissionReferenceOption'))
CREATE TABLE Submissions.SubmissionReferenceOption (
    SubmissionReferenceOptionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                    UNIQUEIDENTIFIER NOT NULL,
    OptionGroup                 NVARCHAR(50)     NOT NULL,
    OptionCode                  NVARCHAR(100)    NOT NULL,
    OptionName                  NVARCHAR(150)    NOT NULL,
    Description                 NVARCHAR(500)    NULL,
    IsDefault                   BIT              NOT NULL DEFAULT 0,
    IsActive                    BIT              NOT NULL DEFAULT 1,
    SortOrder                   INT              NOT NULL DEFAULT 0,
    CreatedDateUtc              DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc             DATETIME2        NULL,
    IsDeleted                   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT UQ_SubmissionReferenceOption_Tenant_Group_Code UNIQUE (TenantId, OptionGroup, OptionCode)
);

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'SubmissionStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'SubmissionStatus', 'New', 'New', 'New submission intake record.', 1, 10),
        (@TenantId, 'SubmissionStatus', 'In Review', 'In Review', 'Submission is in underwriting or carrier review.', 0, 20),
        (@TenantId, 'SubmissionStatus', 'Quoted', 'Quoted', 'Submission has one or more quotes.', 0, 30),
        (@TenantId, 'SubmissionStatus', 'Bound', 'Bound', 'Submission has been bound into policy workflow.', 0, 40),
        (@TenantId, 'SubmissionStatus', 'Declined', 'Declined', 'Submission was declined by underwriting or market.', 0, 80),
        (@TenantId, 'SubmissionStatus', 'Withdrawn', 'Withdrawn', 'Submission was withdrawn by client or producer.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'LineOfBusiness' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'LineOfBusiness', 'General Liability', 'General Liability', 'Commercial general liability placement.', 1, 10),
        (@TenantId, 'LineOfBusiness', 'Commercial Property', 'Commercial Property', 'Commercial property placement.', 0, 20),
        (@TenantId, 'LineOfBusiness', 'Commercial Auto', 'Commercial Auto', 'Commercial automobile placement.', 0, 30),
        (@TenantId, 'LineOfBusiness', 'Workers Comp', 'Workers Comp', 'Workers compensation placement.', 0, 40),
        (@TenantId, 'LineOfBusiness', 'Umbrella / Excess', 'Umbrella / Excess', 'Umbrella or excess liability placement.', 0, 50),
        (@TenantId, 'LineOfBusiness', 'Professional Liability', 'Professional Liability', 'Professional liability placement.', 0, 60),
        (@TenantId, 'LineOfBusiness', 'Home / Dwelling', 'Home / Dwelling', 'Personal home or dwelling placement.', 0, 70),
        (@TenantId, 'LineOfBusiness', 'Personal Auto', 'Personal Auto', 'Personal automobile placement.', 0, 80);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'ApplicationStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'ApplicationStatus', 'Draft', 'Draft', 'Application package is being drafted.', 1, 10),
        (@TenantId, 'ApplicationStatus', 'Submitted', 'Submitted', 'Application has been submitted.', 0, 20),
        (@TenantId, 'ApplicationStatus', 'Under Review', 'Under Review', 'Application is under review.', 0, 30),
        (@TenantId, 'ApplicationStatus', 'Requirements Pending', 'Requirements Pending', 'Additional requirements are pending.', 0, 40),
        (@TenantId, 'ApplicationStatus', 'Approved', 'Approved', 'Application is approved for quote workflow.', 0, 50),
        (@TenantId, 'ApplicationStatus', 'Rejected', 'Rejected', 'Application was rejected.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'QuoteStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'QuoteStatus', 'Pending', 'Pending', 'Quote is pending market response.', 1, 10),
        (@TenantId, 'QuoteStatus', 'Accepted', 'Accepted', 'Quote has been accepted or presented.', 0, 20),
        (@TenantId, 'QuoteStatus', 'Declined', 'Declined', 'Quote has been declined.', 0, 80),
        (@TenantId, 'QuoteStatus', 'Expired', 'Expired', 'Quote has expired.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'DeclineType' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'DeclineType', 'Carrier', 'Carrier', 'Carrier or market declined the submission.', 1, 10),
        (@TenantId, 'DeclineType', 'Internal', 'Internal', 'Agency or underwriting team declined the submission.', 0, 20),
        (@TenantId, 'DeclineType', 'Withdrawn', 'Withdrawn', 'Client or producer withdrew the submission.', 0, 30);
END;";

        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
