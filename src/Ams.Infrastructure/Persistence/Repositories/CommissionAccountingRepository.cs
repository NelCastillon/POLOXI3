using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Commissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class CommissionAccountingRepository(ISqlConnectionFactory connectionFactory) : ICommissionAccountingRepository
{
    public async Task<CommissionAccountingWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT CommissionAccountingOptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,IsDefault,IsActive,SortOrder FROM Commission.CommissionAccountingOption WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1 ORDER BY OptionGroupCode,SortOrder,DisplayName;
SELECT CommissionExpectedReceivableId,TenantId,SourceLedgerId,PolicyId,AccountId,CarrierId,PolicyNumber,AccountName,CarrierName,LineOfBusinessCode,BusinessTypeCode,BillingTypeCode,TransactionTypeCode,EffectiveDate,StatementPeriodStart,StatementPeriodEnd,PremiumAmount,ExpectedRatePct,ExpectedCommissionAmount,ReceivedCommissionAmount,ReconciledCommissionAmount,CurrencyCode,StatusCode,DueDate FROM Commission.CommissionExpectedReceivable WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY StatementPeriodEnd DESC,PolicyNumber;
SELECT s.CarrierCommissionStatementId,s.TenantId,s.CarrierId,s.StatementNumber,s.StatementDate,s.PeriodStartDate,s.PeriodEndDate,s.BillingTypeCode,s.CurrencyCode,s.GrossPremiumAmount,s.CommissionAmount,s.ChargebackAmount,s.NetReceivedAmount,s.SourceFileName,s.ImportStatusCode,s.ReconciliationStatusCode,s.ImportedDateUtc,COUNT(l.CarrierCommissionStatementLineId) LineCount,SUM(CASE WHEN l.MatchStatusCode IN(N'Matched',N'Reconciled') THEN 1 ELSE 0 END) MatchedLineCount,(SELECT COUNT(1) FROM Commission.CommissionReconciliationException e WHERE e.TenantId=s.TenantId AND e.CarrierCommissionStatementId=s.CarrierCommissionStatementId AND e.IsDeleted=0 AND e.StatusCode=N'Open') ExceptionCount FROM Commission.CarrierCommissionStatement s LEFT JOIN Commission.CarrierCommissionStatementLine l ON l.CarrierCommissionStatementId=s.CarrierCommissionStatementId AND l.IsDeleted=0 WHERE s.TenantId=@TenantId AND s.IsDeleted=0 GROUP BY s.CarrierCommissionStatementId,s.TenantId,s.CarrierId,s.StatementNumber,s.StatementDate,s.PeriodStartDate,s.PeriodEndDate,s.BillingTypeCode,s.CurrencyCode,s.GrossPremiumAmount,s.CommissionAmount,s.ChargebackAmount,s.NetReceivedAmount,s.SourceFileName,s.ImportStatusCode,s.ReconciliationStatusCode,s.ImportedDateUtc ORDER BY s.StatementDate DESC;
SELECT CommissionReconciliationMatchId,TenantId,CarrierCommissionStatementLineId,CommissionExpectedReceivableId,MatchMethodCode,MatchScore,MatchedAmount,VarianceAmount,StatusCode,MatchedDateUtc,ApprovedDateUtc,Notes FROM Commission.CommissionReconciliationMatch WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY MatchedDateUtc DESC;
SELECT CommissionReconciliationExceptionId,TenantId,CarrierCommissionStatementId,CarrierCommissionStatementLineId,CommissionExpectedReceivableId,ExceptionNumber,ExceptionTypeCode,SeverityCode,StatusCode,ExpectedAmount,ReceivedAmount,VarianceAmount,Description,ResolutionNotes,AssignedToUserId,ResolvedDateUtc,CreatedDateUtc FROM Commission.CommissionReconciliationException WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY CASE SeverityCode WHEN N'Critical' THEN 1 WHEN N'High' THEN 2 ELSE 3 END,CreatedDateUtc DESC;
SELECT p.CommissionPayableId,p.TenantId,p.PayeeId,COALESCE(NULLIF(cp.PayeeName,N''),NULLIF(u.FullName,N''),NULLIF(u.DisplayName,N''),cp.PayeeTypeCode) PayeeName,p.CommissionReconciliationMatchId,p.CommissionTransactionId,p.ClawbackId,p.PayoutBatchId,p.PayableNumber,p.PayableTypeCode,p.AccountingDate,p.GrossPayableAmount,p.AdjustmentAmount,p.NetPayableAmount,p.CurrencyCode,p.StatusCode,p.ApprovedDateUtc,p.PaidDateUtc FROM Commission.CommissionPayable p LEFT JOIN Commission.CommissionPayee cp ON cp.PayeeId=p.PayeeId LEFT JOIN IAM.[User] u ON u.UserId=cp.UserId WHERE p.TenantId=@TenantId AND p.IsDeleted=0 ORDER BY p.AccountingDate DESC,p.PayableNumber;
SELECT COALESCE(SUM(ExpectedCommissionAmount),0) TotalExpected,COALESCE(SUM(ReceivedCommissionAmount),0) TotalReceived,COALESCE(SUM(ReconciledCommissionAmount),0) TotalReconciled,COALESCE(SUM(ExpectedCommissionAmount-ReconciledCommissionAmount),0) OpenVariance,(SELECT COUNT(1) FROM Commission.CarrierCommissionStatementLine WHERE TenantId=@TenantId AND IsDeleted=0 AND MatchStatusCode=N'Unmatched') UnmatchedLineCount,(SELECT COUNT(1) FROM Commission.CommissionReconciliationException WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode=N'Open') OpenExceptionCount,(SELECT COALESCE(SUM(NetPayableAmount),0) FROM Commission.CommissionPayable WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode=N'Approved') ApprovedPayables,(SELECT COALESCE(SUM(NetPayableAmount),0) FROM Commission.CommissionPayable WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode=N'PendingApproval') PendingPayables FROM Commission.CommissionExpectedReceivable WHERE TenantId=@TenantId AND IsDeleted=0;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var options = (await multi.ReadAsync<CommissionAccountingOptionDto>()).AsList();
        var expected = (await multi.ReadAsync<CommissionExpectedReceivableDto>()).AsList();
        var statements = (await multi.ReadAsync<CarrierCommissionStatementDto>()).AsList();
        var matches = (await multi.ReadAsync<CommissionReconciliationMatchDto>()).AsList();
        var exceptions = (await multi.ReadAsync<CommissionReconciliationExceptionDto>()).AsList();
        var payables = (await multi.ReadAsync<CommissionPayableDto>()).AsList();
        var summary = await multi.ReadSingleAsync<CommissionReconciliationSummaryDto>();
        return new(options, expected, statements, matches, exceptions, payables, summary);
    }

    public async Task<IReadOnlyList<CarrierCommissionStatementLineDto>> GetStatementLinesAsync(Guid tenantId, Guid statementId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT CarrierCommissionStatementLineId,TenantId,CarrierCommissionStatementId,LineNumber,ExternalTransactionId,PolicyNumber,InsuredName,ProducerCode,LineOfBusinessCode,TransactionTypeCode,BillingTypeCode,TransactionDate,EffectiveDate,PremiumAmount,CommissionRatePct,CommissionAmount,ChargebackAmount,NetAmount,CurrencyCode,MatchStatusCode,ValidationErrorsJson FROM Commission.CarrierCommissionStatementLine WHERE TenantId=@TenantId AND CarrierCommissionStatementId=@StatementId AND IsDeleted=0 ORDER BY LineNumber;";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<CarrierCommissionStatementLineDto>(new CommandDefinition(sql, new { TenantId = tenantId, StatementId = statementId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<CommissionImportResultDto> ImportStatementAsync(ImportCarrierCommissionStatementRequest request, CancellationToken cancellationToken = default)
    {
        var rows = ParseCsv(request.CsvContent, request.CurrencyCode, request.BillingTypeCode);
        if (rows.Count == 0) throw new InvalidOperationException("The carrier statement contains no data rows.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.CsvContent)));
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        try
        {
            var duplicate = await cn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT CarrierCommissionStatementId FROM Commission.CarrierCommissionStatement WHERE TenantId=@TenantId AND SourceFileHash=@Hash AND IsDeleted=0;", new { request.TenantId, Hash = hash }, tx, cancellationToken: cancellationToken));
            if (duplicate.HasValue) throw new InvalidOperationException("This carrier statement file has already been imported.");
            var statementId = Guid.NewGuid();
            var invalidCount = rows.Count(x => x.ValidationErrorsJson is not null);
            const string insertStatement = "INSERT Commission.CarrierCommissionStatement(CarrierCommissionStatementId,TenantId,CarrierId,ImportProfileId,StatementNumber,StatementDate,PeriodStartDate,PeriodEndDate,BillingTypeCode,CurrencyCode,GrossPremiumAmount,CommissionAmount,ChargebackAmount,NetReceivedAmount,SourceFileName,SourceFileHash,ImportStatusCode,ReconciliationStatusCode,ImportedDateUtc,ImportedByUserId,CreatedDateUtc,IsDeleted) VALUES(@Id,@TenantId,@CarrierId,@ImportProfileId,@StatementNumber,@StatementDate,@PeriodStartDate,@PeriodEndDate,@BillingTypeCode,@CurrencyCode,@Premium,@Commission,@Chargeback,@Net,@SourceFileName,@Hash,@ImportStatus,N'Unreconciled',SYSUTCDATETIME(),@ImportedByUserId,SYSUTCDATETIME(),0);";
            await cn.ExecuteAsync(new CommandDefinition(insertStatement, new { Id = statementId, request.TenantId, request.CarrierId, request.ImportProfileId, request.StatementNumber, request.StatementDate, request.PeriodStartDate, request.PeriodEndDate, request.BillingTypeCode, request.CurrencyCode, Premium = rows.Sum(x => x.PremiumAmount), Commission = rows.Sum(x => x.CommissionAmount), Chargeback = rows.Sum(x => x.ChargebackAmount), Net = rows.Sum(x => x.NetAmount), request.SourceFileName, Hash = hash, ImportStatus = CommissionAccountingRules.DetermineImportStatus(invalidCount), request.ImportedByUserId }, tx, cancellationToken: cancellationToken));
            const string insertLine = "INSERT Commission.CarrierCommissionStatementLine(CarrierCommissionStatementLineId,TenantId,CarrierCommissionStatementId,LineNumber,ExternalTransactionId,PolicyNumber,InsuredName,ProducerCode,LineOfBusinessCode,TransactionTypeCode,BillingTypeCode,TransactionDate,EffectiveDate,PremiumAmount,CommissionRatePct,CommissionAmount,ChargebackAmount,NetAmount,CurrencyCode,MatchStatusCode,RawDataJson,ValidationErrorsJson,CreatedDateUtc,IsDeleted) VALUES(NEWID(),@TenantId,@StatementId,@LineNumber,@ExternalTransactionId,@PolicyNumber,@InsuredName,@ProducerCode,@LineOfBusinessCode,@TransactionTypeCode,@BillingTypeCode,@TransactionDate,@EffectiveDate,@PremiumAmount,@CommissionRatePct,@CommissionAmount,@ChargebackAmount,@NetAmount,@CurrencyCode,N'Unmatched',@RawDataJson,@ValidationErrorsJson,SYSUTCDATETIME(),0);";
            foreach (var row in rows) await cn.ExecuteAsync(new CommandDefinition(insertLine, new { request.TenantId, StatementId = statementId, row.LineNumber, row.ExternalTransactionId, row.PolicyNumber, row.InsuredName, row.ProducerCode, row.LineOfBusinessCode, row.TransactionTypeCode, row.BillingTypeCode, row.TransactionDate, row.EffectiveDate, row.PremiumAmount, row.CommissionRatePct, row.CommissionAmount, row.ChargebackAmount, row.NetAmount, row.CurrencyCode, row.RawDataJson, row.ValidationErrorsJson }, tx, cancellationToken: cancellationToken));
            await AuditAsync(cn, tx, request.TenantId, "CarrierStatement", statementId, "Imported", $"Imported {rows.Count} carrier commission statement lines.", request.ImportedByUserId, cancellationToken);
            tx.Commit();
            return new(statementId, rows.Count, 0, invalidCount, CommissionAccountingRules.DetermineImportStatus(invalidCount));
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<CommissionMatchRunResultDto> RunMatchingAsync(RunCommissionMatchingRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
DECLARE @Candidates TABLE(LineId UNIQUEIDENTIFIER,ExpectedId UNIQUEIDENTIFIER,Received DECIMAL(18,2),Expected DECIMAL(18,2),Method NVARCHAR(50),Score DECIMAL(9,4),rn INT);
INSERT @Candidates SELECT l.CarrierCommissionStatementLineId,e.CommissionExpectedReceivableId,l.NetAmount,e.ExpectedCommissionAmount,CASE WHEN ABS(l.NetAmount-e.ExpectedCommissionAmount)=0 THEN N'ExactPolicyAmount' ELSE N'PolicyTolerance' END,CASE WHEN ABS(l.NetAmount-e.ExpectedCommissionAmount)=0 THEN 100 ELSE 90 END,ROW_NUMBER() OVER(PARTITION BY l.CarrierCommissionStatementLineId ORDER BY ABS(l.NetAmount-e.ExpectedCommissionAmount),e.StatementPeriodEnd)
FROM Commission.CarrierCommissionStatementLine l JOIN Commission.CommissionExpectedReceivable e ON e.TenantId=l.TenantId AND UPPER(REPLACE(REPLACE(e.PolicyNumber,N'-',N''),N' ',N''))=UPPER(REPLACE(REPLACE(l.PolicyNumber,N'-',N''),N' ',N'')) AND e.IsDeleted=0 AND e.StatusCode IN(N'Expected',N'PartiallyReconciled')
WHERE l.TenantId=@TenantId AND l.CarrierCommissionStatementId=@StatementId AND l.IsDeleted=0 AND l.MatchStatusCode=N'Unmatched' AND l.ValidationErrorsJson IS NULL AND ABS(l.NetAmount-e.ExpectedCommissionAmount)<=@AmountTolerance AND (l.TransactionDate IS NULL OR e.EffectiveDate IS NULL OR ABS(DATEDIFF(day,l.TransactionDate,e.EffectiveDate))<=@DateToleranceDays);
INSERT Commission.CommissionReconciliationMatch(CommissionReconciliationMatchId,TenantId,CarrierCommissionStatementLineId,CommissionExpectedReceivableId,MatchMethodCode,MatchScore,MatchedAmount,VarianceAmount,StatusCode,MatchedDateUtc,MatchedByUserId,CreatedDateUtc,IsDeleted)
SELECT NEWID(),@TenantId,LineId,ExpectedId,Method,Score,Received,Received-Expected,N'Proposed',SYSUTCDATETIME(),@UserId,SYSUTCDATETIME(),0 FROM @Candidates c WHERE rn=1 AND NOT EXISTS(SELECT 1 FROM Commission.CommissionReconciliationMatch m WHERE m.TenantId=@TenantId AND m.CarrierCommissionStatementLineId=c.LineId AND m.IsDeleted=0);
UPDATE l SET MatchStatusCode=N'Matched' FROM Commission.CarrierCommissionStatementLine l JOIN @Candidates c ON c.LineId=l.CarrierCommissionStatementLineId AND c.rn=1;
INSERT Commission.CommissionReconciliationException(CommissionReconciliationExceptionId,TenantId,CarrierCommissionStatementId,CarrierCommissionStatementLineId,CommissionExpectedReceivableId,ExceptionNumber,ExceptionTypeCode,SeverityCode,StatusCode,ExpectedAmount,ReceivedAmount,VarianceAmount,Description,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@StatementId,l.CarrierCommissionStatementLineId,NULL,CONCAT(N'CRE-',FORMAT(SYSUTCDATETIME(),N'yyyyMMdd'),N'-',RIGHT(CONCAT(N'000000',ROW_NUMBER() OVER(ORDER BY l.LineNumber)),6)),N'UnmatchedStatementLine',N'High',N'Open',NULL,l.NetAmount,NULL,CONCAT(N'No expected receivable matched policy ',COALESCE(l.PolicyNumber,N'(missing)'),N'.'),SYSUTCDATETIME(),@UserId,0 FROM Commission.CarrierCommissionStatementLine l WHERE l.TenantId=@TenantId AND l.CarrierCommissionStatementId=@StatementId AND l.IsDeleted=0 AND l.MatchStatusCode=N'Unmatched' AND NOT EXISTS(SELECT 1 FROM Commission.CommissionReconciliationException x WHERE x.TenantId=@TenantId AND x.CarrierCommissionStatementLineId=l.CarrierCommissionStatementLineId AND x.StatusCode=N'Open' AND x.IsDeleted=0);
UPDATE s SET ReconciliationStatusCode=CASE WHEN EXISTS(SELECT 1 FROM Commission.CarrierCommissionStatementLine l WHERE l.CarrierCommissionStatementId=@StatementId AND l.IsDeleted=0 AND l.MatchStatusCode=N'Unmatched') THEN N'InProgress' ELSE N'Matched' END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId FROM Commission.CarrierCommissionStatement s WHERE s.TenantId=@TenantId AND s.CarrierCommissionStatementId=@StatementId;
SELECT SUM(CASE WHEN Method=N'ExactPolicyAmount' AND rn=1 THEN 1 ELSE 0 END) ExactMatches,SUM(CASE WHEN Method=N'PolicyTolerance' AND rn=1 THEN 1 ELSE 0 END) ToleranceMatches,(SELECT COUNT(1) FROM Commission.CarrierCommissionStatementLine WHERE TenantId=@TenantId AND CarrierCommissionStatementId=@StatementId AND IsDeleted=0 AND MatchStatusCode=N'Unmatched') UnmatchedLines,(SELECT COUNT(1) FROM Commission.CommissionReconciliationException WHERE TenantId=@TenantId AND CarrierCommissionStatementId=@StatementId AND IsDeleted=0 AND StatusCode=N'Open') ExceptionsCreated FROM @Candidates;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QuerySingleAsync<MatchCounts>(new CommandDefinition(sql, new { request.TenantId, StatementId = request.CarrierCommissionStatementId, request.AmountTolerance, request.DateToleranceDays, request.UserId }, cancellationToken: cancellationToken));
        return new(request.CarrierCommissionStatementId, result.ExactMatches, result.ToleranceMatches, result.UnmatchedLines, result.ExceptionsCreated);
    }

    public async Task ApproveMatchAsync(ApproveCommissionMatchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE m SET StatusCode=N'Approved',ApprovedDateUtc=SYSUTCDATETIME(),ApprovedByUserId=@UserId,Notes=@Notes,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId FROM Commission.CommissionReconciliationMatch m WHERE m.TenantId=@TenantId AND m.CommissionReconciliationMatchId=@MatchId AND m.IsDeleted=0 AND m.StatusCode=N'Proposed';
IF @@ROWCOUNT<>1 THROW 51000,N'Match was not found or is not pending approval.',1;
UPDATE e SET ReceivedCommissionAmount=e.ReceivedCommissionAmount+m.MatchedAmount,ReconciledCommissionAmount=e.ReconciledCommissionAmount+m.MatchedAmount,StatusCode=CASE WHEN e.ReconciledCommissionAmount+m.MatchedAmount>=e.ExpectedCommissionAmount THEN N'Reconciled' ELSE N'PartiallyReconciled' END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId FROM Commission.CommissionExpectedReceivable e JOIN Commission.CommissionReconciliationMatch m ON m.CommissionExpectedReceivableId=e.CommissionExpectedReceivableId WHERE m.CommissionReconciliationMatchId=@MatchId;
UPDATE l SET MatchStatusCode=N'Reconciled' FROM Commission.CarrierCommissionStatementLine l JOIN Commission.CommissionReconciliationMatch m ON m.CarrierCommissionStatementLineId=l.CarrierCommissionStatementLineId WHERE m.CommissionReconciliationMatchId=@MatchId;
INSERT Commission.CommissionReconciliationException(CommissionReconciliationExceptionId,TenantId,CarrierCommissionStatementId,CarrierCommissionStatementLineId,CommissionExpectedReceivableId,ExceptionNumber,ExceptionTypeCode,SeverityCode,StatusCode,ExpectedAmount,ReceivedAmount,VarianceAmount,Description,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),m.TenantId,l.CarrierCommissionStatementId,m.CarrierCommissionStatementLineId,m.CommissionExpectedReceivableId,CONCAT(N'CRE-',FORMAT(SYSUTCDATETIME(),N'yyyyMMddHHmmss'),N'-',LEFT(CONVERT(NVARCHAR(36),m.CommissionReconciliationMatchId),8)),N'AmountVariance',N'Medium',N'Open',e.ExpectedCommissionAmount,m.MatchedAmount,m.VarianceAmount,N'Received commission differs from the expected receivable.',SYSUTCDATETIME(),@UserId,0
FROM Commission.CommissionReconciliationMatch m JOIN Commission.CommissionExpectedReceivable e ON e.CommissionExpectedReceivableId=m.CommissionExpectedReceivableId JOIN Commission.CarrierCommissionStatementLine l ON l.CarrierCommissionStatementLineId=m.CarrierCommissionStatementLineId
WHERE m.CommissionReconciliationMatchId=@MatchId AND m.VarianceAmount<>0 AND NOT EXISTS(SELECT 1 FROM Commission.CommissionReconciliationException x WHERE x.TenantId=m.TenantId AND x.CarrierCommissionStatementLineId=m.CarrierCommissionStatementLineId AND x.ExceptionTypeCode=N'AmountVariance' AND x.IsDeleted=0);
UPDATE s SET ReconciliationStatusCode=CASE WHEN EXISTS(SELECT 1 FROM Commission.CarrierCommissionStatementLine x WHERE x.CarrierCommissionStatementId=s.CarrierCommissionStatementId AND x.IsDeleted=0 AND x.MatchStatusCode<>N'Reconciled') THEN N'InProgress' WHEN EXISTS(SELECT 1 FROM Commission.CommissionReconciliationException x WHERE x.CarrierCommissionStatementId=s.CarrierCommissionStatementId AND x.IsDeleted=0 AND x.StatusCode=N'Open') THEN N'Exception' ELSE N'Reconciled' END,ApprovedDateUtc=CASE WHEN NOT EXISTS(SELECT 1 FROM Commission.CarrierCommissionStatementLine x WHERE x.CarrierCommissionStatementId=s.CarrierCommissionStatementId AND x.IsDeleted=0 AND x.MatchStatusCode<>N'Reconciled') THEN SYSUTCDATETIME() ELSE s.ApprovedDateUtc END,ApprovedByUserId=CASE WHEN NOT EXISTS(SELECT 1 FROM Commission.CarrierCommissionStatementLine x WHERE x.CarrierCommissionStatementId=s.CarrierCommissionStatementId AND x.IsDeleted=0 AND x.MatchStatusCode<>N'Reconciled') THEN @UserId ELSE s.ApprovedByUserId END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId FROM Commission.CarrierCommissionStatement s JOIN Commission.CarrierCommissionStatementLine l ON l.CarrierCommissionStatementId=s.CarrierCommissionStatementId JOIN Commission.CommissionReconciliationMatch m ON m.CarrierCommissionStatementLineId=l.CarrierCommissionStatementLineId WHERE m.CommissionReconciliationMatchId=@MatchId;
INSERT Commission.CommissionAccountingAuditEvent(CommissionAccountingAuditEventId,TenantId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) SELECT NEWID(),TenantId,N'ReconciliationMatch',CommissionReconciliationMatchId,N'Approved',N'Reconciliation match approved and expected receivable updated.',@UserId,SYSUTCDATETIME() FROM Commission.CommissionReconciliationMatch WHERE CommissionReconciliationMatchId=@MatchId;
COMMIT;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.TenantId, MatchId = request.CommissionReconciliationMatchId, UserId = request.ApprovedByUserId, request.Notes }, cancellationToken: cancellationToken));
    }

    public async Task ResolveExceptionAsync(ResolveCommissionReconciliationExceptionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE Commission.CommissionReconciliationException SET StatusCode=N'Resolved',ResolutionNotes=@ResolutionNotes,ResolvedDateUtc=SYSUTCDATETIME(),ResolvedByUserId=@UserId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND CommissionReconciliationExceptionId=@Id AND IsDeleted=0 AND StatusCode=N'Open';
IF @@ROWCOUNT<>1 THROW 51000,N'The reconciliation exception was not found or is already resolved.',1;
INSERT Commission.CommissionAccountingAuditEvent(CommissionAccountingAuditEventId,TenantId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,N'ReconciliationException',@Id,N'Resolved',N'Reconciliation exception resolved.',JSON_OBJECT(N'ResolutionNotes':@ResolutionNotes),@UserId,SYSUTCDATETIME());
COMMIT;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await cn.ExecuteAsync(new CommandDefinition(sql, new { request.TenantId, Id = request.CommissionReconciliationExceptionId, request.ResolutionNotes, UserId = request.ResolvedByUserId }, cancellationToken: cancellationToken));
        _ = count;
    }

    public async Task<IReadOnlyList<Guid>> CreatePayablesAsync(CreateCommissionPayableBatchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
DECLARE @Created TABLE(Id UNIQUEIDENTIFIER);
INSERT Commission.CommissionPayable(CommissionPayableId,TenantId,PayeeId,CommissionReconciliationMatchId,PayableNumber,PayableTypeCode,AccountingDate,GrossPayableAmount,AdjustmentAmount,NetPayableAmount,CurrencyCode,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
OUTPUT inserted.CommissionPayableId INTO @Created
SELECT NEWID(),m.TenantId,a.PayeeId,m.CommissionReconciliationMatchId,CONCAT(N'CP-',FORMAT(@AccountingDate,N'yyyyMM'),N'-',LEFT(CONVERT(NVARCHAR(36),m.CommissionReconciliationMatchId),8),N'-',LEFT(CONVERT(NVARCHAR(36),a.PayeeId),8)),
       CASE WHEN m.MatchedAmount<0 THEN N'Chargeback' ELSE a.AllocationTypeCode END,@AccountingDate,
       ROUND(m.MatchedAmount*a.SplitPercentage/100.0,2),0,ROUND(m.MatchedAmount*a.SplitPercentage/100.0,2),@CurrencyCode,N'PendingApproval',SYSUTCDATETIME(),@UserId,0
FROM Commission.CommissionReconciliationMatch m
JOIN Commission.CommissionExpectedReceivable e ON e.CommissionExpectedReceivableId=m.CommissionExpectedReceivableId
JOIN Commission.CommissionReceivablePayeeAllocation a ON a.TenantId=m.TenantId AND a.CommissionExpectedReceivableId=e.CommissionExpectedReceivableId AND a.IsDeleted=0 AND a.StatusCode=N'Active' AND a.EffectiveDate<=@AccountingDate AND (a.EffectiveEndDate IS NULL OR a.EffectiveEndDate>=@AccountingDate)
WHERE m.TenantId=@TenantId AND m.IsDeleted=0 AND m.StatusCode=N'Approved' AND e.StatementPeriodEnd<=@AccountingDate AND (@PayeeId IS NULL OR a.PayeeId=@PayeeId) AND NOT EXISTS(SELECT 1 FROM Commission.CommissionPayable x WHERE x.TenantId=m.TenantId AND x.CommissionReconciliationMatchId=m.CommissionReconciliationMatchId AND x.PayeeId=a.PayeeId AND x.IsDeleted=0);
SELECT Id FROM @Created;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<Guid>(new CommandDefinition(sql, new { request.TenantId, AccountingDate = request.AccountingThroughDate, request.PayeeId, request.CurrencyCode, UserId = request.CreatedByUserId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task ApprovePayableAsync(ApproveCommissionPayableRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
UPDATE Commission.CommissionPayable SET StatusCode=N'Approved',ApprovedDateUtc=SYSUTCDATETIME(),ApprovedByUserId=@UserId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND CommissionPayableId=@Id AND IsDeleted=0 AND StatusCode=N'PendingApproval';
IF @@ROWCOUNT<>1 THROW 51000,N'The payable was not found or is not pending approval.',1;
INSERT Commission.CommissionAccountingAuditEvent(CommissionAccountingAuditEventId,TenantId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,N'CommissionPayable',@Id,N'Approved',N'Commission payable approved for payout processing.',@UserId,SYSUTCDATETIME());
COMMIT;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await cn.ExecuteAsync(new CommandDefinition(sql, new { request.TenantId, Id = request.CommissionPayableId, UserId = request.ApprovedByUserId }, cancellationToken: cancellationToken));
        _ = count;
    }

    public async Task<int> SynchronizeExpectedReceivablesAsync(SynchronizeCommissionExpectedReceivablesRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE e SET PolicyId=bp.PolicyId,AccountId=bp.AccountId,CarrierId=bp.CarrierId,PolicyNumber=l.PolicyNumber,AccountName=NULLIF(l.AccountName,N''),CarrierName=NULLIF(l.Carrier,N''),LineOfBusinessCode=NULLIF(l.LineOfBusiness,N''),BusinessTypeCode=COALESCE(NULLIF(l.BusinessType,N''),N'Policy'),BillingTypeCode=COALESCE(NULLIF(REPLACE(bs.BillingModeCode,N' ',N''),N''),N'Unspecified'),TransactionTypeCode=CASE WHEN l.BusinessType LIKE N'%Renew%' THEN N'Renewal' ELSE N'NewBusiness' END,EffectiveDate=l.TransactionDate,StatementPeriodStart=DATEFROMPARTS(YEAR(l.TransactionDate),MONTH(l.TransactionDate),1),StatementPeriodEnd=EOMONTH(l.TransactionDate),PremiumAmount=l.GrossAmount,ExpectedRatePct=l.CommissionPct,ExpectedCommissionAmount=l.AgencyAmount,DueDate=DATEADD(day,30,EOMONTH(l.TransactionDate)),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId
FROM Commission.CommissionExpectedReceivable e JOIN Commission.CommissionLedger l ON l.TenantId=e.TenantId AND l.CommissionId=e.SourceLedgerId LEFT JOIN Submissions.BoundPolicy bp ON bp.TenantId=l.TenantId AND bp.PolicyNumber=l.PolicyNumber AND bp.IsDeleted=0 LEFT JOIN Billing.AccountSettings bs ON bs.TenantId=bp.TenantId AND bs.AccountId=bp.AccountId AND bs.IsDeleted=0
WHERE e.TenantId=@TenantId AND e.IsDeleted=0 AND e.StatusCode IN(N'Expected',N'PartiallyReconciled') AND l.IsDeleted=0 AND (@FromDate IS NULL OR l.TransactionDate>=@FromDate) AND (@ThroughDate IS NULL OR l.TransactionDate<=@ThroughDate);
DECLARE @Updated INT=@@ROWCOUNT;
INSERT Commission.CommissionExpectedReceivable(CommissionExpectedReceivableId,TenantId,SourceLedgerId,PolicyId,AccountId,CarrierId,PolicyNumber,AccountName,CarrierName,LineOfBusinessCode,BusinessTypeCode,BillingTypeCode,TransactionTypeCode,EffectiveDate,StatementPeriodStart,StatementPeriodEnd,PremiumAmount,ExpectedRatePct,ExpectedCommissionAmount,CurrencyCode,StatusCode,DueDate,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),l.TenantId,l.CommissionId,bp.PolicyId,bp.AccountId,bp.CarrierId,l.PolicyNumber,NULLIF(l.AccountName,N''),NULLIF(l.Carrier,N''),NULLIF(l.LineOfBusiness,N''),COALESCE(NULLIF(l.BusinessType,N''),N'Policy'),COALESCE(NULLIF(REPLACE(bs.BillingModeCode,N' ',N''),N''),N'Unspecified'),CASE WHEN l.BusinessType LIKE N'%Renew%' THEN N'Renewal' ELSE N'NewBusiness' END,l.TransactionDate,DATEFROMPARTS(YEAR(l.TransactionDate),MONTH(l.TransactionDate),1),EOMONTH(l.TransactionDate),l.GrossAmount,l.CommissionPct,l.AgencyAmount,N'USD',N'Expected',DATEADD(day,30,EOMONTH(l.TransactionDate)),SYSUTCDATETIME(),@UserId,0
FROM Commission.CommissionLedger l LEFT JOIN Submissions.BoundPolicy bp ON bp.TenantId=l.TenantId AND bp.PolicyNumber=l.PolicyNumber AND bp.IsDeleted=0 LEFT JOIN Billing.AccountSettings bs ON bs.TenantId=bp.TenantId AND bs.AccountId=bp.AccountId AND bs.IsDeleted=0
WHERE l.TenantId=@TenantId AND l.IsDeleted=0 AND (@FromDate IS NULL OR l.TransactionDate>=@FromDate) AND (@ThroughDate IS NULL OR l.TransactionDate<=@ThroughDate) AND NOT EXISTS(SELECT 1 FROM Commission.CommissionExpectedReceivable e WHERE e.TenantId=l.TenantId AND e.SourceLedgerId=l.CommissionId AND e.IsDeleted=0);
SELECT @Updated+@@ROWCOUNT;
""";
        using var cn = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { request.TenantId, FromDate = request.EffectiveFromDate, ThroughDate = request.EffectiveThroughDate, request.UserId }, cancellationToken: cancellationToken));
    }

    private static List<ImportRow> ParseCsv(string content, string defaultCurrency, string? defaultBillingType)
    {
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return [];
        var headers = SplitCsvLine(lines[0]).Select((x, i) => (Name: NormalizeHeader(x), Index: i)).ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
        string Cell(string[] cells, params string[] names) => names.Select(n => headers.TryGetValue(NormalizeHeader(n), out var i) && i < cells.Length ? cells[i].Trim() : string.Empty).FirstOrDefault(x => x.Length > 0) ?? string.Empty;
        var result = new List<ImportRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            var cells = SplitCsvLine(lines[i]);
            var errors = new List<string>();
            var policy = Cell(cells, "PolicyNumber", "Policy", "PolicyNo"); if (policy.Length == 0) errors.Add("Policy number is required.");
            var commission = ParseDecimal(Cell(cells, "CommissionAmount", "Commission", "Amount"), errors, "Commission amount");
            var chargeback = ParseDecimal(Cell(cells, "ChargebackAmount", "Chargeback"), null, null);
            var premium = ParseDecimal(Cell(cells, "PremiumAmount", "Premium"), null, null);
            var row = new ImportRow(i, Cell(cells, "ExternalTransactionId", "TransactionId"), policy, Cell(cells, "InsuredName", "Insured", "AccountName"), Cell(cells, "ProducerCode", "Producer"), Cell(cells, "LineOfBusinessCode", "LineOfBusiness", "LOB"), Cell(cells, "TransactionTypeCode", "TransactionType") is { Length: > 0 } type ? type : (chargeback != 0 ? "Chargeback" : "Commission"), Cell(cells, "BillingTypeCode", "BillingType") is { Length: > 0 } billing ? billing.Replace(" ", string.Empty, StringComparison.Ordinal) : defaultBillingType?.Replace(" ", string.Empty, StringComparison.Ordinal), ParseDate(Cell(cells, "TransactionDate", "Date")), ParseDate(Cell(cells, "EffectiveDate")), premium, ParseNullableDecimal(Cell(cells, "CommissionRatePct", "CommissionRate", "Rate")), commission, chargeback, commission - Math.Abs(chargeback), Cell(cells, "CurrencyCode", "Currency") is { Length: > 0 } currency ? currency : defaultCurrency, JsonSerializer.Serialize(headers.ToDictionary(x => x.Key, x => x.Value < cells.Length ? cells[x.Value] : string.Empty)), errors.Count == 0 ? null : JsonSerializer.Serialize(errors));
            result.Add(row);
        }
        return result;
    }

    private static string[] SplitCsvLine(string line)
    {
        var cells = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++) { var c = line[i]; if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; } else if (c == '"') quoted = !quoted; else if (c == ',' && !quoted) { cells.Add(value.ToString()); value.Clear(); } else value.Append(c); }
        cells.Add(value.ToString()); return [.. cells];
    }

    private static string NormalizeHeader(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());
    private static decimal ParseDecimal(string value, List<string>? errors, string? label) { if (string.IsNullOrWhiteSpace(value)) return 0; if (decimal.TryParse(value, NumberStyles.Currency, CultureInfo.InvariantCulture, out var result)) return result; errors?.Add($"{label} is invalid."); return 0; }
    private static decimal? ParseNullableDecimal(string value) => string.IsNullOrWhiteSpace(value) ? null : ParseDecimal(value, null, null);
    private static DateOnly? ParseDate(string value) => DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date) ? date : null;

    private static Task AuditAsync(System.Data.IDbConnection cn, System.Data.IDbTransaction tx, Guid tenantId, string entityType, Guid entityId, string eventType, string description, Guid? userId, CancellationToken cancellationToken)
        => cn.ExecuteAsync(new CommandDefinition("INSERT Commission.CommissionAccountingAuditEvent(CommissionAccountingAuditEventId,TenantId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@EntityType,@EntityId,@EventType,@Description,@UserId,SYSUTCDATETIME());", new { TenantId = tenantId, EntityType = entityType, EntityId = entityId, EventType = eventType, Description = description, UserId = userId }, tx, cancellationToken: cancellationToken));

    private sealed record ImportRow(int LineNumber, string? ExternalTransactionId, string? PolicyNumber, string? InsuredName, string? ProducerCode, string? LineOfBusinessCode, string TransactionTypeCode, string? BillingTypeCode, DateOnly? TransactionDate, DateOnly? EffectiveDate, decimal PremiumAmount, decimal? CommissionRatePct, decimal CommissionAmount, decimal ChargebackAmount, decimal NetAmount, string CurrencyCode, string RawDataJson, string? ValidationErrorsJson);
    private sealed record MatchCounts(int ExactMatches, int ToleranceMatches, int UnmatchedLines, int ExceptionsCreated);
}
