using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Submissions;
using Dapper;
using System.Data;
using System.Text.Json;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyCreationRepository : IPolicyCreationRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public PolicyCreationRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreatePolicyFromConfirmedBindAsync(PolicyCreationFromConfirmedBindRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @PolicyId UNIQUEIDENTIFIER;
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @OpportunityId UNIQUEIDENTIFIER;
DECLARE @OpportunityStageId UNIQUEIDENTIFIER;
DECLARE @OpportunitySubmissionId UNIQUEIDENTIFIER;
DECLARE @SynchronizedPolicyNumber NVARCHAR(80);
DECLARE @SynchronizedPolicyStatus NVARCHAR(50);
DECLARE @SynchronizedBoundDateUtc DATETIME2;
DECLARE @OpportunityStageChanged BIT = 0;

SELECT @PolicyId = PolicyId
FROM Submissions.PolicyBindTransaction WITH (UPDLOCK, HOLDLOCK)
WHERE PolicyBindTransactionId = @PolicyBindTransactionId
  AND TenantId = @TenantId
  AND IsDeleted = 0;

IF @PolicyId IS NOT NULL
BEGIN
    GOTO SynchronizeOpportunity;
END;

DECLARE @GenerationAuthorized BIT;
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @QuoteId UNIQUEIDENTIFIER;
DECLARE @AccountId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER;
DECLARE @PolicySourceCode NVARCHAR(50);
DECLARE @PolicySourceReason NVARCHAR(500);
DECLARE @PolicySourceNotes NVARCHAR(1000);
DECLARE @PolicyNumber NVARCHAR(80);
DECLARE @AnnualPremium DECIMAL(18,2);
DECLARE @EffectiveDate DATE;
DECLARE @ExpirationDate DATE;
DECLARE @BoundDateUtc DATETIME2;
DECLARE @ConfirmationSourceCode NVARCHAR(50);
DECLARE @CarrierReferenceNumber NVARCHAR(120);
DECLARE @BinderNumber NVARCHAR(120);
DECLARE @ConfirmationCertified BIT;
DECLARE @IssueStatus NVARCHAR(50);
DECLARE @CoverageStatus NVARCHAR(50);
DECLARE @IssuedDateUtc DATETIME2;
DECLARE @PolicyTermId UNIQUEIDENTIFIER;
DECLARE @BinderReviewId UNIQUEIDENTIFIER;
DECLARE @CoverageSnapshotJson NVARCHAR(MAX);
DECLARE @RiskSnapshotJson NVARCHAR(MAX);
DECLARE @ComparisonSnapshotJson NVARCHAR(MAX);
DECLARE @Fees DECIMAL(18,2);
DECLARE @Taxes DECIMAL(18,2);
DECLARE @BillingTypeCode NVARCHAR(50);
DECLARE @PaymentPlan NVARCHAR(200);
DECLARE @LineOfBusiness NVARCHAR(160);
DECLARE @NamedInsured NVARCHAR(240);
DECLARE @AssignedToUserId UNIQUEIDENTIFIER;
DECLARE @CsrId UNIQUEIDENTIFIER;
DECLARE @Deductible DECIMAL(18,2);
DECLARE @Limit DECIMAL(18,2);
DECLARE @CoverageNotes NVARCHAR(1000);
DECLARE @CommissionPlanId UNIQUEIDENTIFIER;
DECLARE @CommissionPlanVersionId UNIQUEIDENTIFIER;
DECLARE @CommissionPayeeId UNIQUEIDENTIFIER;
DECLARE @CommissionSplitRuleId UNIQUEIDENTIFIER;
DECLARE @CommissionBusinessTypeCode NVARCHAR(50);
DECLARE @CommissionRatePct DECIMAL(9,4);
DECLARE @CommissionSplitPct DECIMAL(9,4);
DECLARE @CommissionablePremium DECIMAL(18,2);
DECLARE @EstimatedGrossCommission DECIMAL(18,2);
DECLARE @EstimatedProducerCommission DECIMAL(18,2);
DECLARE @RenewalRetentionCaseId UNIQUEIDENTIFIER;
DECLARE @SourcePolicyId UNIQUEIDENTIFIER;
DECLARE @SourcePolicyTermId UNIQUEIDENTIFIER;
DECLARE @SourceCarrierId UNIQUEIDENTIFIER;
DECLARE @IsIncumbentRenewal BIT = 0;
DECLARE @TermNumber INT = 1;

SELECT @GenerationAuthorized = CASE WHEN EXISTS
       (
           SELECT 1 FROM Submissions.PolicyGenerationRequest pgr
           INNER JOIN Submissions.BinderReview br ON br.BinderReviewId = pgr.BinderReviewId AND br.TenantId = pgr.TenantId AND br.PolicyBindTransactionId = pgr.PolicyBindTransactionId
           WHERE pgr.TenantId = pbt.TenantId AND pgr.PolicyBindTransactionId = pbt.PolicyBindTransactionId AND pgr.StatusCode = N'Processing' AND pgr.IsDeleted = 0
             AND br.StatusCode = N'GenerationQueued' AND br.IsDeleted = 0
       ) THEN 1 ELSE 0 END,
       @SubmissionId = pbt.SubmissionId,
       @QuoteId = pbt.QuoteId,
       @AccountId = pbt.AccountId,
       @CarrierId = pbt.CarrierId,
       @PolicySourceCode = pbt.PolicySourceCode,
       @PolicySourceReason = pbt.BindReason,
       @PolicySourceNotes = pbt.Notes,
       @PolicyNumber = NULLIF(pbt.PolicyNumber, N''),
       @AnnualPremium = COALESCE(pbt.FinalPremium, pbt.AnnualPremium),
       @EffectiveDate = pbt.EffectiveDate,
       @ExpirationDate = pbt.ExpirationDate,
       @BoundDateUtc = COALESCE(pbt.BoundDateUtc, @Now),
       @ConfirmationSourceCode = pbt.ConfirmationSourceCode,
       @CarrierReferenceNumber = pbt.CarrierReferenceNumber,
       @BinderNumber = pbt.BinderNumber,
       @ConfirmationCertified = pbt.ConfirmationCertified,
       @LineOfBusiness = NULLIF(s.LineOfBusiness, N''),
       @NamedInsured = NULLIF(a.AccountName, N''),
       @AssignedToUserId = s.AssignedToUserId,
       @Deductible = q.Deductible,
       @Limit = q.[Limit],
       @CoverageNotes = q.CoverageNotes
       ,@CommissionPlanId = pbt.CommissionPlanId
       ,@CommissionPlanVersionId = pbt.CommissionPlanVersionId
       ,@CommissionPayeeId = pbt.CommissionPayeeId
       ,@CommissionSplitRuleId = pbt.CommissionSplitRuleId
       ,@CommissionBusinessTypeCode = pbt.CommissionBusinessTypeCode
       ,@CommissionRatePct = pbt.CommissionRatePct
       ,@CommissionSplitPct = pbt.CommissionSplitPct
       ,@CommissionablePremium = pbt.CommissionablePremium
       ,@EstimatedGrossCommission = pbt.EstimatedGrossCommission
       ,@EstimatedProducerCommission = pbt.EstimatedProducerCommission
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = pbt.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsActive = 1 AND pbs.IsDeleted = 0
LEFT JOIN Submissions.Submission s ON s.SubmissionId = pbt.SubmissionId AND s.TenantId = pbt.TenantId AND s.IsDeleted = 0
LEFT JOIN Submissions.Quote q ON q.QuoteId = pbt.QuoteId AND q.SubmissionId = pbt.SubmissionId AND q.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = pbt.AccountId AND a.TenantId = pbt.TenantId AND a.IsDeleted = 0
WHERE pbt.PolicyBindTransactionId = @PolicyBindTransactionId
  AND pbt.TenantId = @TenantId
  AND pbt.IsDeleted = 0;

SELECT @BinderReviewId = br.BinderReviewId,
       @PolicyNumber = COALESCE(NULLIF(br.PolicyNumber, N''), @PolicyNumber),
       @CarrierId = br.CarrierId,
       @LineOfBusiness = br.LineOfBusiness,
       @EffectiveDate = br.EffectiveDate,
       @ExpirationDate = br.ExpirationDate,
       @AnnualPremium = br.Premium,
       @Fees = br.Fees,
       @Taxes = br.Taxes,
       @CommissionRatePct = COALESCE(br.CommissionPercent, @CommissionRatePct),
       @PaymentPlan = br.PaymentPlan,
       @BillingTypeCode = br.BillingTypeCode,
       @AssignedToUserId = COALESCE(br.ProducerId, @AssignedToUserId),
       @CsrId = br.CsrId,
       @CoverageSnapshotJson = br.CoverageSnapshotJson,
       @RiskSnapshotJson = br.RiskSnapshotJson,
       @ComparisonSnapshotJson = br.ComparisonSnapshotJson
FROM Submissions.BinderReview br
WHERE br.PolicyBindTransactionId = @PolicyBindTransactionId
  AND br.TenantId = @TenantId
  AND br.StatusCode = N'GenerationQueued'
  AND br.IsDeleted = 0;

IF @GenerationAuthorized IS NULL THROW 52100, 'Confirmed bind request was not found for policy creation.', 1;
IF @GenerationAuthorized = 0 OR @BinderReviewId IS NULL THROW 52101, 'An accepted binder and active policy generation request are required.', 1;
IF @ConfirmationCertified = 0 THROW 52102, 'Carrier confirmation must be certified before policy creation.', 1;
IF NULLIF(@ConfirmationSourceCode, N'') IS NULL THROW 52103, 'Carrier confirmation source is required before policy creation.', 1;
IF NULLIF(@CarrierReferenceNumber, N'') IS NULL AND NULLIF(@BinderNumber, N'') IS NULL AND NULLIF(@PolicyNumber, N'') IS NULL THROW 52104, 'Carrier confirmation requires a carrier reference, binder number, or policy number.', 1;
IF @AnnualPremium <= 0 THROW 52105, 'Policy creation requires a final premium greater than zero.', 1;
IF @ExpirationDate <= @EffectiveDate THROW 52106, 'Policy expiration date must be after the effective date.', 1;

IF @CommissionPlanId IS NOT NULL AND @CommissionPayeeId IS NOT NULL AND @CommissionRatePct IS NOT NULL AND @CommissionSplitPct IS NOT NULL
BEGIN
    DECLARE @CsrPayeeId UNIQUEIDENTIFIER,@CsrSplitRuleId UNIQUEIDENTIFIER,@CsrSplitPct DECIMAL(9,4)=0,@AgencySplitPct DECIMAL(9,4),@SnapshotGrossCommission DECIMAL(18,2);
    SET @SnapshotGrossCommission=COALESCE(@EstimatedGrossCommission,ROUND(COALESCE(@CommissionablePremium,@AnnualPremium)*@CommissionRatePct/100.0,2));

    IF @CsrId IS NOT NULL
    BEGIN
        SELECT TOP 1 @CsrPayeeId=p.PayeeId
        FROM Commission.CommissionPayee p
        WHERE p.TenantId=@TenantId AND p.CommissionPlanId=@CommissionPlanId AND p.UserId=@CsrId
          AND p.PayeeTypeCode IN(N'CSR',N'Service') AND p.StatusCode=N'Active' AND p.EffectiveDate<=@EffectiveDate AND p.IsDeleted=0
        ORDER BY p.EffectiveDate DESC,p.CreatedDateUtc DESC;

        IF @CsrPayeeId IS NOT NULL
        BEGIN
            SELECT TOP 1 @CsrSplitRuleId=sr.SplitRuleId,@CsrSplitPct=sr.SplitPct
            FROM Commission.CommissionSplitRule sr
            WHERE sr.TenantId=@TenantId AND sr.CommissionPlanId=@CommissionPlanId AND sr.SplitTypeCode IN(N'CSR',N'Service')
              AND (sr.PayeeId=@CsrPayeeId OR sr.PayeeId IS NULL) AND sr.StatusCode=N'Active'
              AND sr.EffectiveStartDate<=@EffectiveDate AND (sr.EffectiveEndDate IS NULL OR sr.EffectiveEndDate>=@EffectiveDate) AND sr.IsDeleted=0
            ORDER BY CASE WHEN sr.PayeeId=@CsrPayeeId THEN 0 ELSE 1 END,sr.Priority,sr.EffectiveStartDate DESC;
        END;
    END;

    IF @CommissionSplitPct+COALESCE(@CsrSplitPct,0)>100 THROW 52107,'Configured producer and CSR commission allocations exceed 100 percent.',1;
    SET @AgencySplitPct=100-@CommissionSplitPct-COALESCE(@CsrSplitPct,0);

    INSERT Submissions.PolicyBindCommissionAllocationSnapshot(PolicyBindCommissionAllocationSnapshotId,TenantId,PolicyBindTransactionId,CommissionPlanId,CommissionPlanVersionId,CommissionSplitRuleId,PayeeId,PayeeUserId,PayeeTypeCode,SplitPercent,CommissionRatePct,CommissionablePremium,GrossCommissionAmount,AllocationAmount,SnapshotDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT NEWID(),@TenantId,@PolicyBindTransactionId,@CommissionPlanId,@CommissionPlanVersionId,@CommissionSplitRuleId,@CommissionPayeeId,@AssignedToUserId,N'Producer',@CommissionSplitPct,@CommissionRatePct,COALESCE(@CommissionablePremium,@AnnualPremium),@SnapshotGrossCommission,ROUND(@SnapshotGrossCommission*@CommissionSplitPct/100.0,2),@Now,@Now,@RequestedByUserId,0
    WHERE @CommissionSplitPct>0 AND NOT EXISTS(SELECT 1 FROM Submissions.PolicyBindCommissionAllocationSnapshot WHERE TenantId=@TenantId AND PolicyBindTransactionId=@PolicyBindTransactionId AND PayeeTypeCode=N'Producer' AND IsDeleted=0);

    INSERT Submissions.PolicyBindCommissionAllocationSnapshot(PolicyBindCommissionAllocationSnapshotId,TenantId,PolicyBindTransactionId,CommissionPlanId,CommissionPlanVersionId,CommissionSplitRuleId,PayeeId,PayeeUserId,PayeeTypeCode,SplitPercent,CommissionRatePct,CommissionablePremium,GrossCommissionAmount,AllocationAmount,SnapshotDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT NEWID(),@TenantId,@PolicyBindTransactionId,@CommissionPlanId,@CommissionPlanVersionId,@CsrSplitRuleId,@CsrPayeeId,@CsrId,N'CSR',@CsrSplitPct,@CommissionRatePct,COALESCE(@CommissionablePremium,@AnnualPremium),@SnapshotGrossCommission,ROUND(@SnapshotGrossCommission*@CsrSplitPct/100.0,2),@Now,@Now,@RequestedByUserId,0
    WHERE @CsrPayeeId IS NOT NULL AND @CsrSplitPct>0 AND NOT EXISTS(SELECT 1 FROM Submissions.PolicyBindCommissionAllocationSnapshot WHERE TenantId=@TenantId AND PolicyBindTransactionId=@PolicyBindTransactionId AND PayeeTypeCode=N'CSR' AND IsDeleted=0);

    INSERT Submissions.PolicyBindCommissionAllocationSnapshot(PolicyBindCommissionAllocationSnapshotId,TenantId,PolicyBindTransactionId,CommissionPlanId,CommissionPlanVersionId,CommissionSplitRuleId,PayeeId,PayeeUserId,PayeeTypeCode,SplitPercent,CommissionRatePct,CommissionablePremium,GrossCommissionAmount,AllocationAmount,SnapshotDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT NEWID(),@TenantId,@PolicyBindTransactionId,@CommissionPlanId,@CommissionPlanVersionId,NULL,NULL,NULL,N'Agency',@AgencySplitPct,@CommissionRatePct,COALESCE(@CommissionablePremium,@AnnualPremium),@SnapshotGrossCommission,@SnapshotGrossCommission-ROUND(@SnapshotGrossCommission*@CommissionSplitPct/100.0,2)-ROUND(@SnapshotGrossCommission*COALESCE(@CsrSplitPct,0)/100.0,2),@Now,@Now,@RequestedByUserId,0
    WHERE @AgencySplitPct>0 AND NOT EXISTS(SELECT 1 FROM Submissions.PolicyBindCommissionAllocationSnapshot WHERE TenantId=@TenantId AND PolicyBindTransactionId=@PolicyBindTransactionId AND PayeeTypeCode=N'Agency' AND IsDeleted=0);

    IF ABS((SELECT COALESCE(SUM(SplitPercent),0) FROM Submissions.PolicyBindCommissionAllocationSnapshot WHERE TenantId=@TenantId AND PolicyBindTransactionId=@PolicyBindTransactionId AND IsDeleted=0)-100)>0.0001
        THROW 52108,'Bind-time commission allocation snapshot must total 100 percent.',1;
END;

SET @IssueStatus = CASE WHEN NULLIF(@PolicyNumber, N'') IS NULL THEN N'PendingIssue' ELSE N'Issued' END;
SET @CoverageStatus = CASE WHEN @IssueStatus = N'Issued' THEN N'Active' ELSE N'Bound' END;
SET @IssuedDateUtc = CASE WHEN @IssueStatus = N'Issued' THEN @Now ELSE NULL END;
SET @PolicyTermId = NEWID();
SET @LineOfBusiness = COALESCE(@LineOfBusiness, N'Package');
SET @NamedInsured = COALESCE(@NamedInsured, N'Named Insured');

SELECT @RenewalRetentionCaseId = rc.RetentionCaseId,
       @SourcePolicyId = rc.PolicyId,
       @SourcePolicyTermId = rc.SourcePolicyTermId,
       @SourceCarrierId = sourcePolicy.CarrierId
FROM Renewal.RetentionCase rc WITH (UPDLOCK, HOLDLOCK)
LEFT JOIN Submissions.BoundPolicy sourcePolicy ON sourcePolicy.PolicyId = rc.PolicyId AND sourcePolicy.TenantId = rc.TenantId AND sourcePolicy.IsDeleted = 0
WHERE rc.TenantId = @TenantId
  AND rc.RenewalSubmissionId = @SubmissionId
  AND rc.IsDeleted = 0;

IF @RenewalRetentionCaseId IS NOT NULL AND @SourcePolicyId IS NOT NULL AND @SourceCarrierId = @CarrierId
BEGIN
    SET @IsIncumbentRenewal = 1;
    SET @PolicyId = @SourcePolicyId;
    SELECT @TermNumber = ISNULL(MAX(TermNumber), 0) + 1 FROM Policy.PolicyTerm WITH (UPDLOCK, HOLDLOCK) WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0;
END
ELSE
BEGIN
    SET @PolicyId = NEWID();
END;

INSERT INTO Submissions.BoundPolicy
    (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId,
     PolicyNumber, Status, IssueStatus, CoverageStatus, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IssuedDateUtc, PolicySourceCode, PolicySourceReason, PolicySourceNotes, PolicyBindTransactionId, RenewalRetentionCaseId, PriorPolicyId, IsDeleted)
SELECT
    (@PolicyId, @SubmissionId, @QuoteId, @TenantId, @AccountId, @CarrierId,
     COALESCE(@PolicyNumber, @BinderNumber, @CarrierReferenceNumber, 'POL-' + FORMAT(GETUTCDATE(), 'yyyyMMdd') + '-' + RIGHT('00000' + CAST(NEXT VALUE FOR Submissions.PolicySeq AS VARCHAR), 5)),
     @IssueStatus, @IssueStatus, @CoverageStatus, @AnnualPremium, @EffectiveDate, @ExpirationDate, @BoundDateUtc, @IssuedDateUtc, @PolicySourceCode, @PolicySourceReason, @PolicySourceNotes, @PolicyBindTransactionId, @RenewalRetentionCaseId, CASE WHEN @RenewalRetentionCaseId IS NOT NULL THEN @SourcePolicyId ELSE NULL END, 0)
WHERE @IsIncumbentRenewal = 0;

IF @IsIncumbentRenewal = 1
BEGIN
    UPDATE Submissions.BoundPolicy
    SET Status = @IssueStatus,
        IssueStatus = @IssueStatus,
        CoverageStatus = @CoverageStatus,
        AnnualPremium = @AnnualPremium,
        EffectiveDate = @EffectiveDate,
        ExpirationDate = @ExpirationDate,
        BoundDateUtc = @BoundDateUtc,
        IssuedDateUtc = @IssuedDateUtc,
        RenewalRetentionCaseId = @RenewalRetentionCaseId
    WHERE PolicyId = @PolicyId AND TenantId = @TenantId AND IsDeleted = 0;
END;

INSERT INTO Policy.PolicyTerm
    (PolicyTermId, TenantId, PolicyId, TermNumber, EffectiveDate, ExpirationDate, TermStatusCode, TransactionTypeCode, WrittenPremium, AnnualizedPremium, Taxes, Fees, Surcharges, TotalCost, BillingTypeCode, DataCompletenessCode, RenewalRetentionCaseId, PriorPolicyTermId, CreatedDateUtc, IsDeleted)
SELECT @PolicyTermId, @TenantId, @PolicyId, @TermNumber, @EffectiveDate, @ExpirationDate, CASE WHEN @CoverageStatus = N'Bound' THEN N'Active' ELSE @CoverageStatus END, CASE WHEN @RenewalRetentionCaseId IS NULL THEN N'NewBusiness' ELSE N'Renewal' END, @AnnualPremium, @AnnualPremium, @Taxes, @Fees, NULL, @AnnualPremium + COALESCE(@Taxes,0) + COALESCE(@Fees,0), @BillingTypeCode, N'Verified', @RenewalRetentionCaseId, @SourcePolicyTermId, @Now, 0
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyTerm WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND TermNumber = @TermNumber AND IsDeleted = 0);

INSERT INTO Policy.PolicyLine
    (PolicyLineId, TenantId, PolicyId, PolicyTermId, LineOfBusinessId, LineOfBusinessCode, LineOfBusinessName, PolicyLineStatusCode, WrittenPremium, CoverageSummary, LimitsSummary, DeductibleSummary, SortOrder, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @PolicyTermId, NULL, @LineOfBusiness, @LineOfBusiness, CASE WHEN @CoverageStatus = N'Bound' THEN N'Active' ELSE @CoverageStatus END, @AnnualPremium, @CoverageNotes, CASE WHEN @Limit IS NULL THEN NULL ELSE CONCAT(N'Limit: ', FORMAT(@Limit, N'N2')) END, CASE WHEN @Deductible IS NULL THEN NULL ELSE CONCAT(N'Deductible: ', FORMAT(@Deductible, N'N2')) END, 1, @Now, 0
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyLine WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND SortOrder = 1 AND IsDeleted = 0);

INSERT INTO Policy.PolicySource
    (PolicySourceId, TenantId, PolicyId, SourceCode, ManualReasonCode, ExternalSystem, ExternalReference, CarrierPortalReference, MigrationBatch, SourceNotes, RecordedByUserId, RecordedAtUtc, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @PolicySourceCode, NULL, N'CarrierBindConfirmation', COALESCE(@PolicyNumber, @BinderNumber, @CarrierReferenceNumber), @CarrierReferenceNumber, NULL, @PolicySourceNotes, @RequestedByUserId, @Now, 0
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicySource WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND SourceCode = @PolicySourceCode AND IsDeleted = 0);

INSERT INTO Policy.PolicyNamedInsured
    (PolicyNamedInsuredId, TenantId, PolicyId, LegalName, DbaName, AddressSnapshotJson, IsPrimary, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @NamedInsured, NULL, N'{}', 1, @Now, 0
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyNamedInsured WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsPrimary = 1 AND IsDeleted = 0);

INSERT INTO Policy.PolicyCoverageSummary
    (PolicyCoverageSummaryId, TenantId, PolicyTermId, CoverageSummary, LimitsSummary, DeductibleSummary, CoverageNotes, RiskSnapshotJson, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyTermId, @CoverageNotes, CASE WHEN @Limit IS NULL THEN NULL ELSE CONCAT(N'Limit: ', FORMAT(@Limit, N'N2')) END, CASE WHEN @Deductible IS NULL THEN NULL ELSE CONCAT(N'Deductible: ', FORMAT(@Deductible, N'N2')) END, @PolicySourceNotes, @RiskSnapshotJson, @Now, 0
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyCoverageSummary WHERE TenantId = @TenantId AND PolicyTermId = @PolicyTermId AND IsDeleted = 0);

INSERT INTO Policy.PolicyVersion (PolicyVersionId, TenantId, PolicyId, PolicyTermId, PolicyTransactionId, VersionNumber, VersionReasonCode, SnapshotJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @PolicyTermId, NULL, 1, N'Original',
       JSON_OBJECT(N'policyNumber': @PolicyNumber, N'carrierId': @CarrierId, N'lineOfBusiness': @LineOfBusiness, N'premium': @AnnualPremium, N'fees': @Fees, N'taxes': @Taxes, N'paymentPlan': @PaymentPlan, N'billingType': @BillingTypeCode, N'coverage': JSON_QUERY(COALESCE(@CoverageSnapshotJson,N'{}')), N'risk': JSON_QUERY(COALESCE(@RiskSnapshotJson,N'{}')), N'comparison': JSON_QUERY(COALESCE(@ComparisonSnapshotJson,N'{}'))),
       @Now, @RequestedByUserId, 0
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyVersion WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND VersionNumber=1 AND IsDeleted=0);

INSERT INTO Policy.PolicyDocumentLink (PolicyDocumentLinkId,TenantId,PolicyId,PolicyTermId,DocumentId,DocumentRoleCode,SourceEntityName,SourceEntityId,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@PolicyId,@PolicyTermId,bd.DocumentId,bd.DocumentRoleCode,N'PolicyBindTransaction',@PolicyBindTransactionId,@Now,@RequestedByUserId,0
FROM Submissions.BindDocument bd
WHERE bd.TenantId=@TenantId AND bd.PolicyBindTransactionId=@PolicyBindTransactionId AND bd.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Policy.PolicyDocumentLink pdl WHERE pdl.TenantId=@TenantId AND pdl.PolicyId=@PolicyId AND pdl.DocumentId=bd.DocumentId AND pdl.DocumentRoleCode=bd.DocumentRoleCode AND pdl.IsDeleted=0);

INSERT INTO Policy.PolicyAssignment
    (PolicyAssignmentId, TenantId, PolicyId, Agency, Branch, Department, ProducerId, AccountManagerId, CsrId, ProducerName, AccountManagerName, CsrName, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, NULL, NULL, NULL, COALESCE(@AssignedToUserId, @RequestedByUserId), NULL, NULL, NULL, NULL, NULL, @Now, 0
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyAssignment WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND IsDeleted = 0);

INSERT INTO Policy.PolicyCommissionEstimate
    (PolicyCommissionEstimateId, TenantId, PolicyId, PolicyTermId, CommissionTypeCode, CommissionStatusCode, CommissionRate, EstimatedCommission, ProducerSplitPercent, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @PolicyTermId, COALESCE(@CommissionBusinessTypeCode, N'Estimated'), N'Estimated', @CommissionRatePct, @EstimatedGrossCommission, @CommissionSplitPct, @Now, 0
WHERE @CommissionPlanId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Policy.PolicyCommissionEstimate WHERE TenantId = @TenantId AND PolicyId = @PolicyId AND PolicyTermId = @PolicyTermId AND IsDeleted = 0);

IF @CommissionPlanId IS NOT NULL
   AND @CommissionPlanVersionId IS NOT NULL
   AND @CommissionPayeeId IS NOT NULL
   AND @CommissionRatePct IS NOT NULL
   AND @CommissionSplitPct IS NOT NULL
   AND @CommissionablePremium > 0
   AND @EstimatedProducerCommission IS NOT NULL
BEGIN
    DECLARE @CommissionTransactionId UNIQUEIDENTIFIER;
    SELECT @CommissionTransactionId = TransactionId
    FROM Commission.CommissionTransaction WITH (UPDLOCK, HOLDLOCK)
    WHERE TenantId = @TenantId AND SourceEntityName = N'Policy' AND SourceEntityId = @PolicyId AND PayeeId = @CommissionPayeeId AND IsDeleted = 0;

    IF @CommissionTransactionId IS NULL
    BEGIN
        SET @CommissionTransactionId = NEWID();
        INSERT INTO Commission.CommissionTransaction
            (TransactionId, TenantId, PayeeId, CommissionPlanId, SourceEntityName, SourceEntityId, TransactionDate, GrossAmount, CommissionRate, CommissionAmount, StatusCode, PayoutId, CreatedDateUtc, IsDeleted)
        VALUES
            (@CommissionTransactionId, @TenantId, @CommissionPayeeId, @CommissionPlanId, N'Policy', @PolicyId, @EffectiveDate, @CommissionablePremium, @CommissionRatePct, @EstimatedProducerCommission, N'Pending', NULL, @Now, 0);
    END;

    INSERT INTO Commission.CommissionCalculationResult
        (CalculationResultId, TenantId, TransactionId, PayeeId, CommissionPlanId, BaseAmount, RatePct, SplitPct, CalculatedAmount, AdjustedAmount, StatusCode, CalculatedDateUtc, CreatedDateUtc, IsDeleted)
    SELECT NEWID(), @TenantId, @CommissionTransactionId, @CommissionPayeeId, @CommissionPlanId, @CommissionablePremium, @CommissionRatePct, @CommissionSplitPct, @EstimatedProducerCommission, NULL, N'Calculated', @Now, @Now, 0
    WHERE NOT EXISTS (SELECT 1 FROM Commission.CommissionCalculationResult WHERE TenantId = @TenantId AND TransactionId = @CommissionTransactionId AND PayeeId = @CommissionPayeeId AND IsDeleted = 0);

    INSERT INTO Commission.CommissionAccrualEntry
        (AccrualEntryId, TenantId, TransactionId, GLAccountId, AccrualDate, AccruedAmount, ReversalDate, ReversedAmount, JournalEntryId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, @CommissionTransactionId, NULL, @EffectiveDate, @EstimatedProducerCommission, NULL, NULL, NULL, N'Pending', @Now, @RequestedByUserId, 0
    WHERE NOT EXISTS (SELECT 1 FROM Commission.CommissionAccrualEntry WHERE TenantId = @TenantId AND TransactionId = @CommissionTransactionId AND IsDeleted = 0);
END;

INSERT INTO Policy.PolicyAuditEvent
    (PolicyAuditEventId, TenantId, EntityType, EntityId, ActionCode, SourceCode, ReasonCode, UserId, BeforeJson, AfterJson, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, N'Policy', @PolicyId, N'PolicyCreatedFromBindConfirmation', N'PolicyService', @PolicySourceCode, @RequestedByUserId, NULL, JSON_OBJECT(N'PolicyId': @PolicyId, N'PolicyBindTransactionId': @PolicyBindTransactionId, N'SubmissionId': @SubmissionId, N'QuoteId': @QuoteId, N'LineOfBusiness': @LineOfBusiness, N'AnnualPremium': @AnnualPremium), @Now, 0
WHERE NOT EXISTS (SELECT 1 FROM Policy.PolicyAuditEvent WHERE TenantId = @TenantId AND EntityType = N'Policy' AND EntityId = @PolicyId AND ActionCode = N'PolicyCreatedFromBindConfirmation' AND IsDeleted = 0);

INSERT INTO Accounting.PolicyCreatedEvent
    (PolicyCreatedEventId, TenantId, PolicyId, PolicyTermId, PolicyBindTransactionId, EventTypeCode, EventVersion, CorrelationId, PayloadJson, StatusCode, AttemptCount, OccurredDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @PolicyTermId, @PolicyBindTransactionId, N'PolicyCreated', 1, @PolicyBindTransactionId,
       JSON_OBJECT(
           N'policyId': @PolicyId,
           N'policyTermId': @PolicyTermId,
           N'policyBindTransactionId': @PolicyBindTransactionId,
           N'policyNumber': COALESCE((SELECT TOP 1 bp.PolicyNumber FROM Submissions.BoundPolicy bp WHERE bp.PolicyId = @PolicyId AND bp.TenantId = @TenantId AND bp.IsDeleted = 0), @PolicyNumber, @BinderNumber, @CarrierReferenceNumber),
           N'accountId': @AccountId,
           N'carrierId': @CarrierId,
           N'billingTypeCode': COALESCE(NULLIF(@BillingTypeCode, N''), N'AgencyBill'),
           N'annualPremium': @AnnualPremium,
           N'fees': COALESCE(@Fees, 0),
           N'taxes': COALESCE(@Taxes, 0),
           N'paymentPlan': @PaymentPlan,
           N'commissionRatePct': COALESCE(@CommissionRatePct, 0),
           N'estimatedGrossCommission': COALESCE(@EstimatedGrossCommission, 0),
           N'effectiveDate': @EffectiveDate,
           N'expirationDate': @ExpirationDate,
           N'issuedDateUtc': @IssuedDateUtc,
           N'boundDateUtc': @BoundDateUtc
       ),
       N'Pending', 0, @Now, @RequestedByUserId, 0
WHERE NOT EXISTS
(
    SELECT 1
    FROM Accounting.PolicyCreatedEvent existing
    WHERE existing.TenantId = @TenantId
      AND existing.PolicyId = @PolicyId
      AND existing.PolicyTermId = @PolicyTermId
      AND existing.EventTypeCode = N'PolicyCreated'
      AND existing.EventVersion = 1
      AND existing.IsDeleted = 0
);

UPDATE pbt
SET PolicyId = @PolicyId,
    PolicyNumber = bp.PolicyNumber,
    BoundDateUtc = COALESCE(pbt.BoundDateUtc, @BoundDateUtc),
    ModifiedDateUtc = @Now,
    ModifiedByUserId = @RequestedByUserId
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN Submissions.BoundPolicy bp ON bp.PolicyId = @PolicyId
WHERE pbt.PolicyBindTransactionId = @PolicyBindTransactionId;

IF @RenewalRetentionCaseId IS NOT NULL
BEGIN
    UPDATE Renewal.RetentionCase
    SET RenewalPolicyBindTransactionId = @PolicyBindTransactionId,
        ResultPolicyId = @PolicyId,
        ResultPolicyTermId = @PolicyTermId,
        Stage = N'Saved',
        OutreachStatus = N'Accepted',
        IsSaved = 1,
        CompletedDateUtc = @Now,
        ModifiedDateUtc = @Now,
        ModifiedByUserId = @RequestedByUserId
    WHERE RetentionCaseId = @RenewalRetentionCaseId AND TenantId = @TenantId AND IsDeleted = 0;

    INSERT INTO Renewal.RetentionActivity
        (RetentionActivityId, TenantId, RetentionCaseId, ActivityType, Subject, Outcome, Notes, ActivityDateUtc, CreatedByName, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, @RenewalRetentionCaseId, N'Bind', N'Renewal bound', N'Completed',
           CASE WHEN @IsIncumbentRenewal = 1 THEN N'Carrier confirmation created the next policy term.' ELSE N'Carrier confirmation created a replacement policy.' END,
           @Now, N'Policy Service', @Now, @RequestedByUserId, 0
    WHERE NOT EXISTS (SELECT 1 FROM Renewal.RetentionActivity WHERE RetentionCaseId = @RenewalRetentionCaseId AND Subject = N'Renewal bound' AND IsDeleted = 0);
END;

UPDATE Submissions.Quote
SET Status = CASE WHEN QuoteId = @QuoteId THEN N'Bound' ELSE N'Not Selected' END,
    IsSelected = CASE WHEN QuoteId = @QuoteId THEN 1 ELSE 0 END,
    IsRecommended = CASE WHEN QuoteId = @QuoteId THEN 1 ELSE 0 END,
    ModifiedDateUtc = @Now
WHERE SubmissionId = @SubmissionId
  AND IsDeleted = 0
  AND @SubmissionId IS NOT NULL
  AND @QuoteId IS NOT NULL
  AND @QuoteId <> '00000000-0000-0000-0000-000000000000';

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN CarrierId = @CarrierId THEN N'Bound' ELSE CASE WHEN Status IN (N'Declined', N'Blocked') THEN Status ELSE N'Not Selected' END END,
    RespondedDateUtc = COALESCE(RespondedDateUtc, @Now),
    ModifiedDateUtc = @Now
WHERE SubmissionId = @SubmissionId
  AND IsDeleted = 0
  AND @SubmissionId IS NOT NULL;

UPDATE Submissions.Submission
SET Status = N'Bound',
    ModifiedDateUtc = @Now,
    ModifiedByUserId = @RequestedByUserId
WHERE SubmissionId = @SubmissionId
  AND TenantId = @TenantId
  AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
SELECT NEWID(), @SubmissionId, @TenantId, N'PolicyCreationRequested', CONCAT(N'Policy Service created AMS policy record after carrier confirmation. Issue status: ', @IssueStatus, N'; coverage status: ', @CoverageStatus, N'.'), @Now, N'PolicyBindTransaction', @PolicyBindTransactionId, N'PolicyService', 0
WHERE @SubmissionId IS NOT NULL;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
SELECT NEWID(), @SubmissionId, @TenantId, N'PolicyRecordCreated', CONCAT(N'AMS policy record created. Carrier legal policy issue status: ', @IssueStatus, N'. ', COALESCE(@PolicySourceReason, N''), CASE WHEN NULLIF(@PolicySourceNotes, N'') IS NULL THEN N'' ELSE CONCAT(N' Notes: ', @PolicySourceNotes) END), @Now, N'Policy', @PolicyId, N'PolicyService', 0
WHERE @SubmissionId IS NOT NULL;

INSERT INTO OPS.TaskItem (TaskItemId,TenantId,TaskNumber,Title,Description,TaskTypeCode,StageCode,PriorityCode,StatusCode,RelatedEntityName,RelatedEntityId,AccountId,AssignedToUserId,DueDate,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,CONCAT(N'TASK-',FORMAT(@Now,N'yyyyMMdd'),N'-',RIGHT(REPLACE(CONVERT(NVARCHAR(36),NEWID()),N'-',N''),6)),t.Title,t.Description,t.TaskTypeCode,N'PolicyAdministration',t.PriorityCode,N'Open',N'Policy',@PolicyId,@AccountId,COALESCE(@AssignedToUserId,@RequestedByUserId),DATEADD(day,t.DueDays,CONVERT(date,@Now)),@Now,@RequestedByUserId,0
FROM Submissions.PolicyGenerationTaskTemplate t
WHERE t.TenantId=@TenantId AND t.IsActive=1 AND t.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM OPS.TaskItem existing WHERE existing.TenantId=@TenantId AND existing.RelatedEntityName=N'Policy' AND existing.RelatedEntityId=@PolicyId AND existing.TaskTypeCode=t.TaskTypeCode AND existing.IsDeleted=0);

SynchronizeOpportunity:

SELECT @OpportunityId = s.OpportunityId,
       @SynchronizedPolicyNumber = p.PolicyNumber,
       @SynchronizedPolicyStatus = p.Status,
       @SynchronizedBoundDateUtc = p.BoundDateUtc
FROM Submissions.BoundPolicy p
INNER JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId
                                  AND s.TenantId = p.TenantId
                                  AND s.IsDeleted = 0
WHERE p.PolicyId = @PolicyId
  AND p.TenantId = @TenantId
  AND p.IsDeleted = 0;

IF @OpportunityId IS NOT NULL AND @OpportunityId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    SELECT TOP 1 @OpportunitySubmissionId = source.SubmissionId
    FROM CRM.OpportunitySubmission source
    INNER JOIN Submissions.BoundPolicy p ON p.PolicyId = @PolicyId
    INNER JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId
    WHERE source.TenantId = @TenantId
      AND source.OpportunityId = @OpportunityId
      AND source.IsDeleted = 0
      AND (source.LineOfBusiness = s.LineOfBusiness OR source.SubmissionNumber = s.SubmissionNumber)
    ORDER BY CASE WHEN source.Status = N'Bound' THEN 0 ELSE 1 END,
             source.ModifiedDateUtc DESC,
             source.CreatedDateUtc DESC;

    INSERT INTO CRM.OpportunityBoundPolicy
        (OpportunityBoundPolicyId, TenantId, OpportunityId, OpportunitySubmissionId, SubmissionId, QuoteId, PolicyId, PolicyNumber, BindingStatus, BoundDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), p.TenantId, @OpportunityId, @OpportunitySubmissionId, p.SubmissionId, p.QuoteId, p.PolicyId, p.PolicyNumber, p.Status, p.BoundDateUtc, @Now, @RequestedByUserId, 0
    FROM Submissions.BoundPolicy p
    WHERE p.PolicyId = @PolicyId
      AND p.TenantId = @TenantId
      AND p.IsDeleted = 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM CRM.OpportunityBoundPolicy existing WITH (UPDLOCK, HOLDLOCK)
          WHERE existing.PolicyId = p.PolicyId
            AND existing.IsDeleted = 0
      );

    SELECT TOP 1 @OpportunityStageId = OpportunityStageId
    FROM CRM.OpportunityStage
    WHERE TenantId = @TenantId
      AND IsActive = 1
      AND (StageName = N'Closed Won' OR StageCode = N'CLOSED_WON')
    ORDER BY SortOrder, StageName;

    IF @OpportunityStageId IS NULL
        THROW 52107, 'The active Closed Won opportunity stage is not configured for this tenant.', 1;

    UPDATE CRM.Opportunity
    SET StageName = N'Closed Won',
        OpportunityStageId = @OpportunityStageId,
        ForecastCategoryCode = N'Closed',
        ModifiedDateUtc = @Now,
        ModifiedByUserId = @RequestedByUserId
    WHERE OpportunityId = @OpportunityId
      AND TenantId = @TenantId
      AND IsDeleted = 0
      AND (StageName <> N'Closed Won'
           OR OpportunityStageId <> @OpportunityStageId
           OR OpportunityStageId IS NULL
           OR ForecastCategoryCode <> N'Closed'
           OR ForecastCategoryCode IS NULL);

    IF @@ROWCOUNT > 0 SET @OpportunityStageChanged = 1;

    UPDATE CRM.OpportunityWorkflowEvent
    SET IsDeleted = 1,
        ModifiedDateUtc = @Now,
        ModifiedByUserId = @RequestedByUserId
    WHERE TenantId = @TenantId
      AND OpportunityId = @OpportunityId
      AND EventType = N'PolicyBindingRequired'
      AND IsDeleted = 0;

    IF @OpportunityStageChanged = 1
       AND NOT EXISTS
       (
           SELECT 1
           FROM CRM.OpportunityWorkflowEvent
           WHERE TenantId = @TenantId
             AND OpportunityId = @OpportunityId
             AND EventType = N'Stage'
             AND RelatedEntityName = N'BoundPolicy'
             AND RelatedEntityId = @PolicyId
             AND IsDeleted = 0
       )
    BEGIN
        INSERT INTO CRM.OpportunityWorkflowEvent
            (WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail, RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
            (NEWID(), @TenantId, @OpportunityId, N'Stage', N'Moved to Closed Won', CONCAT(N'Opportunity automatically moved to Closed Won after carrier-confirmed policy ', @SynchronizedPolicyNumber, N' was created.'), N'BoundPolicy', @PolicyId, @Now, @Now, @RequestedByUserId, 0);
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM CRM.OpportunityWorkflowEvent
        WHERE TenantId = @TenantId
          AND OpportunityId = @OpportunityId
          AND EventType = N'PolicyBound'
          AND RelatedEntityName = N'BoundPolicy'
          AND RelatedEntityId = @PolicyId
          AND IsDeleted = 0
    )
    BEGIN
        INSERT INTO CRM.OpportunityWorkflowEvent
            (WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail, RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
            (NEWID(), @TenantId, @OpportunityId, N'PolicyBound', N'Bound policy created', CONCAT(N'Carrier-confirmed policy ', @SynchronizedPolicyNumber, N' was linked to the opportunity and synchronized to Closed Won.'), N'BoundPolicy', @PolicyId, @Now, @Now, @RequestedByUserId, 0);
    END;
END;

COMMIT TRANSACTION;

SELECT @PolicyId;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task<BinderReviewDto?> GetBinderReviewAsync(Guid policyBindTransactionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT br.*, c.CarrierName
FROM Submissions.BinderReview br
INNER JOIN Core.Carrier c ON c.CarrierId = br.CarrierId AND c.TenantId = br.TenantId AND c.IsDeleted = 0
WHERE br.PolicyBindTransactionId = @PolicyBindTransactionId AND br.TenantId = @TenantId AND br.IsDeleted = 0;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<BinderReviewDto>(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<BinderReviewDto> SaveBinderReviewAsync(Guid policyBindTransactionId, UpsertBinderReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF @ReviewedByUserId IS NULL THROW 52204, 'An authenticated reviewer is required.', 1;
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindTransaction WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND BindStatusCode = N'BinderReceived' AND ConfirmationCertified = 1 AND IsDeleted = 0) THROW 52200, 'A certified received carrier binder is required before review.', 1;
IF NOT EXISTS (SELECT 1 FROM Core.Carrier WHERE CarrierId = @CarrierId AND TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0) THROW 52201, 'The selected carrier is not active for this tenant.', 1;
SELECT @CoverageSnapshotJson=JSON_OBJECT(N'quoteId':pbt.QuoteId,N'limit':q.[Limit],N'deductible':q.Deductible,N'coverageNotes':q.CoverageNotes,N'subjectivities':q.Subjectivities,N'exclusions':q.Exclusions),
       @RiskSnapshotJson=JSON_OBJECT(N'accountId':pbt.AccountId,N'submissionId':pbt.SubmissionId,N'quoteId':pbt.QuoteId,N'lineOfBusiness':s.LineOfBusiness),
       @ComparisonSnapshotJson=JSON_OBJECT(N'quotedPremium':pbt.AnnualPremium,N'binderPremium':@Premium,N'premiumVariance':@Premium-pbt.AnnualPremium,N'requestedEffectiveDate':CONVERT(date,pbt.EffectiveDate),N'binderEffectiveDate':@EffectiveDate,N'requestedExpirationDate':CONVERT(date,pbt.ExpirationDate),N'binderExpirationDate':@ExpirationDate,N'carrierId':pbt.CarrierId,N'verifiedCarrierId':@CarrierId)
FROM Submissions.PolicyBindTransaction pbt LEFT JOIN Submissions.Quote q ON q.QuoteId=pbt.QuoteId AND q.SubmissionId=pbt.SubmissionId AND q.IsDeleted=0 LEFT JOIN Submissions.Submission s ON s.SubmissionId=pbt.SubmissionId AND s.TenantId=pbt.TenantId AND s.IsDeleted=0 WHERE pbt.PolicyBindTransactionId=@PolicyBindTransactionId AND pbt.TenantId=@TenantId AND pbt.IsDeleted=0;
DECLARE @BinderReviewId UNIQUEIDENTIFIER = (SELECT BinderReviewId FROM Submissions.BinderReview WITH (UPDLOCK, HOLDLOCK) WHERE PolicyBindTransactionId = @PolicyBindTransactionId AND TenantId = @TenantId AND IsDeleted = 0);
IF @BinderReviewId IS NULL
BEGIN
 SET @BinderReviewId = NEWID();
 INSERT INTO Submissions.BinderReview (BinderReviewId,TenantId,PolicyBindTransactionId,StatusCode,PolicyNumber,CarrierId,LineOfBusiness,EffectiveDate,ExpirationDate,Premium,Fees,Taxes,CommissionPercent,PaymentPlan,BillingTypeCode,ProducerId,CsrId,CoverageSnapshotJson,RiskSnapshotJson,ComparisonSnapshotJson,ReviewNotes,ReviewedDateUtc,ReviewedByUserId,CreatedDateUtc,CreatedByUserId,IsDeleted)
 VALUES (@BinderReviewId,@TenantId,@PolicyBindTransactionId,N'PendingReview',@PolicyNumber,@CarrierId,@LineOfBusiness,@EffectiveDate,@ExpirationDate,@Premium,@Fees,@Taxes,@CommissionPercent,@PaymentPlan,@BillingTypeCode,@ProducerId,@CsrId,@CoverageSnapshotJson,@RiskSnapshotJson,@ComparisonSnapshotJson,@ReviewNotes,SYSUTCDATETIME(),@ReviewedByUserId,SYSUTCDATETIME(),@ReviewedByUserId,0);
END
ELSE UPDATE Submissions.BinderReview SET PolicyNumber=@PolicyNumber,CarrierId=@CarrierId,LineOfBusiness=@LineOfBusiness,EffectiveDate=@EffectiveDate,ExpirationDate=@ExpirationDate,Premium=@Premium,Fees=@Fees,Taxes=@Taxes,CommissionPercent=@CommissionPercent,PaymentPlan=@PaymentPlan,BillingTypeCode=@BillingTypeCode,ProducerId=@ProducerId,CsrId=@CsrId,CoverageSnapshotJson=@CoverageSnapshotJson,RiskSnapshotJson=@RiskSnapshotJson,ComparisonSnapshotJson=@ComparisonSnapshotJson,ReviewNotes=@ReviewNotes,ReviewedDateUtc=SYSUTCDATETIME(),ReviewedByUserId=@ReviewedByUserId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ReviewedByUserId WHERE BinderReviewId=@BinderReviewId AND StatusCode IN(N'PendingReview',N'CorrectionRequested');
IF @BinderReviewId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Submissions.BinderReview WHERE BinderReviewId=@BinderReviewId AND StatusCode IN(N'PendingReview',N'CorrectionRequested')) THROW 52205,'An accepted or generated binder review is immutable.',1;
COMMIT;
SELECT br.*,c.CarrierName FROM Submissions.BinderReview br INNER JOIN Core.Carrier c ON c.CarrierId=br.CarrierId AND c.TenantId=br.TenantId WHERE br.BinderReviewId=@BinderReviewId;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<BinderReviewDto>(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, request.TenantId, request.PolicyNumber, request.CarrierId, request.LineOfBusiness, request.EffectiveDate, request.ExpirationDate, request.Premium, request.Fees, request.Taxes, request.CommissionPercent, request.PaymentPlan, request.BillingTypeCode, request.ProducerId, request.CsrId, request.CoverageSnapshotJson, request.RiskSnapshotJson, request.ComparisonSnapshotJson, request.ReviewNotes, request.ReviewedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task DecideBinderReviewAsync(Guid policyBindTransactionId, DecideBinderReviewRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF @DecidedByUserId IS NULL THROW 52206,'An authenticated decision maker is required.',1;
DECLARE @OldBindStatus NVARCHAR(50)=(SELECT BindStatusCode FROM Submissions.PolicyBindTransaction WHERE PolicyBindTransactionId=@PolicyBindTransactionId AND TenantId=@TenantId AND IsDeleted=0);
DECLARE @ReviewStatus NVARCHAR(50)=CASE @DecisionCode WHEN N'Accepted' THEN N'Accepted' WHEN N'Rejected' THEN N'Rejected' ELSE N'CorrectionRequested' END;
DECLARE @BindStatus NVARCHAR(50)=CASE @DecisionCode WHEN N'Accepted' THEN N'BinderAccepted' WHEN N'Rejected' THEN N'Rejected' ELSE N'NeedInformation' END;
UPDATE Submissions.BinderReview SET StatusCode=@ReviewStatus,ReviewNotes=COALESCE(NULLIF(@Notes,N''),ReviewNotes),AcceptedDateUtc=CASE WHEN @DecisionCode=N'Accepted' THEN SYSUTCDATETIME() ELSE AcceptedDateUtc END,AcceptedByUserId=CASE WHEN @DecisionCode=N'Accepted' THEN @DecidedByUserId ELSE AcceptedByUserId END,RejectedDateUtc=CASE WHEN @DecisionCode=N'Rejected' THEN SYSUTCDATETIME() ELSE RejectedDateUtc END,RejectedByUserId=CASE WHEN @DecisionCode=N'Rejected' THEN @DecidedByUserId ELSE RejectedByUserId END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@DecidedByUserId WHERE PolicyBindTransactionId=@PolicyBindTransactionId AND TenantId=@TenantId AND StatusCode IN(N'PendingReview',N'CorrectionRequested') AND IsDeleted=0;
IF @@ROWCOUNT=0 THROW 52202,'A pending binder review was not found.',1;
INSERT INTO Submissions.BinderReviewDecision(BinderReviewDecisionId,TenantId,BinderReviewId,PolicyBindTransactionId,DecisionCode,Notes,SnapshotJson,DecidedDateUtc,DecidedByUserId,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,BinderReviewId,@PolicyBindTransactionId,@DecisionCode,@Notes,JSON_OBJECT(N'policyNumber':PolicyNumber,N'carrierId':CarrierId,N'lineOfBusiness':LineOfBusiness,N'effectiveDate':EffectiveDate,N'expirationDate':ExpirationDate,N'premium':Premium,N'fees':Fees,N'taxes':Taxes,N'commissionPercent':CommissionPercent,N'paymentPlan':PaymentPlan,N'billingType':BillingTypeCode,N'coverage':JSON_QUERY(CoverageSnapshotJson),N'risk':JSON_QUERY(RiskSnapshotJson),N'comparison':JSON_QUERY(ComparisonSnapshotJson)),SYSUTCDATETIME(),@DecidedByUserId,SYSUTCDATETIME(),@DecidedByUserId,0 FROM Submissions.BinderReview WHERE PolicyBindTransactionId=@PolicyBindTransactionId AND TenantId=@TenantId AND IsDeleted=0;
UPDATE Submissions.PolicyBindTransaction SET BindStatusCode=@BindStatus,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@DecidedByUserId WHERE PolicyBindTransactionId=@PolicyBindTransactionId AND TenantId=@TenantId AND IsDeleted=0;
INSERT INTO Submissions.BindStatusHistory(BindStatusHistoryId,TenantId,PolicyBindTransactionId,OldStatusCode,NewStatusCode,Comments,ChangedDateUtc,ChangedByUserId,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@PolicyBindTransactionId,@OldBindStatus,@BindStatus,COALESCE(NULLIF(@Notes,N''),CONCAT(N'Binder review ',LOWER(@DecisionCode),N'.')),SYSUTCDATETIME(),@DecidedByUserId,SYSUTCDATETIME(),@DecidedByUserId,0);
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, request.TenantId, request.DecisionCode, request.Notes, request.DecidedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<PolicyGenerationRequestDto> QueuePolicyGenerationAsync(Guid policyBindTransactionId, QueuePolicyGenerationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @Id UNIQUEIDENTIFIER=(SELECT PolicyGenerationRequestId FROM Submissions.PolicyGenerationRequest WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IdempotencyKey=@IdempotencyKey AND IsDeleted=0);
IF @Id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Submissions.PolicyGenerationRequest WHERE PolicyGenerationRequestId=@Id AND PolicyBindTransactionId=@PolicyBindTransactionId) THROW 52207,'The idempotency key belongs to a different bind request.',1;
DECLARE @BinderReviewId UNIQUEIDENTIFIER=(SELECT BinderReviewId FROM Submissions.BinderReview WITH(UPDLOCK,HOLDLOCK) WHERE PolicyBindTransactionId=@PolicyBindTransactionId AND TenantId=@TenantId AND StatusCode=N'Accepted' AND IsDeleted=0);
IF @Id IS NULL AND @RequestedByUserId IS NULL THROW 52208,'An authenticated policy generation requester is required.',1;
IF @Id IS NULL AND @BinderReviewId IS NULL THROW 52203,'The carrier binder must be reviewed and accepted before policy generation.',1;
IF @Id IS NULL
BEGIN
 SET @Id=NEWID();
 INSERT INTO Submissions.PolicyGenerationRequest(PolicyGenerationRequestId,TenantId,PolicyBindTransactionId,BinderReviewId,IdempotencyKey,StatusCode,RequestedDateUtc,RequestedByUserId,NextAttemptDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@PolicyBindTransactionId,@BinderReviewId,@IdempotencyKey,N'Queued',SYSUTCDATETIME(),@RequestedByUserId,SYSUTCDATETIME(),SYSUTCDATETIME(),@RequestedByUserId,0);
 UPDATE Submissions.BinderReview SET StatusCode=N'GenerationQueued',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@RequestedByUserId WHERE BinderReviewId=@BinderReviewId;
 UPDATE Submissions.PolicyBindTransaction SET BindStatusCode=N'PolicyGenerationQueued',ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@RequestedByUserId WHERE PolicyBindTransactionId=@PolicyBindTransactionId AND TenantId=@TenantId;
 INSERT INTO Submissions.BindStatusHistory(BindStatusHistoryId,TenantId,PolicyBindTransactionId,OldStatusCode,NewStatusCode,Comments,ChangedDateUtc,ChangedByUserId,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@PolicyBindTransactionId,N'BinderAccepted',N'PolicyGenerationQueued',N'Producer queued policy generation.',SYSUTCDATETIME(),@RequestedByUserId,SYSUTCDATETIME(),@RequestedByUserId,0);
END;
ELSE UPDATE Submissions.PolicyGenerationRequest SET StatusCode=N'Queued',NextAttemptDateUtc=SYSUTCDATETIME(),FailedDateUtc=NULL,ErrorDetails=NULL,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@RequestedByUserId WHERE PolicyGenerationRequestId=@Id AND StatusCode=N'Failed';
COMMIT;
SELECT PolicyGenerationRequestId,TenantId,PolicyBindTransactionId,BinderReviewId,StatusCode,RequestedDateUtc,RequestedByUserId,CompletedDateUtc,AttemptCount,ErrorDetails,PolicyId FROM Submissions.PolicyGenerationRequest WHERE PolicyGenerationRequestId=@Id;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<PolicyGenerationRequestDto>(new CommandDefinition(sql, new { PolicyBindTransactionId = policyBindTransactionId, request.TenantId, request.IdempotencyKey, request.RequestedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ManualPolicyOptionDto>> GetManualPolicyOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT OptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, RequiresDocument, RequiresElevatedPermission, IsDefault, SortOrder
FROM Policy.ManualPolicyOption
WHERE TenantId = @TenantId
  AND IsActive = 1
  AND IsDeleted = 0
ORDER BY OptionGroupCode, SortOrder, DisplayName;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var options = await connection.QueryAsync<ManualPolicyOptionDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return options.AsList();
    }

    public async Task<ManualPolicyDraftDto> SaveManualPolicyDraftAsync(Guid? draftId, UpsertManualPolicyDraftRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @DraftId UNIQUEIDENTIFIER = COALESCE(@RequestedDraftId, NEWID());

IF EXISTS (SELECT 1 FROM Policy.ManualPolicyDraft WHERE DraftId = @DraftId AND TenantId = @TenantId AND AccountId = @AccountId AND IsDeleted = 0)
BEGIN
    UPDATE Policy.ManualPolicyDraft
    SET CurrentStep = @CurrentStep,
        PayloadJson = @PayloadJson,
        StatusCode = CASE WHEN StatusCode = N'Submitted' THEN StatusCode ELSE N'InProgress' END,
        UpdatedAtUtc = @Now,
        ExpiresAtUtc = DATEADD(day, 30, @Now)
    WHERE DraftId = @DraftId;
END
ELSE
BEGIN
    INSERT INTO Policy.ManualPolicyDraft
        (DraftId, TenantId, AccountId, CurrentStep, StatusCode, PayloadJson, CreatedByUserId, CreatedAtUtc, UpdatedAtUtc, ExpiresAtUtc, IsDeleted)
    VALUES
        (@DraftId, @TenantId, @AccountId, @CurrentStep, N'InProgress', @PayloadJson, @ModifiedByUserId, @Now, @Now, DATEADD(day, 30, @Now), 0);
END;

SELECT DraftId, TenantId, AccountId, CurrentStep, StatusCode, PayloadJson, CreatedAtUtc, UpdatedAtUtc, ExpiresAtUtc
FROM Policy.ManualPolicyDraft
WHERE DraftId = @DraftId;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<ManualPolicyDraftDto>(new CommandDefinition(sql, new
        {
            RequestedDraftId = draftId,
            request.TenantId,
            request.AccountId,
            request.CurrentStep,
            PayloadJson = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<ManualPolicyDraftDto?> GetManualPolicyDraftAsync(Guid tenantId, Guid accountId, Guid draftId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT DraftId, TenantId, AccountId, CurrentStep, StatusCode, PayloadJson, CreatedAtUtc, UpdatedAtUtc, ExpiresAtUtc
FROM Policy.ManualPolicyDraft
WHERE DraftId = @DraftId
  AND TenantId = @TenantId
  AND AccountId = @AccountId
  AND IsDeleted = 0;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ManualPolicyDraftDto>(new CommandDefinition(sql, new { TenantId = tenantId, AccountId = accountId, DraftId = draftId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ManualPolicyDuplicateCandidateDto>> FindManualPolicyDuplicatesAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NormalizedPolicyNumber NVARCHAR(80) = @NormalizedNumber;

SELECT TOP 10
    bp.PolicyId,
    bp.PolicyNumber,
    COALESCE(bp.NormalizedPolicyNumber, @NormalizedPolicyNumber) AS NormalizedPolicyNumber,
    COALESCE(c.CarrierName, c.Name, N'') AS CarrierName,
    COALESCE(bp.LineOfBusiness, @LineOfBusiness) AS LineOfBusiness,
    bp.EffectiveDate,
    bp.ExpirationDate,
    CASE
        WHEN bp.AccountId = @AccountId
         AND bp.CarrierId = @CarrierId
         AND COALESCE(bp.NormalizedPolicyNumber, N'') = @NormalizedPolicyNumber
         AND CONVERT(date, bp.EffectiveDate) = @EffectiveDate
         AND CONVERT(date, bp.ExpirationDate) = @ExpirationDate THEN N'ExactDuplicate'
        WHEN COALESCE(bp.NormalizedPolicyNumber, N'') = @NormalizedPolicyNumber
         AND CONVERT(date, bp.EffectiveDate) = @EffectiveDate THEN N'PossibleDuplicate'
        WHEN COALESCE(bp.NormalizedPolicyNumber, N'') = @NormalizedPolicyNumber THEN N'RenewalTerm'
        ELSE N'PossibleDuplicate'
    END AS Classification
FROM Submissions.BoundPolicy bp
LEFT JOIN Core.Carrier c ON c.CarrierId = bp.CarrierId
WHERE bp.TenantId = @TenantId
  AND bp.IsDeleted = 0
  AND COALESCE(bp.NormalizedPolicyNumber, UPPER(REPLACE(REPLACE(REPLACE(REPLACE(bp.PolicyNumber, N'-', N''), N' ', N''), N'.', N''), N'/', N''))) = @NormalizedPolicyNumber
  AND (@CarrierId = '00000000-0000-0000-0000-000000000000' OR bp.CarrierId = @CarrierId OR bp.AccountId = @AccountId)
ORDER BY
    CASE WHEN bp.AccountId = @AccountId AND bp.CarrierId = @CarrierId AND CONVERT(date, bp.EffectiveDate) = @EffectiveDate AND CONVERT(date, bp.ExpirationDate) = @ExpirationDate THEN 0 ELSE 1 END,
    bp.EffectiveDate DESC;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var duplicates = await connection.QueryAsync<ManualPolicyDuplicateCandidateDto>(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.AccountId,
            request.CarrierId,
            request.LineOfBusiness,
            EffectiveDate = request.EffectiveDate,
            ExpirationDate = request.ExpirationDate,
            NormalizedNumber = NormalizePolicyNumber(request.PolicyNumber)
        }, cancellationToken: cancellationToken));
        return duplicates.AsList();
    }

    public async Task<ManualPolicyCreateResultDto> CreateManualPolicyAsync(CreateManualPolicyRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @PolicyId UNIQUEIDENTIFIER = NEWID();
DECLARE @PolicyTermId UNIQUEIDENTIFIER = NEWID();
DECLARE @AnnualPremium DECIMAL(18,2) = COALESCE(@AnnualizedPremium, @WrittenPremium, 0);
DECLARE @Status NVARCHAR(50) = COALESCE(NULLIF(@PolicyStatus, N''), N'PendingVerification');
DECLARE @IssueStatus NVARCHAR(50) = CASE WHEN @Status IN (N'Active', N'PendingVerification') THEN N'Issued' ELSE @Status END;
DECLARE @CoverageStatus NVARCHAR(50) = CASE WHEN @TermStatus = N'Future' THEN N'Future' WHEN @Status = N'Cancelled' THEN N'Cancelled' WHEN @Status = N'Expired' THEN N'Expired' ELSE N'Active' END;

IF NOT EXISTS (SELECT 1 FROM Client.Account WHERE AccountId = @AccountId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52300, 'Manual policy creation requires a valid account.', 1;

IF EXISTS
(
    SELECT 1
    FROM Submissions.BoundPolicy
    WHERE TenantId = @TenantId
      AND AccountId = @AccountId
      AND CarrierId = @CarrierId
      AND COALESCE(NormalizedPolicyNumber, UPPER(REPLACE(REPLACE(REPLACE(REPLACE(PolicyNumber, N'-', N''), N' ', N''), N'.', N''), N'/', N''))) = @NormalizedPolicyNumber
      AND CONVERT(date, EffectiveDate) = @EffectiveDate
      AND CONVERT(date, ExpirationDate) = @ExpirationDate
      AND IsDeleted = 0
)
    THROW 52301, 'A matching policy already exists.', 1;

INSERT INTO Submissions.BoundPolicy
    (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId, PolicyNumber, Status, IssueStatus, CoverageStatus, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IssuedDateUtc, PolicySourceCode, PolicySourceReason, PolicySourceNotes, PolicyBindTransactionId, LineOfBusiness, NormalizedPolicyNumber, WritingCompanyId, BrokerOrMgaId, PolicyType, PolicyForm, PolicyDescription, DataCompletenessCode, VerificationStatusCode, IsDeleted)
VALUES
    (@PolicyId, NULL, NULL, @TenantId, @AccountId, @CarrierId, @PolicyNumber, @Status, @IssueStatus, @CoverageStatus, @AnnualPremium, @EffectiveDate, @ExpirationDate, @Now, CASE WHEN @PolicyIssueDate IS NULL THEN @Now ELSE CAST(@PolicyIssueDate AS DATETIME2) END, @PolicySourceCode, @ManualReasonCode, @Notes, NULL, @LineOfBusiness, @NormalizedPolicyNumber, @WritingCompanyId, @BrokerOrMgaId, @PolicyType, @PolicyForm, @PolicyDescription, @DataCompletenessCode, N'PendingVerification', 0);

INSERT INTO Policy.PolicyTerm
    (PolicyTermId, TenantId, PolicyId, TermNumber, EffectiveDate, ExpirationDate, TermStatusCode, TransactionTypeCode, WrittenPremium, AnnualizedPremium, Taxes, Fees, Surcharges, TotalCost, BillingTypeCode, DataCompletenessCode, CreatedDateUtc, IsDeleted)
VALUES
    (@PolicyTermId, @TenantId, @PolicyId, 1, @EffectiveDate, @ExpirationDate, @TermStatus, @TransactionTypeCode, @WrittenPremium, @AnnualizedPremium, @Taxes, @Fees, @Surcharges, @TotalCost, @BillingTypeCode, @DataCompletenessCode, @Now, 0);

INSERT INTO Policy.PolicySource
    (PolicySourceId, TenantId, PolicyId, SourceCode, ManualReasonCode, ExternalSystem, ExternalReference, CarrierPortalReference, MigrationBatch, SourceNotes, RecordedByUserId, RecordedAtUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyId, @PolicySourceCode, @ManualReasonCode, @ExternalSystem, COALESCE(@ExternalReference, @CarrierPortalReference), @CarrierPortalReference, @MigrationBatch, @Notes, @CreatedByUserId, @Now, 0);

INSERT INTO Policy.PolicyNamedInsured
    (PolicyNamedInsuredId, TenantId, PolicyId, LegalName, DbaName, AddressSnapshotJson, IsPrimary, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyId, @NamedInsured, @DbaName, COALESCE(NULLIF(@MailingAddressJson, N''), N'{}'), 1, @Now, 0);

INSERT INTO Policy.PolicyCoverageSummary
    (PolicyCoverageSummaryId, TenantId, PolicyTermId, CoverageSummary, LimitsSummary, DeductibleSummary, CoverageNotes, RiskSnapshotJson, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyTermId, @CoverageSummary, @LimitsSummary, @DeductibleSummary, @CoverageNotes, @RiskSnapshotJson, @Now, 0);

INSERT INTO Policy.PolicyLine
    (PolicyLineId, TenantId, PolicyId, PolicyTermId, LineOfBusinessId, LineOfBusinessCode, LineOfBusinessName, PolicyLineStatusCode, WrittenPremium, CoverageSummary, LimitsSummary, DeductibleSummary, SortOrder, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, @PolicyId, @PolicyTermId, LineOfBusinessId, LineOfBusinessCode, LineOfBusinessName, PolicyLineStatusCode, WrittenPremium, CoverageSummary, LimitsSummary, DeductibleSummary, SortOrder, @Now, 0
FROM @PolicyLines;

INSERT INTO Policy.PolicyAssignment
    (PolicyAssignmentId, TenantId, PolicyId, Agency, Branch, Department, ProducerId, AccountManagerId, CsrId, ProducerName, AccountManagerName, CsrName, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyId, @Agency, @Branch, @Department, @ProducerId, @AccountManagerId, @CsrId, @ProducerName, @AccountManagerName, @CsrName, @Now, 0);

INSERT INTO Policy.PolicyCommissionEstimate
    (PolicyCommissionEstimateId, TenantId, PolicyId, PolicyTermId, CommissionTypeCode, CommissionStatusCode, CommissionRate, EstimatedCommission, ProducerSplitPercent, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, @PolicyId, @PolicyTermId, @CommissionTypeCode, N'Estimated', @CommissionRate, @EstimatedCommission, @ProducerSplitPercent, @Now, 0);

INSERT INTO Policy.PolicyAuditEvent
    (PolicyAuditEventId, TenantId, EntityType, EntityId, ActionCode, SourceCode, ReasonCode, UserId, BeforeJson, AfterJson, CreatedDateUtc, IsDeleted)
VALUES
    (NEWID(), @TenantId, N'Policy', @PolicyId, N'ManualPolicyCreated', N'UserInterface', @ManualReasonCode, @CreatedByUserId, NULL, @AuditJson, @Now, 0);

IF @DraftId IS NOT NULL
BEGIN
    UPDATE Policy.ManualPolicyDraft
    SET StatusCode = N'Submitted', SubmittedPolicyId = @PolicyId, UpdatedAtUtc = @Now
    WHERE DraftId = @DraftId AND TenantId = @TenantId AND AccountId = @AccountId AND IsDeleted = 0;
END;

SELECT @PolicyId AS PolicyId, @PolicyTermId AS PolicyTermId, @PolicyNumber AS PolicyNumber, @Status AS Status, @DataCompletenessCode AS DataCompleteness;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<ManualPolicyCreateResultDto>(new CommandDefinition(sql, BuildManualPolicyParameters(request), cancellationToken: cancellationToken));
    }

    private static object BuildManualPolicyParameters(CreateManualPolicyRequest request)
    {
        var audit = JsonSerializer.Serialize(new
        {
            request.AccountId,
            request.CarrierId,
            request.PolicyNumber,
            request.PolicySourceCode,
            request.ManualReasonCode,
            request.LineOfBusiness,
            request.EffectiveDate,
            request.ExpirationDate,
            request.WrittenPremium,
            request.AnnualizedPremium,
            request.BillingTypeCode,
            request.DataCompletenessCode
        });

        return new
        {
            request.TenantId,
            request.AccountId,
            request.DraftId,
            request.CarrierId,
            WritingCompanyId = request.WritingCompanyId ?? request.CarrierId,
            request.BrokerOrMgaId,
            PolicyNumber = request.PolicyNumber.Trim(),
            NormalizedPolicyNumber = NormalizePolicyNumber(request.PolicyNumber),
            PolicySourceCode = string.IsNullOrWhiteSpace(request.PolicySourceCode) ? "ManualExistingPolicy" : request.PolicySourceCode.Trim(),
            ManualReasonCode = request.ManualReasonCode.Trim(),
            request.ExternalSystem,
            request.ExternalReference,
            request.CarrierPortalReference,
            request.MigrationBatch,
            LineOfBusiness = request.LineOfBusiness.Trim(),
            request.PolicyType,
            request.PolicyStatus,
            TermStatus = request.TermStatus,
            request.TransactionTypeCode,
            request.PolicyForm,
            request.PolicyDescription,
            NamedInsured = request.NamedInsured.Trim(),
            request.DbaName,
            request.MailingAddressJson,
            request.RiskSnapshotJson,
            request.EffectiveDate,
            request.ExpirationDate,
            request.PolicyIssueDate,
            request.WrittenPremium,
            request.AnnualizedPremium,
            request.Taxes,
            request.Fees,
            request.Surcharges,
            request.TotalCost,
            request.BillingTypeCode,
            request.CoverageSummary,
            request.LimitsSummary,
            request.DeductibleSummary,
            request.CoverageNotes,
            request.DataCompletenessCode,
            request.Agency,
            request.Branch,
            request.Department,
            request.ProducerId,
            request.AccountManagerId,
            request.CsrId,
            request.ProducerName,
            request.AccountManagerName,
            request.CsrName,
            request.CommissionTypeCode,
            request.CommissionRate,
            request.EstimatedCommission,
            request.ProducerSplitPercent,
            request.Notes,
            request.CreatedByUserId,
            PolicyLines = BuildPolicyLinesTable(request).AsTableValuedParameter("Policy.PolicyLineCreateTableType"),
            AuditJson = audit
        };
    }

    private static DataTable BuildPolicyLinesTable(CreateManualPolicyRequest request)
    {
        var table = new DataTable();
        table.Columns.Add("LineOfBusinessId", typeof(Guid));
        table.Columns.Add("LineOfBusinessCode", typeof(string));
        table.Columns.Add("LineOfBusinessName", typeof(string));
        table.Columns.Add("PolicyLineStatusCode", typeof(string));
        table.Columns.Add("WrittenPremium", typeof(decimal));
        table.Columns.Add("CoverageSummary", typeof(string));
        table.Columns.Add("LimitsSummary", typeof(string));
        table.Columns.Add("DeductibleSummary", typeof(string));
        table.Columns.Add("SortOrder", typeof(int));

        foreach (var line in request.PolicyLines.OrderBy(line => line.SortOrder))
        {
            table.Rows.Add(
                line.LineOfBusinessId.HasValue ? line.LineOfBusinessId.Value : DBNull.Value,
                line.LineOfBusinessCode,
                line.LineOfBusinessName,
                string.IsNullOrWhiteSpace(line.PolicyLineStatusCode) ? "Active" : line.PolicyLineStatusCode,
                line.WrittenPremium.HasValue ? line.WrittenPremium.Value : DBNull.Value,
                string.IsNullOrWhiteSpace(line.CoverageSummary) ? DBNull.Value : line.CoverageSummary,
                string.IsNullOrWhiteSpace(line.LimitsSummary) ? DBNull.Value : line.LimitsSummary,
                string.IsNullOrWhiteSpace(line.DeductibleSummary) ? DBNull.Value : line.DeductibleSummary,
                line.SortOrder);
        }

        return table;
    }

    private static string NormalizePolicyNumber(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
