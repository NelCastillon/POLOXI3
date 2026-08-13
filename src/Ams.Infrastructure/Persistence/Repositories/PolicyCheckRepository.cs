using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyChecks;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyCheckRepository : IPolicyCheckRepository
{
    private const string CheckColumns = @"PolicyCheckId, TenantId, PolicyId, AccountId, QuoteId, CheckNumber, PolicyNumber, AccountName, CarrierName,
        LineOfBusiness, PolicyEffectiveDate, PolicyExpirationDate, StatusCode, PriorityCode, CheckTypeCode, AssignedToUserId, AssignedToName,
        DueDate, ReceivedDateUtc, CompletedDateUtc, CompletedByName, ItemsTotal, ItemsMatched, ItemsDiscrepant, ResultSummary, Notes, IsUrgent, IsArchived";

    private readonly ISqlConnectionFactory _connectionFactory;

    public PolicyCheckRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PolicyCheckCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {CheckColumns}
FROM Policy.PolicyCheck
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsArchived = 0
ORDER BY IsUrgent DESC, DueDate, ReceivedDateUtc DESC;

SELECT PolicyCheckStatusId, TenantId, StatusCode, StatusName, Description, ColorHex, IsTerminal, IsDefault, SortOrder
FROM Policy.PolicyCheckStatus
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY SortOrder;

SELECT PolicyCheckItemDefinitionId, TenantId, ItemCode, ItemName, CategoryCode, CategoryName, Description, DefaultSeverityCode, IsRequired, SortOrder
FROM Policy.PolicyCheckItemDefinition
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY SortOrder;

SELECT PolicyCheckDiscrepancyTypeId, TenantId, TypeCode, TypeName, Description, DefaultSeverityCode, RequiresCarrierNotification, SortOrder
FROM Policy.PolicyCheckDiscrepancyType
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY SortOrder;

SELECT bp.PolicyId, bp.AccountId, bp.QuoteId, ISNULL(bp.PolicyNumber, N'') AS PolicyNumber,
       ISNULL(a.AccountName, N'Unknown Account') AS AccountName, c.CarrierName, bp.LineOfBusiness,
       bp.EffectiveDate, bp.ExpirationDate
FROM Submissions.BoundPolicy bp
LEFT JOIN Client.Account a ON a.AccountId = bp.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = bp.CarrierId
WHERE bp.TenantId = @TenantId AND bp.IsDeleted = 0
ORDER BY bp.BoundDateUtc DESC, bp.PolicyNumber;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new PolicyCheckCenterDto
        {
            Checks = (await multi.ReadAsync<PolicyCheckDto>()).AsList(),
            Statuses = (await multi.ReadAsync<PolicyCheckStatusDto>()).AsList(),
            ItemDefinitions = (await multi.ReadAsync<PolicyCheckItemDefinitionDto>()).AsList(),
            DiscrepancyTypes = (await multi.ReadAsync<PolicyCheckDiscrepancyTypeDto>()).AsList(),
            EligiblePolicies = (await multi.ReadAsync<PolicyCheckEligiblePolicyDto>()).AsList()
        };
    }

    public async Task<PolicyCheckDetailDto?> GetDetailAsync(Guid policyCheckId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {CheckColumns}
FROM Policy.PolicyCheck
WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0;

SELECT PolicyCheckItemId, TenantId, PolicyCheckId, PolicyCheckItemDefinitionId, ItemCode, ItemName, CategoryName,
       ExpectedValue, ActualValue, MatchStatusCode, SeverityCode, IsRequired, Notes, CheckedByName, CheckedDateUtc, SortOrder
FROM Policy.PolicyCheckItem
WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0
ORDER BY SortOrder;

SELECT PolicyCheckDiscrepancyId, TenantId, PolicyCheckId, PolicyCheckItemId, TypeCode, TypeName, SeverityCode, StatusCode,
       Description, CarrierNotified, CarrierNotifiedDateUtc, CarrierReferenceNumber, ResolutionNotes, ResolvedByName, ResolvedDateUtc, CreatedDateUtc
FROM Policy.PolicyCheckDiscrepancy
WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;

SELECT ActivityId, TenantId, PolicyCheckId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc
FROM Policy.PolicyCheckActivity
WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { PolicyCheckId = policyCheckId }, cancellationToken: cancellationToken));
        var check = await multi.ReadSingleOrDefaultAsync<PolicyCheckDto>();
        if (check is null) return null;

        return new PolicyCheckDetailDto
        {
            Check = check,
            Items = (await multi.ReadAsync<PolicyCheckItemDto>()).AsList(),
            Discrepancies = (await multi.ReadAsync<PolicyCheckDiscrepancyDto>()).AsList(),
            Activities = (await multi.ReadAsync<PolicyCheckActivityDto>()).AsList()
        };
    }

    public async Task<Guid> CreateAsync(CreatePolicyCheckRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NextNumber INT = ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyCheck WHERE TenantId = @TenantId), 1);
