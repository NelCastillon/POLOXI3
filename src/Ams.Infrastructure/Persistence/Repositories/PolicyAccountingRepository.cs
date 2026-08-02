using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyAccountingRepository(ISqlConnectionFactory connectionFactory) : IPolicyAccountingRepository
{
    public async Task<InvoiceDeliveryDispatchDto> EmailInvoiceAsync(Guid policyId, Guid invoiceId, EmailPolicyInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @DispatchId UNIQUEIDENTIFIER=NEWID(),@ProviderId UNIQUEIDENTIFIER,@MaxAttempts INT,@IsConfigured BIT,@StatusCode NVARCHAR(50),@AccountId UNIQUEIDENTIFIER,@InvoiceNumber NVARCHAR(80),@PolicyNumber NVARCHAR(100),@DueDate DATE,@TotalAmount DECIMAL(18,2),@BalanceAmount DECIMAL(18,2),@CurrencyCode NVARCHAR(3),@AgencyName NVARCHAR(240);
SELECT @AccountId=i.AccountId,@InvoiceNumber=i.InvoiceNumber,@DueDate=i.DueDate,@TotalAmount=i.TotalAmount,@BalanceAmount=i.BalanceAmount,@CurrencyCode=i.CurrencyCode,@PolicyNumber=bp.PolicyNumber
FROM Billing.Invoice i JOIN Submissions.BoundPolicy bp ON bp.TenantId=i.TenantId AND bp.PolicyId=i.PolicyId AND bp.IsDeleted=0
WHERE i.TenantId=@TenantId AND i.InvoiceId=@InvoiceId AND i.PolicyId=@PolicyId AND i.IsDeleted=0;
IF @InvoiceNumber IS NULL THROW 52320,N'Policy invoice was not found.',1;
SELECT TOP 1 @ProviderId=ProposalDeliveryProviderId,@MaxAttempts=MaxAttempts,@IsConfigured=IsConfigured FROM Submissions.ProposalDeliveryProvider WHERE TenantId=@TenantId AND DeliveryMethodCode=N'Email' AND DeliveryCategoryCode IN(N'All',N'Invoice') AND IsActive=1 AND IsDeleted=0 ORDER BY CASE DeliveryCategoryCode WHEN N'Invoice' THEN 0 ELSE 1 END;
IF @ProviderId IS NULL THROW 52321,N'Email delivery is not configured for this tenant.',1;
IF EXISTS(SELECT 1 FROM Submissions.ProposalDeliveryDispatch WHERE TenantId=@TenantId AND DeliveryCategoryCode=N'Invoice' AND EntityName=N'Billing.Invoice' AND EntityId=@InvoiceId AND Recipient=@Recipient AND StatusCode IN(N'Queued',N'Processing',N'Delivered') AND IsDeleted=0) THROW 52322,N'An active invoice email delivery already exists for this recipient.',1;
SELECT TOP 1 @AgencyName=TenantName FROM Core.Tenant WHERE TenantId=@TenantId AND IsDeleted=0;
SET @StatusCode=CASE WHEN @IsConfigured=1 THEN N'Queued' ELSE N'Configuration Required' END;
INSERT Submissions.ProposalDeliveryDispatch(ProposalDeliveryDispatchId,TenantId,SubmissionId,ProposalId,ProposalDeliveryProviderId,ProposalVersionNumber,DeliveryMethodCode,Recipient,StatusCode,AttemptCount,MaxAttempts,NextAttemptDateUtc,ErrorCode,ErrorMessage,CreatedDateUtc,CreatedByUserId,IsDeleted,DeliveryCategoryCode,DeliveryTypeCode,EntityName,EntityId,AccountId,Subject,HtmlContent,DocumentId,RequestJson)
VALUES(@DispatchId,@TenantId,NULL,NULL,@ProviderId,NULL,N'Email',@Recipient,@StatusCode,0,@MaxAttempts,CASE WHEN @StatusCode=N'Queued' THEN SYSUTCDATETIME() END,CASE WHEN @StatusCode=N'Configuration Required' THEN N'PROVIDER_NOT_CONFIGURED' END,CASE WHEN @StatusCode=N'Configuration Required' THEN N'Tenant email provider requires configuration.' END,SYSUTCDATETIME(),@UserId,0,N'Invoice',N'PolicyInvoice',N'Billing.Invoice',@InvoiceId,@AccountId,CONCAT(N'Invoice ',@InvoiceNumber,N' for policy ',@PolicyNumber),CONCAT(N'<p>Hello,</p><p>Please find invoice <strong>',@InvoiceNumber,N'</strong> for policy <strong>',@PolicyNumber,N'</strong>.</p><table style="border-collapse:collapse"><tr><td style="padding:6px"><strong>Due date</strong></td><td style="padding:6px">',CONVERT(nvarchar(30),@DueDate,107),N'</td></tr><tr><td style="padding:6px"><strong>Total</strong></td><td style="padding:6px">',@CurrencyCode,N' ',FORMAT(@TotalAmount,N'N2'),N'</td></tr><tr><td style="padding:6px"><strong>Balance</strong></td><td style="padding:6px">',@CurrencyCode,N' ',FORMAT(@BalanceAmount,N'N2'),N'</td></tr></table><p>Questions? Contact ',COALESCE(@AgencyName,N'your insurance agency'),N'.</p>'),NULL,JSON_OBJECT(N'invoiceId':@InvoiceId,N'policyId':@PolicyId,N'invoiceNumber':@InvoiceNumber,N'recipient':@Recipient));
INSERT Accounting.PolicyAccountingAuditEvent(PolicyAccountingAuditEventId,TenantId,PolicyId,PolicyCreatedEventId,EventTypeCode,EventDescription,DataJson,ActorUserId,CreatedDateUtc)
SELECT NEWID(),@TenantId,@PolicyId,s.PolicyCreatedEventId,N'InvoiceEmailQueued',N'Policy invoice email delivery queued through proposal delivery infrastructure.',JSON_OBJECT(N'invoiceId':@InvoiceId,N'dispatchId':@DispatchId,N'recipient':@Recipient),@UserId,SYSUTCDATETIME() FROM Accounting.PolicyAccountingState s WHERE s.TenantId=@TenantId AND s.PolicyId=@PolicyId AND s.IsDeleted=0;
COMMIT; SELECT @DispatchId DeliveryDispatchId,@InvoiceId InvoiceId,@Recipient Recipient,@StatusCode StatusCode,SYSUTCDATETIME() CreatedDateUtc;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<InvoiceDeliveryDispatchDto>(new CommandDefinition(sql, new { request.TenantId, PolicyId = policyId, InvoiceId = invoiceId, request.Recipient, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> RemitCarrierPayableAsync(Guid carrierPayableId, RemitCarrierPayableRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRAN;
DECLARE @PolicyId UNIQUEIDENTIFIER,@PolicyTermId UNIQUEIDENTIFIER,@ReceivableId UNIQUEIDENTIFIER,@PayableBalance DECIMAL(18,2),@TrustBalance DECIMAL(18,2),@TrustRequired BIT,@TrustAccount UNIQUEIDENTIFIER,@PayableAccount UNIQUEIDENTIFIER,@JournalEntryId UNIQUEIDENTIFIER=NEWID(),@ExistingJournalEntryId UNIQUEIDENTIFIER,@Now DATETIME2=SYSUTCDATETIME();
SELECT @ExistingJournalEntryId=JournalEntryId FROM Finance.JournalEntry WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND SourceEventId=@CarrierPayableId AND EntryTypeCode=N'CarrierRemittance' AND IsDeleted=0;
IF @ExistingJournalEntryId IS NOT NULL BEGIN COMMIT; SELECT @ExistingJournalEntryId; RETURN; END;
SELECT @PolicyId=PolicyId,@PolicyTermId=PolicyTermId,@PayableBalance=PayableAmount-PaidAmount,@TrustRequired=TrustRequired FROM Accounting.CarrierPayable WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND CarrierPayableId=@CarrierPayableId AND IsDeleted=0 AND StatusCode<>N'Remitted';
IF @PolicyId IS NULL OR @Amount<>@PayableBalance THROW 52310,N'Open carrier payable was not found or the remittance must equal its remaining balance.',1;
SELECT @ReceivableId=AgencyBillReceivableId FROM Billing.AgencyBillReceivable WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND PolicyTermId=@PolicyTermId AND IsDeleted=0;
SELECT @TrustBalance=COALESCE(SUM(CASE WHEN DirectionCode=N'Credit' THEN Amount ELSE -Amount END),0) FROM Billing.PremiumTrustTransaction WHERE TenantId=@TenantId AND AgencyBillReceivableId=@ReceivableId AND StatusCode=N'Posted' AND IsDeleted=0;
IF @TrustRequired=1 AND @Amount>@TrustBalance THROW 52311,N'Carrier remittance exceeds cleared premium trust funds.',1;
SELECT @TrustAccount=MAX(CASE WHEN o.OptionCode=N'PremiumTrustCash' THEN a.GLAccountId END),@PayableAccount=MAX(CASE WHEN o.OptionCode=N'PremiumPayable' THEN a.GLAccountId END) FROM Accounting.PolicyAccountingOption o JOIN Finance.GLAccount a ON a.TenantId=o.TenantId AND a.AccountCode=o.TextValue AND a.IsDeleted=0 AND a.IsActive=1 WHERE o.TenantId=@TenantId AND o.OptionGroupCode=N'GLAccount' AND o.IsActive=1 AND o.IsDeleted=0;
IF @TrustAccount IS NULL OR @PayableAccount IS NULL THROW 52312,N'Required remittance GL account mapping is missing.',1;
UPDATE Accounting.CarrierPayable SET PaidAmount=PaidAmount+@Amount,StatusCode=CASE WHEN PaidAmount+@Amount>=PayableAmount THEN N'Remitted' ELSE N'PartiallyRemitted' END,RemittedDateUtc=CASE WHEN PaidAmount+@Amount>=PayableAmount THEN @Now ELSE NULL END,ModifiedDateUtc=@Now,ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND CarrierPayableId=@CarrierPayableId;
INSERT Billing.PremiumTrustTransaction(PremiumTrustTransactionId,TenantId,TrustAccountCode,TransactionTypeCode,AgencyBillReceivableId,TransactionDate,Amount,DirectionCode,ReferenceNumber,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,N'PREMIUM_TRUST',N'CarrierRemittance',@ReceivableId,@RemittanceDate,@Amount,N'Debit',@ReferenceNumber,N'Posted',@Now,@UserId,0);
INSERT Finance.JournalEntry(JournalEntryId,TenantId,EntryNumber,EntryDate,Description,TotalDebit,TotalCredit,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted,PolicyId,PolicyTermId,SourceEventId,EntryTypeCode) VALUES(@JournalEntryId,@TenantId,CONCAT(N'JE-REM-',RIGHT(REPLACE(CONVERT(NVARCHAR(36),@JournalEntryId),N'-',N''),12)),@RemittanceDate,N'Carrier premium remittance',@Amount,@Amount,N'Posted',@Now,@UserId,0,@PolicyId,@PolicyTermId,@CarrierPayableId,N'CarrierRemittance');
INSERT Finance.JournalEntryLine(LineId,JournalEntryId,GLAccountId,DebitAmount,CreditAmount,Description,LineOrder,TenantId,PolicyId,AccountingCategoryCode) VALUES(NEWID(),@JournalEntryId,@PayableAccount,@Amount,0,N'Reduce carrier premium payable',1,@TenantId,@PolicyId,N'PremiumPayable'),(NEWID(),@JournalEntryId,@TrustAccount,0,@Amount,N'Premium trust cash remitted',2,@TenantId,@PolicyId,N'PremiumTrustCash');
INSERT Accounting.PolicyAccountingAuditEvent(PolicyAccountingAuditEventId,TenantId,PolicyId,PolicyCreatedEventId,EventTypeCode,EventDescription,DataJson,ActorUserId,CreatedDateUtc) SELECT NEWID(),@TenantId,@PolicyId,PolicyCreatedEventId,N'CarrierRemitted',N'Carrier payable remittance posted.',JSON_OBJECT(N'CarrierPayableId':@CarrierPayableId,N'Amount':@Amount,N'ReferenceNumber':@ReferenceNumber),@UserId,@Now FROM Accounting.CarrierPayable WHERE TenantId=@TenantId AND CarrierPayableId=@CarrierPayableId;
COMMIT; SELECT @JournalEntryId;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(sql, new { request.TenantId, CarrierPayableId = carrierPayableId, request.Amount, request.RemittanceDate, request.ReferenceNumber, request.UserId }, cancellationToken: cancellationToken));
    }

    public async Task ProcessPolicyCreatedEventAsync(Guid eventId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

DECLARE @PolicyId UNIQUEIDENTIFIER,@PolicyTermId UNIQUEIDENTIFIER,@PolicyBindTransactionId UNIQUEIDENTIFIER,@CreatedByUserId UNIQUEIDENTIFIER;
DECLARE @PolicyNumber NVARCHAR(80),@BillingTypeCode NVARCHAR(50),@PaymentPlan NVARCHAR(200),@AccountName NVARCHAR(200),@CarrierName NVARCHAR(200),@LineOfBusiness NVARCHAR(100);
DECLARE @AccountId UNIQUEIDENTIFIER,@CarrierId UNIQUEIDENTIFIER,@ProducerId UNIQUEIDENTIFIER,@CommissionTransactionId UNIQUEIDENTIFIER;
DECLARE @EffectiveDate DATE,@ExpirationDate DATE,@Premium DECIMAL(18,2),@Fees DECIMAL(18,2),@Taxes DECIMAL(18,2),@InvoiceTotal DECIMAL(18,2),@CommissionRate DECIMAL(9,4),@CommissionAmount DECIMAL(18,2),@CarrierPayable DECIMAL(18,2);
DECLARE @InvoiceId UNIQUEIDENTIFIER,@ReceivableId UNIQUEIDENTIFIER,@CarrierPayableId UNIQUEIDENTIFIER,@ExpectedReceivableId UNIQUEIDENTIFIER,@JournalEntryId UNIQUEIDENTIFIER,@StateId UNIQUEIDENTIFIER;
DECLARE @InstallmentCount INT,@GraceDays INT,@InsuredDueDays INT,@CarrierDueDays INT,@TrustRequired BIT,@Now DATETIME2=SYSUTCDATETIME();
DECLARE @ArAccount UNIQUEIDENTIFIER,@PremiumPayableAccount UNIQUEIDENTIFIER,@FeeRevenueAccount UNIQUEIDENTIFIER,@TaxPayableAccount UNIQUEIDENTIFIER,@CommissionReceivableAccount UNIQUEIDENTIFIER,@CommissionRevenueAccount UNIQUEIDENTIFIER;

SELECT @PolicyId=e.PolicyId,@PolicyTermId=e.PolicyTermId,@PolicyBindTransactionId=e.PolicyBindTransactionId,@CreatedByUserId=e.CreatedByUserId
FROM Accounting.PolicyCreatedEvent e WITH(UPDLOCK,HOLDLOCK)
WHERE e.PolicyCreatedEventId=@EventId AND e.TenantId=@TenantId AND e.StatusCode=N'Processing' AND e.IsDeleted=0;
IF @PolicyId IS NULL THROW 52300,N'PolicyCreated accounting event is not actively claimed.',1;

SELECT @PolicyNumber=bp.PolicyNumber,@AccountId=bp.AccountId,@CarrierId=bp.CarrierId,
       @EffectiveDate=pt.EffectiveDate,@ExpirationDate=pt.ExpirationDate,@Premium=COALESCE(pt.WrittenPremium,pt.AnnualizedPremium,0),
       @Fees=COALESCE(pt.Fees,0),@Taxes=COALESCE(pt.Taxes,0),@BillingTypeCode=COALESCE(NULLIF(pt.BillingTypeCode,N''),N'AgencyBill')
FROM Policy.PolicyTerm pt
JOIN Submissions.BoundPolicy bp ON bp.PolicyId=pt.PolicyId AND bp.TenantId=pt.TenantId AND bp.IsDeleted=0
WHERE pt.TenantId=@TenantId AND pt.PolicyId=@PolicyId AND pt.PolicyTermId=@PolicyTermId AND pt.IsDeleted=0;
IF @PolicyNumber IS NULL THROW 52301,N'Authoritative policy term was not found.',1;

SELECT @PaymentPlan=br.PaymentPlan,@CommissionRate=COALESCE(br.CommissionPercent,pbt.CommissionRatePct,0),
       @CommissionAmount=COALESCE(pbt.EstimatedGrossCommission,ROUND(@Premium*COALESCE(br.CommissionPercent,pbt.CommissionRatePct,0)/100,2)),
       @ProducerId=COALESCE(br.ProducerId,pbt.RequestedByUserId),@LineOfBusiness=br.LineOfBusiness
FROM Submissions.PolicyBindTransaction pbt
LEFT JOIN Submissions.BinderReview br ON br.TenantId=pbt.TenantId AND br.PolicyBindTransactionId=pbt.PolicyBindTransactionId AND br.IsDeleted=0
WHERE pbt.TenantId=@TenantId AND pbt.PolicyBindTransactionId=@PolicyBindTransactionId AND pbt.IsDeleted=0;
SET @CommissionRate=COALESCE(@CommissionRate,0); SET @CommissionAmount=COALESCE(@CommissionAmount,ROUND(@Premium*@CommissionRate/100,2));
SET @CarrierPayable=CASE WHEN @Premium-@CommissionAmount<0 THEN 0 ELSE @Premium-@CommissionAmount END;
SET @InvoiceTotal=@Premium+@Fees+@Taxes;
SELECT @AccountName=AccountName FROM Client.Account WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0;
SELECT @CarrierName=CarrierName FROM Agency.Carrier WHERE TenantId=@TenantId AND CarrierId=@CarrierId AND IsDeleted=0;

SELECT @GraceDays=CONVERT(INT,MAX(CASE WHEN OptionGroupCode=N'Installment' AND OptionCode=N'GraceDays' THEN NumericValue END)),
       @InstallmentCount=CONVERT(INT,MAX(CASE WHEN OptionGroupCode=N'Installment' AND OptionCode=N'DefaultCount' THEN NumericValue END)),
       @InsuredDueDays=CONVERT(INT,MAX(CASE WHEN OptionGroupCode=N'PaymentTerms' AND OptionCode=N'InsuredDueDays' THEN NumericValue END)),
       @CarrierDueDays=CONVERT(INT,MAX(CASE WHEN OptionGroupCode=N'PaymentTerms' AND OptionCode=N'CarrierDueDays' THEN NumericValue END)),
       @TrustRequired=CONVERT(BIT,MAX(CASE WHEN OptionGroupCode=N'Trust' AND OptionCode=N'PremiumTrustRequired' AND LOWER(TextValue)=N'true' THEN 1 ELSE 0 END))
FROM Accounting.PolicyAccountingOption WHERE TenantId=@TenantId AND IsActive=1 AND IsDeleted=0;
SET @GraceDays=COALESCE(@GraceDays,10); SET @InstallmentCount=COALESCE(@InstallmentCount,1); SET @InsuredDueDays=COALESCE(@InsuredDueDays,30); SET @CarrierDueDays=COALESCE(@CarrierDueDays,30); SET @TrustRequired=COALESCE(@TrustRequired,1);
SET @InstallmentCount=CASE WHEN @PaymentPlan LIKE N'%12%' OR @PaymentPlan LIKE N'%Monthly%' THEN 12 WHEN @PaymentPlan LIKE N'%Quarter%' OR @PaymentPlan LIKE N'%4%' THEN 4 WHEN @PaymentPlan LIKE N'%Semi%' OR @PaymentPlan LIKE N'%2%' THEN 2 WHEN @PaymentPlan LIKE N'%Annual%' OR @PaymentPlan LIKE N'%Full%' THEN 1 ELSE @InstallmentCount END;
SET @InstallmentCount=CASE WHEN @InstallmentCount<1 THEN 1 WHEN @InstallmentCount>120 THEN 120 ELSE @InstallmentCount END;

SELECT @CommissionTransactionId=TransactionId FROM Commission.CommissionTransaction WHERE TenantId=@TenantId AND SourceEntityName=N'Policy' AND SourceEntityId=@PolicyId AND IsDeleted=0 ORDER BY CreatedDateUtc;

IF @BillingTypeCode=N'AgencyBill'
BEGIN
    SELECT @InvoiceId=InvoiceId FROM Billing.Invoice WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND SourceEventId=@EventId AND IsDeleted=0;
    IF @InvoiceId IS NULL BEGIN SET @InvoiceId=NEWID(); INSERT Billing.Invoice(InvoiceId,TenantId,InvoiceNumber,AccountId,PolicyId,PolicyTermId,SourceEventId,InvoiceDate,DueDate,TotalAmount,BalanceAmount,CurrencyCode,BillingTypeCode,StatusCode,InvoiceStatusCodeId,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@InvoiceId,@TenantId,CONCAT(N'INV-',REPLACE(@PolicyNumber,N' ',N'')),@AccountId,@PolicyId,@PolicyTermId,@EventId,@EffectiveDate,DATEADD(day,@InsuredDueDays,@EffectiveDate),@InvoiceTotal,@InvoiceTotal,N'USD',N'AgencyBill',N'Open',N'Open',@Now,@CreatedByUserId,0); END;
    INSERT Billing.InvoiceLine(InvoiceLineId,TenantId,InvoiceId,PolicyId,PolicyTermId,SourceEventId,LineOrder,LineTypeCode,ItemCode,Description,Amount,IsCarrierMoney,RevenueRecognitionCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT NEWID(),@TenantId,@InvoiceId,@PolicyId,@PolicyTermId,@EventId,v.LineOrder,v.LineTypeCode,v.ItemCode,v.Description,v.Amount,v.IsCarrierMoney,v.RevenueCode,@Now,@CreatedByUserId,0 FROM (VALUES(1,N'Premium',N'PREMIUM',N'Policy premium',@Premium,CONVERT(bit,1),N'CarrierMoney'),(2,N'AgencyFee',N'AGENCY_FEE',N'Agency fee',@Fees,CONVERT(bit,0),N'AgencyRevenue'),(3,N'Tax',N'PREMIUM_TAX',N'Premium and regulatory taxes',@Taxes,CONVERT(bit,1),N'TaxLiability'))v(LineOrder,LineTypeCode,ItemCode,Description,Amount,IsCarrierMoney,RevenueCode) WHERE v.Amount<>0 AND NOT EXISTS(SELECT 1 FROM Billing.InvoiceLine x WHERE x.TenantId=@TenantId AND x.SourceEventId=@EventId AND x.LineTypeCode=v.LineTypeCode AND x.IsDeleted=0);
    SELECT @ReceivableId=AgencyBillReceivableId FROM Billing.AgencyBillReceivable WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND SourceEventId=@EventId AND IsDeleted=0;
    IF @ReceivableId IS NULL BEGIN SET @ReceivableId=NEWID(); INSERT Billing.AgencyBillReceivable(AgencyBillReceivableId,TenantId,ReceivableNumber,SourceTypeCode,SourceInvoiceId,PolicyId,PolicyTermId,AccountId,CarrierId,BillingTypeCode,CurrencyCode,TransactionDate,DueDate,OriginalAmount,AllocatedAmount,AdjustedAmount,StatusCode,DelinquencyStageCode,Notes,CreatedDateUtc,CreatedByUserId,IsDeleted,SourceEventId) VALUES(@ReceivableId,@TenantId,CONCAT(N'AR-',REPLACE(@PolicyNumber,N' ',N'')),N'PolicyCreated',@InvoiceId,@PolicyId,@PolicyTermId,@AccountId,@CarrierId,N'AgencyBill',N'USD',@EffectiveDate,DATEADD(day,@InsuredDueDays,@EffectiveDate),@InvoiceTotal,0,0,N'Open',N'Current',N'Created from committed policy.',@Now,@CreatedByUserId,0,@EventId); END;
    ;WITH n AS(SELECT 1 i UNION ALL SELECT i+1 FROM n WHERE i<@InstallmentCount)
    INSERT Billing.AgencyBillInstallment(AgencyBillInstallmentId,TenantId,AgencyBillReceivableId,InstallmentNumber,DueDate,InstallmentAmount,AllocatedAmount,StatusCode,GraceDate,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT NEWID(),@TenantId,@ReceivableId,n.i,DATEADD(month,n.i-1,DATEADD(day,@InsuredDueDays,@EffectiveDate)),CASE WHEN n.i=@InstallmentCount THEN @InvoiceTotal-ROUND(@InvoiceTotal/@InstallmentCount,2)*(@InstallmentCount-1) ELSE ROUND(@InvoiceTotal/@InstallmentCount,2) END,0,N'Scheduled',DATEADD(day,@GraceDays,DATEADD(month,n.i-1,DATEADD(day,@InsuredDueDays,@EffectiveDate))),@Now,@CreatedByUserId,0 FROM n WHERE NOT EXISTS(SELECT 1 FROM Billing.AgencyBillInstallment x WHERE x.TenantId=@TenantId AND x.AgencyBillReceivableId=@ReceivableId AND x.InstallmentNumber=n.i AND x.IsDeleted=0) OPTION(MAXRECURSION 120);
    SELECT @CarrierPayableId=CarrierPayableId FROM Accounting.CarrierPayable WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND PolicyTermId=@PolicyTermId AND IsDeleted=0;
    IF @CarrierPayableId IS NULL BEGIN SET @CarrierPayableId=NEWID(); INSERT Accounting.CarrierPayable(CarrierPayableId,TenantId,PolicyId,PolicyTermId,PolicyCreatedEventId,CarrierId,PayableNumber,PremiumAmount,CommissionAmount,FeeAmount,TaxAmount,PayableAmount,PaidAmount,DueDate,CurrencyCode,StatusCode,TrustRequired,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@CarrierPayableId,@TenantId,@PolicyId,@PolicyTermId,@EventId,@CarrierId,CONCAT(N'CP-',REPLACE(@PolicyNumber,N' ',N'')),@Premium,@CommissionAmount,0,0,@CarrierPayable,0,DATEADD(day,@CarrierDueDays,@EffectiveDate),N'USD',N'PendingRemittance',@TrustRequired,@Now,@CreatedByUserId,0); END;
END;

SELECT @ExpectedReceivableId=CommissionExpectedReceivableId FROM Commission.CommissionExpectedReceivable WHERE TenantId=@TenantId AND PolicyCreatedEventId=@EventId AND IsDeleted=0;
IF @ExpectedReceivableId IS NULL BEGIN SET @ExpectedReceivableId=NEWID(); INSERT Commission.CommissionExpectedReceivable(CommissionExpectedReceivableId,TenantId,SourceLedgerId,PolicyId,AccountId,CarrierId,PolicyNumber,AccountName,CarrierName,LineOfBusinessCode,BusinessTypeCode,BillingTypeCode,TransactionTypeCode,EffectiveDate,StatementPeriodStart,StatementPeriodEnd,PremiumAmount,ExpectedRatePct,ExpectedCommissionAmount,ReceivedCommissionAmount,ReconciledCommissionAmount,CurrencyCode,StatusCode,DueDate,CreatedDateUtc,CreatedByUserId,IsDeleted,PolicyCreatedEventId,CommissionTransactionId) VALUES(@ExpectedReceivableId,@TenantId,NULL,@PolicyId,@AccountId,@CarrierId,@PolicyNumber,@AccountName,@CarrierName,@LineOfBusiness,N'NewBusiness',@BillingTypeCode,N'NewBusiness',@EffectiveDate,@EffectiveDate,@ExpirationDate,@Premium,@CommissionRate,@CommissionAmount,0,0,N'USD',N'Expected',CASE WHEN @BillingTypeCode=N'DirectBill' THEN @ExpirationDate ELSE DATEADD(day,@CarrierDueDays,@EffectiveDate) END,@Now,@CreatedByUserId,0,@EventId,@CommissionTransactionId); END;

IF @CommissionAmount>0
BEGIN
    INSERT Accounting.PolicyCommissionSplit(PolicyCommissionSplitId,TenantId,PolicyId,PolicyTermId,PolicyCreatedEventId,CommissionTransactionId,PayeeId,PayeeTypeCode,SplitPercent,SplitAmount,ExpectedDate,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT NEWID(),@TenantId,@PolicyId,@PolicyTermId,@EventId,@CommissionTransactionId,s.PayeeUserId,s.PayeeTypeCode,s.SplitPercent,
           CASE WHEN s.PayeeTypeCode=N'Agency' THEN @CommissionAmount-COALESCE((SELECT SUM(ROUND(@CommissionAmount*x.SplitPercent/100.0,2)) FROM Submissions.PolicyBindCommissionAllocationSnapshot x WHERE x.TenantId=s.TenantId AND x.PolicyBindTransactionId=s.PolicyBindTransactionId AND x.PayeeTypeCode<>N'Agency' AND x.IsDeleted=0),0) ELSE ROUND(@CommissionAmount*s.SplitPercent/100.0,2) END,
           @EffectiveDate,N'PendingEarned',@Now,@CreatedByUserId,0
    FROM Submissions.PolicyBindCommissionAllocationSnapshot s
    WHERE s.TenantId=@TenantId AND s.PolicyBindTransactionId=@PolicyBindTransactionId AND s.IsDeleted=0
      AND NOT EXISTS(SELECT 1 FROM Accounting.PolicyCommissionSplit x WHERE x.TenantId=@TenantId AND x.PolicyId=@PolicyId AND x.PolicyTermId=@PolicyTermId AND x.PayeeTypeCode=s.PayeeTypeCode AND x.IsDeleted=0);

    IF NOT EXISTS(SELECT 1 FROM Submissions.PolicyBindCommissionAllocationSnapshot WHERE TenantId=@TenantId AND PolicyBindTransactionId=@PolicyBindTransactionId AND IsDeleted=0)
    BEGIN
        INSERT Accounting.PolicyCommissionSplit(PolicyCommissionSplitId,TenantId,PolicyId,PolicyTermId,PolicyCreatedEventId,CommissionTransactionId,PayeeId,PayeeTypeCode,SplitPercent,SplitAmount,ExpectedDate,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
        SELECT NEWID(),@TenantId,@PolicyId,@PolicyTermId,@EventId,@CommissionTransactionId,@ProducerId,N'Producer',COALESCE(pbt.CommissionSplitPct,100),ROUND(@CommissionAmount*COALESCE(pbt.CommissionSplitPct,100)/100,2),@EffectiveDate,N'PendingEarned',@Now,@CreatedByUserId,0 FROM Submissions.PolicyBindTransaction pbt WHERE pbt.TenantId=@TenantId AND pbt.PolicyBindTransactionId=@PolicyBindTransactionId AND pbt.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM Accounting.PolicyCommissionSplit x WHERE x.TenantId=@TenantId AND x.PolicyId=@PolicyId AND x.PolicyTermId=@PolicyTermId AND x.PayeeTypeCode=N'Producer' AND x.IsDeleted=0);
        INSERT Accounting.PolicyCommissionSplit(PolicyCommissionSplitId,TenantId,PolicyId,PolicyTermId,PolicyCreatedEventId,CommissionTransactionId,PayeeId,PayeeTypeCode,SplitPercent,SplitAmount,ExpectedDate,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
        SELECT NEWID(),@TenantId,@PolicyId,@PolicyTermId,@EventId,@CommissionTransactionId,NULL,N'Agency',100-COALESCE(pbt.CommissionSplitPct,100),@CommissionAmount-ROUND(@CommissionAmount*COALESCE(pbt.CommissionSplitPct,100)/100,2),@EffectiveDate,N'PendingEarned',@Now,@CreatedByUserId,0 FROM Submissions.PolicyBindTransaction pbt WHERE pbt.TenantId=@TenantId AND pbt.PolicyBindTransactionId=@PolicyBindTransactionId AND pbt.IsDeleted=0 AND 100-COALESCE(pbt.CommissionSplitPct,100)>0 AND NOT EXISTS(SELECT 1 FROM Accounting.PolicyCommissionSplit x WHERE x.TenantId=@TenantId AND x.PolicyId=@PolicyId AND x.PolicyTermId=@PolicyTermId AND x.PayeeTypeCode=N'Agency' AND x.IsDeleted=0);
    END;
END;

SELECT @ArAccount=MAX(CASE WHEN o.OptionCode=N'AccountsReceivable' THEN a.GLAccountId END),@PremiumPayableAccount=MAX(CASE WHEN o.OptionCode=N'PremiumPayable' THEN a.GLAccountId END),@FeeRevenueAccount=MAX(CASE WHEN o.OptionCode=N'AgencyFeeRevenue' THEN a.GLAccountId END),@TaxPayableAccount=MAX(CASE WHEN o.OptionCode=N'PremiumTaxPayable' THEN a.GLAccountId END),@CommissionReceivableAccount=MAX(CASE WHEN o.OptionCode=N'CommissionReceivable' THEN a.GLAccountId END),@CommissionRevenueAccount=MAX(CASE WHEN o.OptionCode=N'CommissionRevenue' THEN a.GLAccountId END)
FROM Accounting.PolicyAccountingOption o JOIN Finance.GLAccount a ON a.TenantId=o.TenantId AND a.AccountCode=o.TextValue AND a.IsDeleted=0 AND a.IsActive=1 WHERE o.TenantId=@TenantId AND o.OptionGroupCode=N'GLAccount' AND o.IsActive=1 AND o.IsDeleted=0;
IF (@BillingTypeCode=N'AgencyBill' AND (@ArAccount IS NULL OR @PremiumPayableAccount IS NULL OR (@Fees<>0 AND @FeeRevenueAccount IS NULL) OR (@Taxes<>0 AND @TaxPayableAccount IS NULL) OR (@CommissionAmount<>0 AND @CommissionRevenueAccount IS NULL))) OR (@BillingTypeCode=N'DirectBill' AND @CommissionAmount<>0 AND (@CommissionReceivableAccount IS NULL OR @CommissionRevenueAccount IS NULL)) THROW 52302,N'Required tenant GL account mapping is missing.',1;
SELECT @JournalEntryId=JournalEntryId FROM Finance.JournalEntry WHERE TenantId=@TenantId AND SourceEventId=@EventId AND EntryTypeCode=N'PolicyCreated' AND IsDeleted=0;
IF @JournalEntryId IS NULL
BEGIN
    SET @JournalEntryId=NEWID();
    INSERT Finance.JournalEntry(JournalEntryId,TenantId,EntryNumber,EntryDate,Description,TotalDebit,TotalCredit,StatusCode,CreatedDateUtc,CreatedByUserId,IsDeleted,PolicyId,PolicyTermId,SourceEventId,EntryTypeCode) VALUES(@JournalEntryId,@TenantId,CONCAT(N'JE-POL-',RIGHT(REPLACE(CONVERT(NVARCHAR(36),@PolicyId),N'-',N''),12)),@EffectiveDate,CONCAT(N'Policy created accounting - ',@PolicyNumber),CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InvoiceTotal ELSE @CommissionAmount END,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InvoiceTotal ELSE @CommissionAmount END,N'Posted',@Now,@CreatedByUserId,0,@PolicyId,@PolicyTermId,@EventId,N'PolicyCreated');
    IF @BillingTypeCode=N'AgencyBill'
    BEGIN
        INSERT Finance.JournalEntryLine(LineId,JournalEntryId,GLAccountId,DebitAmount,CreditAmount,Description,LineOrder,TenantId,PolicyId,AccountingCategoryCode) VALUES(NEWID(),@JournalEntryId,@ArAccount,@InvoiceTotal,0,N'Accounts receivable',1,@TenantId,@PolicyId,N'AccountsReceivable');
        IF @CarrierPayable<>0 INSERT Finance.JournalEntryLine VALUES(NEWID(),@JournalEntryId,@PremiumPayableAccount,0,@CarrierPayable,N'Carrier premium payable',2,@TenantId,@PolicyId,N'PremiumPayable');
        IF @CommissionAmount<>0 INSERT Finance.JournalEntryLine VALUES(NEWID(),@JournalEntryId,@CommissionRevenueAccount,0,@CommissionAmount,N'Agency commission revenue',3,@TenantId,@PolicyId,N'CommissionRevenue');
        IF @Fees<>0 INSERT Finance.JournalEntryLine VALUES(NEWID(),@JournalEntryId,@FeeRevenueAccount,0,@Fees,N'Agency fee revenue',4,@TenantId,@PolicyId,N'AgencyFeeRevenue');
        IF @Taxes<>0 INSERT Finance.JournalEntryLine VALUES(NEWID(),@JournalEntryId,@TaxPayableAccount,0,@Taxes,N'Premium tax payable',5,@TenantId,@PolicyId,N'PremiumTaxPayable');
    END
    ELSE IF @CommissionAmount<>0
    BEGIN
        INSERT Finance.JournalEntryLine VALUES(NEWID(),@JournalEntryId,@CommissionReceivableAccount,@CommissionAmount,0,N'Expected direct-bill commission',1,@TenantId,@PolicyId,N'CommissionReceivable');
        INSERT Finance.JournalEntryLine VALUES(NEWID(),@JournalEntryId,@CommissionRevenueAccount,0,@CommissionAmount,N'Direct-bill commission revenue',2,@TenantId,@PolicyId,N'CommissionRevenue');
    END;
END;

SELECT @StateId=PolicyAccountingStateId FROM Accounting.PolicyAccountingState WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND PolicyTermId=@PolicyTermId AND IsDeleted=0;
IF @StateId IS NULL BEGIN SET @StateId=NEWID(); INSERT Accounting.PolicyAccountingState(PolicyAccountingStateId,TenantId,PolicyId,PolicyTermId,PolicyCreatedEventId,BillingTypeCode,CurrencyCode,PremiumAmount,FeeAmount,TaxAmount,InvoiceAmount,OutstandingBalance,CommissionRatePct,CommissionAmount,CarrierPayableAmount,InstallmentCount,InvoiceId,AgencyBillReceivableId,CarrierPayableId,CommissionExpectedReceivableId,JournalEntryId,StatusCode,SynchronizedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@StateId,@TenantId,@PolicyId,@PolicyTermId,@EventId,@BillingTypeCode,N'USD',@Premium,@Fees,@Taxes,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InvoiceTotal ELSE 0 END,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InvoiceTotal ELSE 0 END,@CommissionRate,@CommissionAmount,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @CarrierPayable ELSE 0 END,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InstallmentCount ELSE 0 END,@InvoiceId,@ReceivableId,@CarrierPayableId,@ExpectedReceivableId,@JournalEntryId,N'Synchronized',@Now,@Now,@CreatedByUserId,0); END
ELSE UPDATE Accounting.PolicyAccountingState SET InvoiceAmount=CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InvoiceTotal ELSE 0 END,OutstandingBalance=CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InvoiceTotal ELSE 0 END,CommissionAmount=@CommissionAmount,CarrierPayableAmount=CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @CarrierPayable ELSE 0 END,InstallmentCount=CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InstallmentCount ELSE 0 END,InvoiceId=@InvoiceId,AgencyBillReceivableId=@ReceivableId,CarrierPayableId=@CarrierPayableId,CommissionExpectedReceivableId=@ExpectedReceivableId,JournalEntryId=@JournalEntryId,StatusCode=N'Synchronized',SynchronizedDateUtc=@Now,ModifiedDateUtc=@Now WHERE PolicyAccountingStateId=@StateId;

INSERT Accounting.PolicyAccountingWorkItem(PolicyAccountingWorkItemId,TenantId,PolicyId,PolicyTermId,PolicyCreatedEventId,WorkItemTypeCode,QueueCode,Title,ReferenceNumber,Amount,PriorityCode,StatusCode,DueDateUtc,AssignedToUserId,DetailUrl,Notes,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@PolicyId,@PolicyTermId,@EventId,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN N'NewPolicyBilling' ELSE N'DirectBillCommission' END,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN N'new-policy-billing' ELSE N'direct-bill' END,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN CONCAT(N'New policy awaiting billing review - ',@PolicyNumber) ELSE CONCAT(N'Direct bill commission expected - ',@PolicyNumber) END,@PolicyNumber,CASE WHEN @BillingTypeCode=N'AgencyBill' THEN @InvoiceTotal ELSE @CommissionAmount END,N'Normal',N'Open',DATEADD(day,1,@Now),@ProducerId,CONCAT(N'/policies/',CONVERT(NVARCHAR(36),@PolicyId)),N'Generated from committed PolicyCreated event.',@Now,@CreatedByUserId,0 WHERE NOT EXISTS(SELECT 1 FROM Accounting.PolicyAccountingWorkItem WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND PolicyTermId=@PolicyTermId AND WorkItemTypeCode=CASE WHEN @BillingTypeCode=N'AgencyBill' THEN N'NewPolicyBilling' ELSE N'DirectBillCommission' END AND IsDeleted=0);
INSERT Accounting.PolicyAccountingAuditEvent(PolicyAccountingAuditEventId,TenantId,PolicyId,PolicyCreatedEventId,EventTypeCode,EventDescription,DataJson,ActorUserId,CreatedDateUtc) SELECT NEWID(),@TenantId,@PolicyId,@EventId,N'AccountingSynchronized',N'Policy accounting subledgers synchronized from PolicyCreated.',JSON_OBJECT(N'billingType':@BillingTypeCode,N'invoiceId':@InvoiceId,N'receivableId':@ReceivableId,N'carrierPayableId':@CarrierPayableId,N'commissionExpectedReceivableId':@ExpectedReceivableId,N'journalEntryId':@JournalEntryId),@CreatedByUserId,@Now WHERE NOT EXISTS(SELECT 1 FROM Accounting.PolicyAccountingAuditEvent WHERE TenantId=@TenantId AND PolicyCreatedEventId=@EventId AND EventTypeCode=N'AccountingSynchronized');
COMMIT;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { EventId = eventId, TenantId = tenantId }, cancellationToken: cancellationToken, commandTimeout: 120));
    }

    public async Task<PolicyAccountingDashboardDto?> GetPolicyDashboardAsync(Guid tenantId, Guid policyId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT s.PolicyId,s.PolicyTermId,bp.PolicyNumber,s.BillingTypeCode,s.StatusCode,s.CurrencyCode,s.PremiumAmount,s.FeeAmount,s.TaxAmount,s.InvoiceAmount,
       COALESCE(r.BalanceAmount,s.OutstandingBalance) OutstandingBalance,s.CommissionRatePct,s.CommissionAmount,
       COALESCE(cp.PayableAmount-cp.PaidAmount,s.CarrierPayableAmount) CarrierPayableAmount,s.InstallmentCount,s.InvoiceId,s.AgencyBillReceivableId,s.CarrierPayableId,s.CommissionExpectedReceivableId,s.JournalEntryId,s.SynchronizedDateUtc
FROM Accounting.PolicyAccountingState s
JOIN Submissions.BoundPolicy bp ON bp.TenantId=s.TenantId AND bp.PolicyId=s.PolicyId AND bp.IsDeleted=0
LEFT JOIN Billing.AgencyBillReceivable r ON r.TenantId=s.TenantId AND r.AgencyBillReceivableId=s.AgencyBillReceivableId AND r.IsDeleted=0
LEFT JOIN Accounting.CarrierPayable cp ON cp.TenantId=s.TenantId AND cp.CarrierPayableId=s.CarrierPayableId AND cp.IsDeleted=0
WHERE s.TenantId=@TenantId AND s.PolicyId=@PolicyId AND s.IsDeleted=0;
SELECT i.InvoiceId,i.InvoiceNumber,i.InvoiceDate,i.DueDate,i.TotalAmount,i.BalanceAmount,i.StatusCode,d.ProposalDeliveryDispatchId DeliveryDispatchId,d.StatusCode DeliveryStatusCode,d.Recipient DeliveryRecipient,d.CompletedDateUtc DeliveredDateUtc,d.ErrorMessage DeliveryErrorMessage FROM Billing.Invoice i OUTER APPLY(SELECT TOP 1 x.ProposalDeliveryDispatchId,x.StatusCode,x.Recipient,x.CompletedDateUtc,x.ErrorMessage FROM Submissions.ProposalDeliveryDispatch x WHERE x.TenantId=i.TenantId AND x.DeliveryCategoryCode=N'Invoice' AND x.EntityName=N'Billing.Invoice' AND x.EntityId=i.InvoiceId AND x.IsDeleted=0 ORDER BY x.CreatedDateUtc DESC)d WHERE i.TenantId=@TenantId AND i.PolicyId=@PolicyId AND i.IsDeleted=0 ORDER BY i.InvoiceDate,i.InvoiceNumber;
SELECT x.AgencyBillInstallmentId,x.InstallmentNumber,x.DueDate,x.InstallmentAmount,x.AllocatedAmount,x.BalanceAmount,x.StatusCode FROM Billing.AgencyBillInstallment x JOIN Billing.AgencyBillReceivable r ON r.TenantId=x.TenantId AND r.AgencyBillReceivableId=x.AgencyBillReceivableId AND r.IsDeleted=0 WHERE x.TenantId=@TenantId AND r.PolicyId=@PolicyId AND x.IsDeleted=0 ORDER BY x.InstallmentNumber;
SELECT PolicyCommissionSplitId,PayeeId,PayeeTypeCode,SplitPercent,SplitAmount,ExpectedDate,StatusCode FROM Accounting.PolicyCommissionSplit WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0 ORDER BY PayeeTypeCode;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, PolicyId = policyId }, cancellationToken: cancellationToken));
        var dashboard = await multi.ReadSingleOrDefaultAsync<PolicyAccountingDashboardDto>();
        if (dashboard is null) return null;
        return dashboard with
        {
            Invoices = (await multi.ReadAsync<PolicyAccountingInvoiceDto>()).AsList(),
            Installments = (await multi.ReadAsync<PolicyAccountingInstallmentDto>()).AsList(),
            CommissionSplits = (await multi.ReadAsync<PolicyAccountingCommissionSplitDto>()).AsList()
        };
    }
}
