using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyLifecycle;
using Dapper;
using System.Data;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyLifecycleRepository : IPolicyLifecycleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PolicyLifecycleRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<PolicyLifecycleOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT PolicyLifecycleOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsTerminal, IsPremiumBearing, RequiresDocument, IsDefault, SortOrder
FROM Policy.PolicyLifecycleOption
WHERE TenantId = @TenantId
  AND IsActive = 1
  AND IsDeleted = 0
ORDER BY OptionGroupCode, SortOrder, DisplayName;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PolicyLifecycleOptionDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PolicyLifecycleWorkbenchRowDto>> GetWorkbenchAsync(Guid tenantId, string? mode = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
WITH PolicyRows AS
(
    SELECT bp.PolicyId AS Id,
           bp.TenantId,
           bp.PolicyId,
           pt.PolicyTermId,
           CAST(NULL AS UNIQUEIDENTIFIER) AS PolicyTransactionId,
           N'policies' AS Mode,
           bp.PolicyNumber AS Number,
           CONCAT(COALESCE(NULLIF(bp.LineOfBusiness, N''), N'Policy'), N' policy lifecycle') AS Title,
           COALESCE(a.AccountName, N'Account') AS Account,
           COALESCE(c.CarrierName, c.Name, N'Carrier') AS Carrier,
           COALESCE(pa.ProducerName, pa.AccountManagerName, pa.CsrName, N'Unassigned') AS Owner,
           COALESCE(pa.Branch, N'HQ') AS Branch,
           COALESCE(NULLIF(bp.LineOfBusiness, N''), N'Policy') AS Type,
           COALESCE(NULLIF(bp.CoverageStatus, N''), NULLIF(bp.Status, N''), N'Active') AS Status,
           CAST(bp.ExpirationDate AS DATETIME2) AS [Date],
           COALESCE(bp.AnnualPremium, 0) AS Amount,
           COALESCE(docs.DocumentCount, 0) AS DocumentCount,
           COALESCE(ver.VersionNumber, 1) AS VersionNumber,
           CAST(NULL AS NVARCHAR(80)) AS NextStatusCode,
           CAST(NULL AS NVARCHAR(160)) AS NextStatusDisplayName,
           CAST(0 AS BIT) AS NextTransitionRequiresDocument,
           CAST(0 AS BIT) AS NextTransitionRequiresApproval
    FROM Submissions.BoundPolicy bp
    LEFT JOIN Client.Account a ON a.AccountId = bp.AccountId AND a.TenantId = bp.TenantId AND a.IsDeleted = 0
    LEFT JOIN Agency.Carrier c ON c.CarrierId = bp.CarrierId AND c.TenantId = bp.TenantId AND c.IsDeleted = 0
    OUTER APPLY (SELECT TOP 1 PolicyTermId FROM Policy.PolicyTerm WHERE TenantId = bp.TenantId AND PolicyId = bp.PolicyId AND IsDeleted = 0 ORDER BY TermNumber DESC, CreatedDateUtc DESC) pt
    OUTER APPLY (SELECT TOP 1 ProducerName, AccountManagerName, CsrName, Branch FROM Policy.PolicyAssignment WHERE TenantId = bp.TenantId AND PolicyId = bp.PolicyId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC) pa
    OUTER APPLY (SELECT COUNT(1) AS DocumentCount FROM Policy.PolicyTransactionDocument WHERE TenantId = bp.TenantId AND PolicyId = bp.PolicyId AND IsDeleted = 0) docs
    OUTER APPLY (SELECT MAX(VersionNumber) AS VersionNumber FROM Policy.PolicyVersion WHERE TenantId = bp.TenantId AND PolicyId = bp.PolicyId AND IsDeleted = 0) ver
    WHERE bp.TenantId = @TenantId AND bp.IsDeleted = 0
),
TransactionRows AS
(
    SELECT tx.PolicyId AS Id,
           tx.TenantId,
           tx.PolicyId,
           tx.PolicyTermId,
           tx.PolicyTransactionId,
           CASE WHEN tx.TransactionTypeCode = N'Endorsement' THEN N'endorsements'
                WHEN tx.TransactionTypeCode IN (N'Cancellation', N'Reinstatement', N'NonRenewal') THEN N'cancellations'
                ELSE N'policies' END AS Mode,
           tx.TransactionNumber AS Number,
           COALESCE(NULLIF(tx.Description, N''), CONCAT(tx.TransactionTypeCode, N' transaction for ', bp.PolicyNumber)) AS Title,
           COALESCE(a.AccountName, N'Account') AS Account,
           COALESCE(c.CarrierName, c.Name, N'Carrier') AS Carrier,
           COALESCE(pa.ProducerName, pa.AccountManagerName, pa.CsrName, N'Unassigned') AS Owner,
           COALESCE(pa.Branch, N'HQ') AS Branch,
           tx.TransactionTypeCode AS Type,
           tx.TransactionStatusCode AS Status,
           CAST(tx.EffectiveDate AS DATETIME2) AS [Date],
           COALESCE(tx.PremiumChange, tx.NewWrittenPremium, 0) AS Amount,
           tx.DocumentCount,
           tx.CurrentVersionNumber AS VersionNumber,
           nextTransition.ToStatusCode AS NextStatusCode,
           nextStatus.DisplayName AS NextStatusDisplayName,
           COALESCE(nextTransition.RequiresDocument, 0) AS NextTransitionRequiresDocument,
           COALESCE(nextTransition.RequiresApproval, 0) AS NextTransitionRequiresApproval
    FROM Policy.PolicyTransaction tx
    INNER JOIN Submissions.BoundPolicy bp ON bp.TenantId = tx.TenantId AND bp.PolicyId = tx.PolicyId AND bp.IsDeleted = 0
    LEFT JOIN Client.Account a ON a.AccountId = bp.AccountId AND a.TenantId = bp.TenantId AND a.IsDeleted = 0
    LEFT JOIN Agency.Carrier c ON c.CarrierId = bp.CarrierId AND c.TenantId = bp.TenantId AND c.IsDeleted = 0
    OUTER APPLY (SELECT TOP 1 ProducerName, AccountManagerName, CsrName, Branch FROM Policy.PolicyAssignment WHERE TenantId = tx.TenantId AND PolicyId = tx.PolicyId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC) pa
    OUTER APPLY
    (
        SELECT TOP 1 transition.ToStatusCode, transition.RequiresDocument, transition.RequiresApproval
        FROM Policy.PolicyTransactionTransition transition
        WHERE transition.TenantId = tx.TenantId
          AND transition.FromStatusCode = tx.TransactionStatusCode
          AND (transition.TransactionTypeCode IS NULL OR transition.TransactionTypeCode = tx.TransactionTypeCode)
          AND transition.IsActive = 1
          AND transition.IsDeleted = 0
        ORDER BY CASE WHEN transition.TransactionTypeCode = tx.TransactionTypeCode THEN 0 ELSE 1 END, transition.SortOrder
    ) nextTransition
    LEFT JOIN Policy.PolicyLifecycleOption nextStatus
      ON nextStatus.TenantId = tx.TenantId
     AND nextStatus.OptionGroupCode = N'PolicyTransactionStatus'
     AND nextStatus.OptionCode = nextTransition.ToStatusCode
     AND nextStatus.IsActive = 1
     AND nextStatus.IsDeleted = 0
    WHERE tx.TenantId = @TenantId AND tx.IsDeleted = 0 AND tx.TransactionTypeCode <> N'NewBusiness'
)
SELECT *
FROM
(
    SELECT * FROM PolicyRows
    UNION ALL
    SELECT * FROM TransactionRows
) rows
WHERE NULLIF(@Mode, N'') IS NULL OR Mode = @Mode
ORDER BY [Date], Number;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PolicyLifecycleWorkbenchRowDto>(new CommandDefinition(sql, new { TenantId = tenantId, Mode = mode }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<PolicyLifecycleDetailDto?> GetDetailAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM Submissions.BoundPolicy WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0)
BEGIN
    SELECT CAST(0 AS BIT) AS Found;
    RETURN;
END;

SELECT CAST(1 AS BIT) AS Found;

SELECT bp.PolicyId,
       bp.PolicyNumber,
       COALESCE(a.AccountName, N'Account') AS AccountName,
       COALESCE(c.CarrierName, c.Name, N'Carrier') AS CarrierName,
       COALESCE(NULLIF(bp.LineOfBusiness, N''), N'Policy') AS LineOfBusiness,
       COALESCE(NULLIF(bp.CoverageStatus, N''), NULLIF(bp.Status, N''), N'Active') AS Status,
       CAST(bp.EffectiveDate AS DATETIME2) AS EffectiveDate,
       CAST(bp.ExpirationDate AS DATETIME2) AS ExpirationDate,
       COALESCE(bp.AnnualPremium, 0) AS AnnualPremium
FROM Submissions.BoundPolicy bp
LEFT JOIN Client.Account a ON a.AccountId = bp.AccountId AND a.TenantId = bp.TenantId AND a.IsDeleted = 0
LEFT JOIN Agency.Carrier c ON c.CarrierId = bp.CarrierId AND c.TenantId = bp.TenantId AND c.IsDeleted = 0
WHERE bp.TenantId = @TenantId AND bp.PolicyId = @PolicyId AND bp.IsDeleted = 0;

SELECT PolicyTransactionId, TenantId, PolicyId, PolicyTermId, ParentPolicyTransactionId, SupersedesPolicyTransactionId, TransactionNumber, TransactionTypeCode, TransactionStatusCode,
       CAST(EffectiveDate AS DATETIME2) AS EffectiveDate, CAST(ExpirationDate AS DATETIME2) AS ExpirationDate, RequestedDateUtc, ApprovedDateUtc, IssuedDateUtc, ProcessedDateUtc,
       PriorWrittenPremium, PremiumChange, NewWrittenPremium, TaxesChange, FeesChange, SurchargesChange, TotalCostChange, ReasonCode, SourceCode, ExternalReference,
       CarrierTransactionNumber, Description, Notes, RequestedByUserId, ApprovedByUserId, IssuedByUserId, CurrentVersionNumber, DocumentCount, CreatedDateUtc
FROM Policy.PolicyTransaction
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0
ORDER BY EffectiveDate DESC, CreatedDateUtc DESC;

SELECT PolicyTransactionLineChangeId, PolicyTransactionId, PolicyId, PolicyTermId, PolicyLineId, LineOfBusinessId, LineOfBusinessCode, LineOfBusinessName, ChangeTypeCode,
       PriorPremium, PremiumChange, NewPremium, BeforeJson, AfterJson
FROM Policy.PolicyTransactionLineChange
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;

SELECT PolicyTransactionDocumentId, PolicyTransactionId, PolicyId, DocumentId, DocumentRoleCode, DocumentTitle, DocumentNumber, FileName, StorageUri, LinkedDateUtc
FROM Policy.PolicyTransactionDocument
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0
ORDER BY LinkedDateUtc DESC;

SELECT PolicyTermHistoryId, PolicyId, PolicyTermId, PolicyTransactionId, TermNumber, TermStatusCode, CAST(EffectiveDate AS DATETIME2) AS EffectiveDate, CAST(ExpirationDate AS DATETIME2) AS ExpirationDate,
       WrittenPremium, AnnualizedPremium, TotalCost, SnapshotJson, CreatedDateUtc
FROM Policy.PolicyTermHistory
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;

SELECT PolicyVersionId, PolicyId, PolicyTermId, PolicyTransactionId, VersionNumber, VersionReasonCode, SnapshotJson, CreatedDateUtc
FROM Policy.PolicyVersion
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0
ORDER BY VersionNumber DESC;

SELECT PolicyStatusHistoryId, PolicyId, PolicyTermId, PolicyTransactionId, StatusScopeCode, OldStatusCode, NewStatusCode, ReasonCode, Notes, ChangedDateUtc, ChangedByUserId
FROM Policy.PolicyStatusHistory
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0
ORDER BY ChangedDateUtc DESC;

SELECT PolicyTransactionTransitionId, TenantId, TransactionTypeCode, FromStatusCode, ToStatusCode, RequiresDocument, RequiresApproval, SortOrder
FROM Policy.PolicyTransactionTransition
WHERE TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0
ORDER BY SortOrder, ToStatusCode;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, PolicyId = policyId }, cancellationToken: cancellationToken));
        var found = await grid.ReadSingleAsync<bool>();
        if (!found)
        {
            return null;
        }

        var policy = await grid.ReadSingleAsync<PolicyLifecyclePolicySummaryDto>();

        return new PolicyLifecycleDetailDto
        {
            TenantId = tenantId,
            PolicyId = policyId,
            Policy = policy,
            Transactions = (await grid.ReadAsync<PolicyTransactionDto>()).AsList(),
            LineChanges = (await grid.ReadAsync<PolicyTransactionLineChangeDto>()).AsList(),
            Documents = (await grid.ReadAsync<PolicyTransactionDocumentDto>()).AsList(),
            TermHistory = (await grid.ReadAsync<PolicyTermHistoryDto>()).AsList(),
            Versions = (await grid.ReadAsync<PolicyVersionDto>()).AsList(),
            StatusHistory = (await grid.ReadAsync<PolicyStatusHistoryDto>()).AsList(),
            Transitions = (await grid.ReadAsync<PolicyTransactionTransitionDto>()).AsList()
        };
    }

    public async Task<Guid> CreateTransactionAsync(CreatePolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default)
    {
        const string insertTransactionSql = @"
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @PolicyTermId UNIQUEIDENTIFIER = @RequestedPolicyTermId;
DECLARE @PolicyStatus NVARCHAR(80);
DECLARE @TermStatus NVARCHAR(80);
DECLARE @PolicyNumber NVARCHAR(80);

IF NOT EXISTS (SELECT 1 FROM Policy.PolicyLifecycleOption WHERE TenantId = @TenantId AND OptionGroupCode = N'PolicyTransactionType' AND OptionCode = @TransactionTypeCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52340, 'Policy transaction type is not configured for this tenant.', 1;
IF NOT EXISTS (SELECT 1 FROM Policy.PolicyLifecycleOption WHERE TenantId = @TenantId AND OptionGroupCode = N'PolicyTransactionStatus' AND OptionCode = @TransactionStatusCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52341, 'Policy transaction status is not configured for this tenant.', 1;
IF EXISTS (SELECT 1 FROM Policy.PolicyLifecycleOption WHERE TenantId = @TenantId AND OptionGroupCode = N'PolicyTransactionStatus' AND OptionCode = @TransactionStatusCode AND RequiresDocument = 1 AND IsActive = 1 AND IsDeleted = 0) AND @RequestedDocumentCount = 0
    THROW 52345, 'The selected transaction status requires linked documentation.', 1;

SELECT @PolicyStatus = Status, @PolicyNumber = PolicyNumber
FROM Submissions.BoundPolicy
WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0;
IF @PolicyStatus IS NULL THROW 52342, 'Policy was not found for the tenant.', 1;

IF @PolicyTermId IS NULL
BEGIN
    SELECT TOP 1 @PolicyTermId = PolicyTermId
    FROM Policy.PolicyTerm
    WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0
    ORDER BY TermNumber DESC, CreatedDateUtc DESC;
END;

SELECT @TermStatus = TermStatusCode FROM Policy.PolicyTerm WHERE TenantId = @TenantId AND PolicyTermId = @PolicyTermId AND IsDeleted = 0;

INSERT INTO Policy.PolicyTransaction
    (PolicyTransactionId, TenantId, PolicyId, PolicyTermId, ParentPolicyTransactionId, SupersedesPolicyTransactionId, TransactionNumber, TransactionTypeCode, TransactionStatusCode,
     EffectiveDate, ExpirationDate, RequestedDateUtc, ApprovedDateUtc, IssuedDateUtc, ProcessedDateUtc, PriorWrittenPremium, PremiumChange, NewWrittenPremium, TaxesChange, FeesChange,
     SurchargesChange, TotalCostChange, ReasonCode, SourceCode, ExternalReference, CarrierTransactionNumber, Description, Notes, RequestedByUserId, ApprovedByUserId, IssuedByUserId,
     CurrentVersionNumber, DocumentCount, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@PolicyTransactionId, @TenantId, @PolicyId, @PolicyTermId, @ParentPolicyTransactionId, @SupersedesPolicyTransactionId,
     CONCAT(N'PTR-', FORMAT(@Now, N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @PolicyTransactionId), N'-', N''), 6)),
     @TransactionTypeCode, @TransactionStatusCode, @EffectiveDate, @ExpirationDate, @Now,
     CASE WHEN @TransactionStatusCode IN (N'Approved', N'Issued', N'Completed') THEN @Now ELSE NULL END,
     CASE WHEN @TransactionStatusCode IN (N'Issued', N'Completed') THEN @Now ELSE NULL END,
     CASE WHEN @TransactionStatusCode = N'Completed' THEN @Now ELSE NULL END,
     @PriorWrittenPremium, @PremiumChange, @NewWrittenPremium, @TaxesChange, @FeesChange, @SurchargesChange, @TotalCostChange, @ReasonCode, @SourceCode, @ExternalReference,
     @CarrierTransactionNumber, @Description, @Notes, @RequestedByUserId, @ApprovedByUserId, @IssuedByUserId, 1, 0, @Now, @RequestedByUserId, 0);

IF @TransactionStatusCode IN (N'Issued', N'Completed')
BEGIN
    UPDATE Submissions.BoundPolicy
    SET Status = CASE @TransactionTypeCode
                    WHEN N'Cancellation' THEN N'Cancelled'
                    WHEN N'NonRenewal' THEN N'NonRenewed'
                    WHEN N'Rewrite' THEN N'Rewritten'
                    WHEN N'Reinstatement' THEN N'Active'
                    ELSE Status
                 END,
        CoverageStatus = CASE @TransactionTypeCode
                    WHEN N'Cancellation' THEN N'Cancelled'
                    WHEN N'NonRenewal' THEN N'NonRenewed'
                    WHEN N'Rewrite' THEN N'Rewritten'
                    WHEN N'Reinstatement' THEN N'Active'
                    ELSE CoverageStatus
                 END,
        AnnualPremium = COALESCE(@NewWrittenPremium, AnnualPremium),
        ModifiedDateUtc = @Now,
        ModifiedByUserId = @RequestedByUserId
    WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0;
END;

INSERT INTO Policy.PolicyStatusHistory
    (PolicyStatusHistoryId, TenantId, PolicyId, PolicyTermId, PolicyTransactionId, StatusScopeCode, OldStatusCode, NewStatusCode, ReasonCode, Notes, ChangedDateUtc, ChangedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyId, @PolicyTermId, @PolicyTransactionId, N'Transaction', NULL, @TransactionStatusCode, @ReasonCode, @Notes, @Now, @RequestedByUserId, @Now, @RequestedByUserId, 0);

INSERT INTO Policy.PolicyTermHistory
    (PolicyTermHistoryId, TenantId, PolicyId, PolicyTermId, PolicyTransactionId, TermNumber, TermStatusCode, EffectiveDate, ExpirationDate, WrittenPremium, AnnualizedPremium, TotalCost, SnapshotJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, pt.PolicyTermId, @PolicyTransactionId, pt.TermNumber, pt.TermStatusCode, pt.EffectiveDate, pt.ExpirationDate,
       COALESCE(@NewWrittenPremium, pt.WrittenPremium), COALESCE(@NewWrittenPremium, pt.AnnualizedPremium), COALESCE(@TotalCostChange, pt.TotalCost),
       JSON_OBJECT(N'PolicyId': @PolicyId, N'PolicyNumber': @PolicyNumber, N'TransactionId': @PolicyTransactionId, N'TransactionType': @TransactionTypeCode, N'TransactionStatus': @TransactionStatusCode, N'PriorTermStatus': @TermStatus),
       @Now, @RequestedByUserId, 0
FROM Policy.PolicyTerm pt
WHERE pt.TenantId = @TenantId AND pt.PolicyTermId = @PolicyTermId AND pt.IsDeleted = 0;

INSERT INTO Policy.PolicyVersion
    (PolicyVersionId, TenantId, PolicyId, PolicyTermId, PolicyTransactionId, VersionNumber, VersionReasonCode, SnapshotJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @PolicyTermId, @PolicyTransactionId,
       COALESCE((SELECT MAX(VersionNumber) FROM Policy.PolicyVersion WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0), 0) + 1,
       @TransactionTypeCode,
       JSON_OBJECT(N'PolicyId': @PolicyId, N'PolicyNumber': @PolicyNumber, N'TransactionId': @PolicyTransactionId, N'TransactionType': @TransactionTypeCode, N'TransactionStatus': @TransactionStatusCode, N'PremiumChange': @PremiumChange, N'NewWrittenPremium': @NewWrittenPremium),
       @Now, @RequestedByUserId, 0;

SELECT @PolicyTransactionId;";

        const string insertLineSql = @"
IF NOT EXISTS (SELECT 1 FROM Policy.PolicyLifecycleOption WHERE TenantId = @TenantId AND OptionGroupCode = N'PolicyLineChangeType' AND OptionCode = @ChangeTypeCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52343, 'Policy line change type is not configured for this tenant.', 1;

INSERT INTO Policy.PolicyTransactionLineChange
    (PolicyTransactionLineChangeId, TenantId, PolicyTransactionId, PolicyId, PolicyTermId, PolicyLineId, LineOfBusinessId, LineOfBusinessCode, LineOfBusinessName, ChangeTypeCode,
     PriorPremium, PremiumChange, NewPremium, BeforeJson, AfterJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyTransactionId, @PolicyId, @PolicyTermId, @PolicyLineId, @LineOfBusinessId, @LineOfBusinessCode, @LineOfBusinessName, @ChangeTypeCode,
     @PriorPremium, @PremiumChange, @NewPremium, @BeforeJson, @AfterJson, SYSUTCDATETIME(), @CreatedByUserId, 0);";

        const string insertDocumentSql = @"
IF NOT EXISTS (SELECT 1 FROM Policy.PolicyLifecycleOption WHERE TenantId = @TenantId AND OptionGroupCode = N'PolicyDocumentRole' AND OptionCode = @DocumentRoleCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52344, 'Policy transaction document role is not configured for this tenant.', 1;

INSERT INTO Policy.PolicyTransactionDocument
    (PolicyTransactionDocumentId, TenantId, PolicyTransactionId, PolicyId, DocumentId, DocumentRoleCode, DocumentTitle, DocumentNumber, FileName, StorageUri, LinkedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyTransactionId, @PolicyId, @DocumentId, @DocumentRoleCode, @DocumentTitle, @DocumentNumber, @FileName, @StorageUri, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);";

        const string updateDocumentCountSql = @"
UPDATE Policy.PolicyTransaction
SET DocumentCount = (SELECT COUNT(1) FROM Policy.PolicyTransactionDocument WHERE TenantId = @TenantId AND PolicyTransactionId = @PolicyTransactionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE TenantId = @TenantId AND PolicyTransactionId = @PolicyTransactionId;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var policyTransactionId = Guid.NewGuid();
        try
        {
            var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(insertTransactionSql, new
            {
                PolicyTransactionId = policyTransactionId,
                request.TenantId,
                request.PolicyId,
                RequestedPolicyTermId = request.PolicyTermId,
                request.ParentPolicyTransactionId,
                request.SupersedesPolicyTransactionId,
                TransactionTypeCode = request.TransactionTypeCode.Trim(),
                TransactionStatusCode = request.TransactionStatusCode.Trim(),
                EffectiveDate = request.EffectiveDate.ToDateTime(TimeOnly.MinValue),
                ExpirationDate = request.ExpirationDate?.ToDateTime(TimeOnly.MinValue),
                request.PriorWrittenPremium,
                request.PremiumChange,
                request.NewWrittenPremium,
                request.TaxesChange,
                request.FeesChange,
                request.SurchargesChange,
                request.TotalCostChange,
                ReasonCode = request.ReasonCode?.Trim(),
                SourceCode = request.SourceCode?.Trim(),
                ExternalReference = request.ExternalReference?.Trim(),
                CarrierTransactionNumber = request.CarrierTransactionNumber?.Trim(),
                Description = request.Description?.Trim(),
                Notes = request.Notes?.Trim(),
                request.RequestedByUserId,
                request.ApprovedByUserId,
                request.IssuedByUserId,
                RequestedDocumentCount = request.Documents.Count
            }, transaction, cancellationToken: cancellationToken));

            foreach (var line in request.LineChanges)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertLineSql, new
                {
                    request.TenantId,
                    PolicyTransactionId = id,
                    request.PolicyId,
                    PolicyTermId = request.PolicyTermId ?? await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("SELECT PolicyTermId FROM Policy.PolicyTransaction WHERE TenantId = @TenantId AND PolicyTransactionId = @PolicyTransactionId AND IsDeleted = 0;", new { request.TenantId, PolicyTransactionId = id }, transaction, cancellationToken: cancellationToken)),
                    line.PolicyLineId,
                    line.LineOfBusinessId,
                    LineOfBusinessCode = line.LineOfBusinessCode.Trim(),
                    LineOfBusinessName = line.LineOfBusinessName.Trim(),
                    ChangeTypeCode = line.ChangeTypeCode.Trim(),
                    line.PriorPremium,
                    line.PremiumChange,
                    line.NewPremium,
                    line.BeforeJson,
                    line.AfterJson,
                    CreatedByUserId = request.RequestedByUserId
                }, transaction, cancellationToken: cancellationToken));
            }

            foreach (var document in request.Documents)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertDocumentSql, new
                {
                    request.TenantId,
                    PolicyTransactionId = id,
                    request.PolicyId,
                    document.DocumentId,
                    DocumentRoleCode = document.DocumentRoleCode.Trim(),
                    DocumentTitle = document.DocumentTitle.Trim(),
                    DocumentNumber = document.DocumentNumber?.Trim(),
                    FileName = document.FileName?.Trim(),
                    StorageUri = document.StorageUri?.Trim(),
                    CreatedByUserId = request.RequestedByUserId
                }, transaction, cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(updateDocumentCountSql, new { request.TenantId, PolicyTransactionId = id, ModifiedByUserId = request.RequestedByUserId }, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task TransitionTransactionAsync(Guid policyTransactionId, TransitionPolicyLifecycleTransactionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON;
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @PolicyId UNIQUEIDENTIFIER;
DECLARE @PolicyTermId UNIQUEIDENTIFIER;
DECLARE @TransactionTypeCode NVARCHAR(80);
DECLARE @FromStatusCode NVARCHAR(80);
DECLARE @PolicyStatus NVARCHAR(80);
DECLARE @NewPolicyStatus NVARCHAR(80);
DECLARE @NewTermStatus NVARCHAR(80);
DECLARE @DocumentCount INT;
DECLARE @RequiresDocument BIT;
DECLARE @RequiresApproval BIT;
DECLARE @EffectiveDate DATE;
DECLARE @ExpirationDate DATE;
DECLARE @NewWrittenPremium DECIMAL(18,2);
DECLARE @PremiumChange DECIMAL(18,2);
DECLARE @TotalCostChange DECIMAL(18,2);

SELECT @PolicyId = PolicyId,
       @PolicyTermId = PolicyTermId,
       @TransactionTypeCode = TransactionTypeCode,
       @FromStatusCode = TransactionStatusCode,
       @DocumentCount = DocumentCount,
       @EffectiveDate = EffectiveDate,
       @ExpirationDate = ExpirationDate,
       @NewWrittenPremium = NewWrittenPremium,
       @PremiumChange = PremiumChange,
       @TotalCostChange = TotalCostChange
FROM Policy.PolicyTransaction WITH (UPDLOCK, HOLDLOCK)
WHERE TenantId = @TenantId AND PolicyTransactionId = @PolicyTransactionId AND IsDeleted = 0;

IF @PolicyId IS NULL THROW 52346, 'Policy lifecycle transaction was not found for the tenant.', 1;
IF @FromStatusCode = @ToStatusCode THROW 52347, 'Policy lifecycle transaction is already in the requested status.', 1;
IF NOT EXISTS (SELECT 1 FROM Policy.PolicyLifecycleOption WHERE TenantId = @TenantId AND OptionGroupCode = N'PolicyTransactionStatus' AND OptionCode = @ToStatusCode AND IsActive = 1 AND IsDeleted = 0)
    THROW 52348, 'Target policy transaction status is not configured for this tenant.', 1;

SELECT TOP 1 @RequiresDocument = RequiresDocument, @RequiresApproval = RequiresApproval
FROM Policy.PolicyTransactionTransition
WHERE TenantId = @TenantId
  AND FromStatusCode = @FromStatusCode
  AND ToStatusCode = @ToStatusCode
  AND (TransactionTypeCode IS NULL OR TransactionTypeCode = @TransactionTypeCode)
  AND IsActive = 1
  AND IsDeleted = 0
ORDER BY CASE WHEN TransactionTypeCode = @TransactionTypeCode THEN 0 ELSE 1 END;

IF @RequiresDocument IS NULL THROW 52349, 'The requested policy transaction status transition is not allowed.', 1;
IF @RequiresDocument = 1 AND @DocumentCount = 0 THROW 52350, 'This policy transaction transition requires linked documentation.', 1;
IF @RequiresApproval = 1 AND @ChangedByUserId IS NULL THROW 52351, 'This policy transaction transition requires an identified approving user.', 1;

SELECT @PolicyStatus = Status FROM Submissions.BoundPolicy WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0;
IF @PolicyStatus IS NULL THROW 52352, 'Policy was not found for the tenant.', 1;

IF @TransactionTypeCode = N'Renewal' AND @ToStatusCode = N'Completed'
BEGIN
    IF @ExpirationDate IS NULL OR @ExpirationDate <= @EffectiveDate THROW 52353, 'A completed renewal requires a valid expiration date.', 1;

    DECLARE @PriorPolicyTermId UNIQUEIDENTIFIER = @PolicyTermId;
    SELECT @PolicyTermId = PolicyTermId
    FROM Policy.PolicyTerm
    WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND EffectiveDate = @EffectiveDate AND ExpirationDate = @ExpirationDate AND IsDeleted = 0;

    IF @PolicyTermId IS NULL
    BEGIN
        SET @PolicyTermId = NEWID();
        INSERT INTO Policy.PolicyTerm
            (PolicyTermId, TenantId, PolicyId, TermNumber, EffectiveDate, ExpirationDate, TermStatusCode, TransactionTypeCode,
             WrittenPremium, AnnualizedPremium, Taxes, Fees, Surcharges, TotalCost, BillingTypeCode, DataCompletenessCode,
             CreatedDateUtc, CreatedByUserId, IsDeleted)
        SELECT @PolicyTermId, @TenantId, @PolicyId,
               COALESCE((SELECT MAX(TermNumber) FROM Policy.PolicyTerm WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0), 0) + 1,
               @EffectiveDate, @ExpirationDate, N'Active', N'Renewal',
               COALESCE(@NewWrittenPremium, pt.WrittenPremium), COALESCE(@NewWrittenPremium, pt.AnnualizedPremium), pt.Taxes, pt.Fees, pt.Surcharges,
               COALESCE(pt.TotalCost, 0) + COALESCE(@TotalCostChange, @PremiumChange, 0), pt.BillingTypeCode, pt.DataCompletenessCode,
               @Now, @ChangedByUserId, 0
        FROM Policy.PolicyTerm pt
        WHERE pt.TenantId = @TenantId AND pt.PolicyTermId = @PriorPolicyTermId AND pt.IsDeleted = 0;

        INSERT INTO Policy.PolicyLine
            (PolicyLineId, TenantId, PolicyId, PolicyTermId, LineOfBusinessId, LineOfBusinessCode, LineOfBusinessName, PolicyLineStatusCode,
             WrittenPremium, CoverageSummary, LimitsSummary, DeductibleSummary, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
        SELECT NEWID(), TenantId, PolicyId, @PolicyTermId, LineOfBusinessId, LineOfBusinessCode, LineOfBusinessName, N'Active',
               WrittenPremium, CoverageSummary, LimitsSummary, DeductibleSummary, SortOrder, @Now, @ChangedByUserId, 0
        FROM Policy.PolicyLine
        WHERE TenantId = @TenantId AND PolicyTermId = @PriorPolicyTermId AND IsDeleted = 0;
    END;

    UPDATE Policy.PolicyTerm
    SET TermStatusCode = N'Renewed', ModifiedDateUtc = @Now, ModifiedByUserId = @ChangedByUserId
    WHERE TenantId = @TenantId AND PolicyTermId = @PriorPolicyTermId AND PolicyTermId <> @PolicyTermId AND IsDeleted = 0;

    UPDATE Policy.PolicyTransaction SET PolicyTermId = @PolicyTermId WHERE TenantId = @TenantId AND PolicyTransactionId = @PolicyTransactionId;
END;

SET @NewPolicyStatus = CASE
    WHEN @ToStatusCode NOT IN (N'Issued', N'Completed') THEN @PolicyStatus
    WHEN @TransactionTypeCode = N'Cancellation' THEN N'Cancelled'
    WHEN @TransactionTypeCode = N'NonRenewal' THEN N'NonRenewed'
    WHEN @TransactionTypeCode = N'Rewrite' THEN N'Rewritten'
    WHEN @TransactionTypeCode IN (N'Reinstatement', N'Renewal') THEN N'Active'
    ELSE @PolicyStatus
END;

SET @NewTermStatus = CASE
    WHEN @ToStatusCode NOT IN (N'Issued', N'Completed') THEN NULL
    WHEN @TransactionTypeCode = N'Cancellation' THEN N'Cancelled'
    WHEN @TransactionTypeCode = N'NonRenewal' THEN N'NonRenewed'
    WHEN @TransactionTypeCode = N'Rewrite' THEN N'Rewritten'
    WHEN @TransactionTypeCode IN (N'Reinstatement', N'Renewal') THEN N'Active'
    ELSE NULL
END;

UPDATE Policy.PolicyTransaction
SET TransactionStatusCode = @ToStatusCode,
    ApprovedDateUtc = CASE WHEN @ToStatusCode IN (N'Approved', N'Issued', N'Completed') THEN COALESCE(ApprovedDateUtc, @Now) ELSE ApprovedDateUtc END,
    ApprovedByUserId = CASE WHEN @ToStatusCode IN (N'Approved', N'Issued', N'Completed') THEN COALESCE(ApprovedByUserId, @ChangedByUserId) ELSE ApprovedByUserId END,
    IssuedDateUtc = CASE WHEN @ToStatusCode IN (N'Issued', N'Completed') THEN COALESCE(IssuedDateUtc, @Now) ELSE IssuedDateUtc END,
    IssuedByUserId = CASE WHEN @ToStatusCode IN (N'Issued', N'Completed') THEN COALESCE(IssuedByUserId, @ChangedByUserId) ELSE IssuedByUserId END,
    ProcessedDateUtc = CASE WHEN @ToStatusCode IN (N'Completed', N'Declined', N'Withdrawn', N'Superseded') THEN COALESCE(ProcessedDateUtc, @Now) ELSE ProcessedDateUtc END,
    ReasonCode = COALESCE(@ReasonCode, ReasonCode),
    Notes = COALESCE(@Notes, Notes),
    CurrentVersionNumber = CurrentVersionNumber + 1,
    ModifiedDateUtc = @Now,
    ModifiedByUserId = @ChangedByUserId
WHERE TenantId = @TenantId AND PolicyTransactionId = @PolicyTransactionId;

IF @ToStatusCode IN (N'Issued', N'Completed')
BEGIN
    UPDATE Submissions.BoundPolicy
    SET Status = @NewPolicyStatus,
        CoverageStatus = @NewPolicyStatus,
        AnnualPremium = COALESCE(@NewWrittenPremium, AnnualPremium),
        EffectiveDate = CASE WHEN @TransactionTypeCode = N'Renewal' THEN @EffectiveDate ELSE EffectiveDate END,
        ExpirationDate = CASE WHEN @TransactionTypeCode = N'Renewal' THEN @ExpirationDate ELSE ExpirationDate END,
        ModifiedDateUtc = @Now,
        ModifiedByUserId = @ChangedByUserId
    WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0;

    UPDATE Policy.PolicyTerm
    SET TermStatusCode = COALESCE(@NewTermStatus, TermStatusCode),
        WrittenPremium = COALESCE(@NewWrittenPremium, WrittenPremium),
        AnnualizedPremium = COALESCE(@NewWrittenPremium, AnnualizedPremium),
        TotalCost = COALESCE(TotalCost, 0) + COALESCE(@TotalCostChange, 0),
        ModifiedDateUtc = @Now,
        ModifiedByUserId = @ChangedByUserId
    WHERE TenantId = @TenantId AND PolicyTermId = @PolicyTermId AND IsDeleted = 0;

    UPDATE pl
    SET PolicyLineStatusCode = CASE
            WHEN tx.TransactionTypeCode = N'Cancellation' THEN N'Cancelled'
            WHEN tx.TransactionTypeCode = N'NonRenewal' THEN N'NonRenewed'
            WHEN tx.TransactionTypeCode = N'Rewrite' THEN N'Rewritten'
            WHEN tx.TransactionTypeCode = N'Reinstatement' THEN N'Active'
            ELSE pl.PolicyLineStatusCode
        END,
        WrittenPremium = COALESCE(ch.NewPremium, pl.WrittenPremium),
        ModifiedDateUtc = @Now,
        ModifiedByUserId = @ChangedByUserId
    FROM Policy.PolicyLine pl
    INNER JOIN Policy.PolicyTransaction tx ON tx.TenantId = pl.TenantId AND tx.PolicyTransactionId = @PolicyTransactionId
    LEFT JOIN Policy.PolicyTransactionLineChange ch ON ch.TenantId = pl.TenantId AND ch.PolicyTransactionId = tx.PolicyTransactionId AND ch.PolicyLineId = pl.PolicyLineId AND ch.IsDeleted = 0
    WHERE pl.TenantId = @TenantId AND pl.PolicyTermId = @PolicyTermId AND pl.IsDeleted = 0;

    INSERT INTO Policy.PolicyLine
        (PolicyLineId, TenantId, PolicyId, PolicyTermId, LineOfBusinessId, LineOfBusinessCode, LineOfBusinessName, PolicyLineStatusCode,
         WrittenPremium, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT COALESCE(ch.PolicyLineId, NEWID()), ch.TenantId, ch.PolicyId, @PolicyTermId, ch.LineOfBusinessId, ch.LineOfBusinessCode, ch.LineOfBusinessName, N'Active',
           ch.NewPremium, COALESCE((SELECT MAX(SortOrder) FROM Policy.PolicyLine WHERE TenantId = @TenantId AND PolicyTermId = @PolicyTermId AND IsDeleted = 0), 0) + ROW_NUMBER() OVER (ORDER BY ch.CreatedDateUtc),
           @Now, @ChangedByUserId, 0
    FROM Policy.PolicyTransactionLineChange ch
    WHERE ch.TenantId = @TenantId AND ch.PolicyTransactionId = @PolicyTransactionId AND ch.ChangeTypeCode = N'AddLine' AND ch.IsDeleted = 0
      AND NOT EXISTS (SELECT 1 FROM Policy.PolicyLine pl WHERE pl.TenantId = ch.TenantId AND pl.PolicyTermId = @PolicyTermId AND pl.LineOfBusinessCode = ch.LineOfBusinessCode AND pl.IsDeleted = 0);
END;

INSERT INTO Policy.PolicyStatusHistory
    (PolicyStatusHistoryId, TenantId, PolicyId, PolicyTermId, PolicyTransactionId, StatusScopeCode, OldStatusCode, NewStatusCode, ReasonCode, Notes, ChangedDateUtc, ChangedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyId, @PolicyTermId, @PolicyTransactionId, N'Transaction', @FromStatusCode, @ToStatusCode, @ReasonCode, @Notes, @Now, @ChangedByUserId, @Now, @ChangedByUserId, 0);

IF @NewPolicyStatus <> @PolicyStatus
    INSERT INTO Policy.PolicyStatusHistory
        (PolicyStatusHistoryId, TenantId, PolicyId, PolicyTermId, PolicyTransactionId, StatusScopeCode, OldStatusCode, NewStatusCode, ReasonCode, Notes, ChangedDateUtc, ChangedByUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @PolicyId, @PolicyTermId, @PolicyTransactionId, N'Policy', @PolicyStatus, @NewPolicyStatus, @ReasonCode, @Notes, @Now, @ChangedByUserId, @Now, @ChangedByUserId, 0);

INSERT INTO Policy.PolicyTermHistory
    (PolicyTermHistoryId, TenantId, PolicyId, PolicyTermId, PolicyTransactionId, TermNumber, TermStatusCode, EffectiveDate, ExpirationDate,
     WrittenPremium, AnnualizedPremium, TotalCost, SnapshotJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, pt.PolicyTermId, @PolicyTransactionId, pt.TermNumber, pt.TermStatusCode, pt.EffectiveDate, pt.ExpirationDate,
       pt.WrittenPremium, pt.AnnualizedPremium, pt.TotalCost,
       JSON_OBJECT(N'PolicyId': @PolicyId, N'PolicyTransactionId': @PolicyTransactionId, N'TransactionTypeCode': @TransactionTypeCode, N'TransactionStatusCode': @ToStatusCode, N'PolicyStatus': @NewPolicyStatus),
       @Now, @ChangedByUserId, 0
FROM Policy.PolicyTerm pt
WHERE pt.TenantId = @TenantId AND pt.PolicyTermId = @PolicyTermId AND pt.IsDeleted = 0;

INSERT INTO Policy.PolicyVersion
    (PolicyVersionId, TenantId, PolicyId, PolicyTermId, PolicyTransactionId, VersionNumber, VersionReasonCode, SnapshotJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @PolicyTermId, @PolicyTransactionId,
       COALESCE((SELECT MAX(VersionNumber) FROM Policy.PolicyVersion WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0), 0) + 1,
       CONCAT(@TransactionTypeCode, N':', @ToStatusCode),
       JSON_OBJECT(N'PolicyId': @PolicyId, N'PolicyTransactionId': @PolicyTransactionId, N'TransactionTypeCode': @TransactionTypeCode, N'TransactionStatusCode': @ToStatusCode, N'PolicyStatus': @NewPolicyStatus, N'PremiumChange': @PremiumChange, N'NewWrittenPremium': @NewWrittenPremium),
       @Now, @ChangedByUserId, 0;

INSERT INTO Policy.PolicyAuditEvent
    (PolicyAuditEventId, TenantId, EntityType, EntityId, PolicyId, PolicyTermId, PolicyTransactionId, ActionCode, SourceCode, ReasonCode, UserId,
     BeforeJson, AfterJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (NEWID(), @TenantId, N'PolicyTransaction', @PolicyTransactionId, @PolicyId, @PolicyTermId, @PolicyTransactionId, N'StatusTransition', N'PolicyLifecycle', @ReasonCode, @ChangedByUserId,
     JSON_OBJECT(N'TransactionStatusCode': @FromStatusCode, N'PolicyStatus': @PolicyStatus),
     JSON_OBJECT(N'TransactionStatusCode': @ToStatusCode, N'PolicyStatus': @NewPolicyStatus), @Now, @ChangedByUserId, 0);";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                PolicyTransactionId = policyTransactionId,
                request.TenantId,
                ToStatusCode = request.ToStatusCode.Trim(),
                ReasonCode = request.ReasonCode?.Trim(),
                Notes = request.Notes?.Trim(),
                request.ChangedByUserId
            }, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