DECLARE @CheckNumber NVARCHAR(50) = CONCAT(N'CHK-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(@NextNumber, N'0000'));

INSERT INTO Policy.PolicyCheck
(PolicyCheckId, TenantId, PolicyId, AccountId, QuoteId, CheckNumber, PolicyNumber, AccountName, CarrierName, LineOfBusiness,
 PolicyEffectiveDate, PolicyExpirationDate, StatusCode, PriorityCode, CheckTypeCode, AssignedToUserId, AssignedToName,
 DueDate, ReceivedDateUtc, Notes, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@PolicyCheckId, @TenantId, @PolicyId, @AccountId, @QuoteId, @CheckNumber, @PolicyNumber, @AccountName, @CarrierName, @LineOfBusiness,
 @PolicyEffectiveDate, @PolicyExpirationDate, N'Pending', @PriorityCode, @CheckTypeCode, @AssignedToUserId, @AssignedToName,
 @DueDate, SYSUTCDATETIME(), @Notes, @IsUrgent, 0, SYSUTCDATETIME(), @CreatedByUserId, 0);

-- Materialize checklist items from tenant definitions with expected values from the bound quote where available.
INSERT INTO Policy.PolicyCheckItem
(PolicyCheckItemId, TenantId, PolicyCheckId, PolicyCheckItemDefinitionId, ItemCode, ItemName, CategoryName,
 ExpectedValue, MatchStatusCode, SeverityCode, IsRequired, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyCheckId, d.PolicyCheckItemDefinitionId, d.ItemCode, d.ItemName, d.CategoryName,
       CASE d.ItemCode
           WHEN N'NAMED_INSURED'  THEN @AccountName
           WHEN N'POLICY_NUMBER'  THEN @PolicyNumber
           WHEN N'EFFECTIVE_DATE' THEN CONVERT(NVARCHAR(10), @PolicyEffectiveDate, 120)
           WHEN N'EXPIRATION_DATE' THEN CONVERT(NVARCHAR(10), @PolicyExpirationDate, 120)
           WHEN N'CARRIER_WRITING_CO' THEN @CarrierName
            WHEN N'PREMIUM'        THEN CONVERT(NVARCHAR(30), q.AnnualPremium)
            WHEN N'COMMISSION'     THEN CONVERT(NVARCHAR(30), q.CommissionPercent)
            WHEN N'LIMITS'         THEN CONVERT(NVARCHAR(50), q.[Limit])
            WHEN N'DEDUCTIBLES'    THEN CONVERT(NVARCHAR(50), q.Deductible)
           WHEN N'BILLING_PLAN'   THEN q.PaymentTerms
           WHEN N'SUBJECTIVITIES' THEN q.Subjectivities
           ELSE NULL
       END,
       N'Unchecked', d.DefaultSeverityCode, d.IsRequired, d.SortOrder, SYSUTCDATETIME(), @CreatedByUserId, 0
FROM Policy.PolicyCheckItemDefinition d
LEFT JOIN Submissions.Quote q ON q.QuoteId = @QuoteId
WHERE d.TenantId = @TenantId AND d.IsActive = 1 AND d.IsDeleted = 0;

UPDATE Policy.PolicyCheck
SET ItemsTotal = (SELECT COUNT(1) FROM Policy.PolicyCheckItem WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0)
WHERE PolicyCheckId = @PolicyCheckId;

INSERT INTO Policy.PolicyCheckActivity (ActivityId, TenantId, PolicyCheckId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedByUserId)
VALUES (NEWID(), @TenantId, @PolicyCheckId, N'Created', N'Policy check created',
        CONCAT(N'Policy check ', @CheckNumber, N' opened for policy ', @PolicyNumber, N'.'), ISNULL(@AssignedToName, N'System'), SYSUTCDATETIME(), @CreatedByUserId);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyCheckId = id,
            request.TenantId,
            request.PolicyId,
            request.AccountId,
            request.QuoteId,
            request.PolicyNumber,
            request.AccountName,
            request.CarrierName,
            request.LineOfBusiness,
            request.PolicyEffectiveDate,
            request.PolicyExpirationDate,
            request.PriorityCode,
            request.CheckTypeCode,
            request.AssignedToUserId,
            request.AssignedToName,
            request.DueDate,
            request.Notes,
            request.IsUrgent,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid policyCheckId, UpdatePolicyCheckRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCheck SET
    PriorityCode = @PriorityCode,
    CheckTypeCode = @CheckTypeCode,
    AssignedToUserId = @AssignedToUserId,
    AssignedToName = @AssignedToName,
    DueDate = @DueDate,
    ResultSummary = @ResultSummary,
    Notes = @Notes,
    IsUrgent = @IsUrgent,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyCheckId = policyCheckId,
            request.PriorityCode,
            request.CheckTypeCode,
            request.AssignedToUserId,
            request.AssignedToName,
            request.DueDate,
            request.ResultSummary,
            request.Notes,
            request.IsUrgent,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid policyCheckId, UpdatePolicyCheckStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER, @CheckNumber NVARCHAR(50), @IsTerminal BIT = 0;
SELECT @TenantId = TenantId, @CheckNumber = CheckNumber FROM Policy.PolicyCheck WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0;
IF @TenantId IS NULL RETURN;

IF NOT EXISTS (SELECT 1 FROM Policy.PolicyCheckStatus WHERE TenantId = @TenantId AND StatusCode = @StatusCode AND IsDeleted = 0 AND IsActive = 1)
    THROW 50001, 'Invalid policy check status for tenant.', 1;

SELECT @IsTerminal = IsTerminal FROM Policy.PolicyCheckStatus WHERE TenantId = @TenantId AND StatusCode = @StatusCode AND IsDeleted = 0;

UPDATE Policy.PolicyCheck SET
    StatusCode = @StatusCode,
    CompletedDateUtc = CASE WHEN @IsTerminal = 1 THEN SYSUTCDATETIME() ELSE NULL END,
    CompletedByName = CASE WHEN @IsTerminal = 1 THEN @CreatedByName ELSE NULL END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0;

-- Keep the bound policy verification status in sync with the check outcome.
UPDATE bp SET VerificationStatusCode = CASE WHEN @StatusCode IN (N'Passed', N'PassedWithNotes', N'Resolved', N'Closed') THEN N'Verified' ELSE N'PendingVerification' END,
              ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.BoundPolicy bp
JOIN Policy.PolicyCheck pc ON pc.PolicyId = bp.PolicyId
WHERE pc.PolicyCheckId = @PolicyCheckId AND bp.IsDeleted = 0;

INSERT INTO Policy.PolicyCheckActivity (ActivityId, TenantId, PolicyCheckId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedByUserId)
VALUES (NEWID(), @TenantId, @PolicyCheckId, N'StatusChange', CONCAT(N'Status changed to ', @StatusCode), @Notes, @CreatedByName, SYSUTCDATETIME(), @ModifiedByUserId);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyCheckId = policyCheckId,
            request.StatusCode,
            request.Notes,
            request.CreatedByName,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateItemAsync(Guid policyCheckItemId, UpdatePolicyCheckItemRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCheckItem SET
    ExpectedValue = @ExpectedValue,
    ActualValue = @ActualValue,
    MatchStatusCode = @MatchStatusCode,
    Notes = @Notes,
    CheckedByName = @CheckedByName,
    CheckedDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE PolicyCheckItemId = @PolicyCheckItemId AND IsDeleted = 0;

UPDATE pc SET
    ItemsTotal = agg.Total,
    ItemsMatched = agg.Matched,
    ItemsDiscrepant = agg.Discrepant,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Policy.PolicyCheck pc
JOIN Policy.PolicyCheckItem i ON i.PolicyCheckId = pc.PolicyCheckId AND i.PolicyCheckItemId = @PolicyCheckItemId
CROSS APPLY (
    SELECT COUNT(1) AS Total,
           SUM(CASE WHEN x.MatchStatusCode = N'Match' THEN 1 ELSE 0 END) AS Matched,
           SUM(CASE WHEN x.MatchStatusCode = N'Discrepancy' THEN 1 ELSE 0 END) AS Discrepant
    FROM Policy.PolicyCheckItem x
    WHERE x.PolicyCheckId = pc.PolicyCheckId AND x.IsDeleted = 0
) agg;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyCheckItemId = policyCheckItemId,
            request.ExpectedValue,
            request.ActualValue,
            request.MatchStatusCode,
            request.Notes,
            request.CheckedByName,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddDiscrepancyAsync(AddPolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Policy.PolicyCheckDiscrepancy
(PolicyCheckDiscrepancyId, TenantId, PolicyCheckId, PolicyCheckItemId, TypeCode, TypeName, SeverityCode, StatusCode, Description,
 CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@PolicyCheckDiscrepancyId, @TenantId, @PolicyCheckId, @PolicyCheckItemId, @TypeCode, @TypeName, @SeverityCode, N'Open', @Description,
 SYSUTCDATETIME(), @CreatedByUserId, 0);

UPDATE Policy.PolicyCheck SET StatusCode = N'DiscrepanciesFound', CompletedDateUtc = NULL, CompletedByName = NULL,
    ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @CreatedByUserId
WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0 AND StatusCode NOT IN (N'SentToCarrier');

INSERT INTO Policy.PolicyCheckActivity (ActivityId, TenantId, PolicyCheckId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedByUserId)
VALUES (NEWID(), @TenantId, @PolicyCheckId, N'Discrepancy', CONCAT(N'Discrepancy logged: ', @TypeName), @Description, @CreatedByName, SYSUTCDATETIME(), @CreatedByUserId);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyCheckDiscrepancyId = id,
            request.TenantId,
            request.PolicyCheckId,
            request.PolicyCheckItemId,
            request.TypeCode,
            request.TypeName,
            request.SeverityCode,
            request.Description,
            request.CreatedByName,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ResolveDiscrepancyAsync(Guid policyCheckDiscrepancyId, ResolvePolicyCheckDiscrepancyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER, @PolicyCheckId UNIQUEIDENTIFIER, @TypeName NVARCHAR(120);
SELECT @TenantId = TenantId, @PolicyCheckId = PolicyCheckId, @TypeName = TypeName
FROM Policy.PolicyCheckDiscrepancy WHERE PolicyCheckDiscrepancyId = @PolicyCheckDiscrepancyId AND IsDeleted = 0;
IF @TenantId IS NULL RETURN;

UPDATE Policy.PolicyCheckDiscrepancy SET
    StatusCode = @StatusCode,
    CarrierNotified = @CarrierNotified,
    CarrierNotifiedDateUtc = CASE WHEN @CarrierNotified = 1 AND CarrierNotifiedDateUtc IS NULL THEN SYSUTCDATETIME() ELSE CarrierNotifiedDateUtc END,
    CarrierReferenceNumber = @CarrierReferenceNumber,
    ResolutionNotes = @ResolutionNotes,
    ResolvedByName = CASE WHEN @StatusCode IN (N'Resolved', N'Waived') THEN @ResolvedByName ELSE ResolvedByName END,
    ResolvedDateUtc = CASE WHEN @StatusCode IN (N'Resolved', N'Waived') THEN SYSUTCDATETIME() ELSE ResolvedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE PolicyCheckDiscrepancyId = @PolicyCheckDiscrepancyId AND IsDeleted = 0;

INSERT INTO Policy.PolicyCheckActivity (ActivityId, TenantId, PolicyCheckId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedByUserId)
VALUES (NEWID(), @TenantId, @PolicyCheckId, N'DiscrepancyUpdate', CONCAT(N'Discrepancy ', @StatusCode, N': ', @TypeName), @ResolutionNotes, @ResolvedByName, SYSUTCDATETIME(), @ModifiedByUserId);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyCheckDiscrepancyId = policyCheckDiscrepancyId,
            request.StatusCode,
            request.CarrierNotified,
            request.CarrierReferenceNumber,
            request.ResolutionNotes,
            request.ResolvedByName,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddActivityAsync(AddPolicyCheckActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Policy.PolicyCheckActivity (ActivityId, TenantId, PolicyCheckId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedByUserId)
VALUES (@ActivityId, @TenantId, @PolicyCheckId, @ActivityType, @Subject, @Notes, @CreatedByName, SYSUTCDATETIME(), @CreatedByUserId);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ActivityId = id,
            request.TenantId,
            request.PolicyCheckId,
            request.ActivityType,
            request.Subject,
            request.Notes,
            request.CreatedByName,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ArchiveAsync(Guid policyCheckId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCheck SET IsArchived = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE PolicyCheckId = @PolicyCheckId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PolicyCheckId = policyCheckId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
