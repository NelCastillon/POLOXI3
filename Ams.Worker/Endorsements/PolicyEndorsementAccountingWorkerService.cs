using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.PolicyEndorsements;
using Ams.Worker.Automation;
using Dapper;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Endorsements;

public sealed class PolicyEndorsementAccountingWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<PolicyEndorsementAccountingWorkerService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public PolicyEndorsementAccountingWorkerService(IServiceProvider serviceProvider, IOptions<WorkerOptions> options, ILogger<PolicyEndorsementAccountingWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IPolicyEndorsementRepository>();
                var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
                var items = await repository.ClaimAccountingWorkAsync(_workerId, Math.Clamp(_options.MaxEndorsementWorkItemsPerPoll, 1, 100), Lease, stoppingToken);
                foreach (var item in items)
                    await ProcessAsync(repository, connectionFactory, item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Endorsement accounting polling cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessAsync(IPolicyEndorsementRepository repository, ISqlConnectionFactory connectionFactory, PolicyEndorsementAccountingWorkItem item, CancellationToken cancellationToken)
    {
        try
        {
            var invoiceId = await CreateInvoiceAsync(connectionFactory, item, cancellationToken);
            await repository.CompleteAccountingWorkAsync(item.AccountingWorkId, _workerId, new("Billing.Invoice", invoiceId), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await repository.FailAccountingWorkAsync(item.AccountingWorkId, _workerId, new(exception.Message, true), cancellationToken);
            _logger.LogError(exception, "Endorsement accounting work {WorkId} failed.", item.AccountingWorkId);
        }
    }

    private static async Task<Guid> CreateInvoiceAsync(ISqlConnectionFactory connectionFactory, PolicyEndorsementAccountingWorkItem item, CancellationToken cancellationToken)
    {
        const string sql = """
SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE; BEGIN TRAN;
DECLARE @InvoiceId UNIQUEIDENTIFIER,@AccountId UNIQUEIDENTIFIER,@PolicyNumber NVARCHAR(100),@BillingTypeCode NVARCHAR(50),@DueDays INT,@Amount DECIMAL(18,2)=@TotalAmount;
SELECT @InvoiceId=InvoiceId FROM Billing.Invoice WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND SourceEventId=@AccountingWorkId AND IsDeleted=0;
IF @WorkTypeCode=N'CommissionOnly' SET @InvoiceId=@EndorsementId;
IF @InvoiceId IS NULL AND @WorkTypeCode<>N'CommissionOnly'
BEGIN
    SELECT @AccountId=policy.AccountId,@PolicyNumber=policy.PolicyNumber,@BillingTypeCode=accounting.BillingTypeCode
    FROM Submissions.BoundPolicy policy
    LEFT JOIN Accounting.PolicyAccountingState accounting ON accounting.TenantId=policy.TenantId AND accounting.PolicyId=policy.PolicyId AND accounting.IsDeleted=0
    WHERE policy.TenantId=@TenantId AND policy.PolicyId=@PolicyId AND policy.IsDeleted=0;
    IF @AccountId IS NULL THROW 52510,N'The policy account was not found for endorsement accounting.',1;
    SELECT TOP 1 @DueDays=CONVERT(INT,NumericValue) FROM Accounting.PolicyAccountingOption WHERE TenantId=@TenantId AND OptionGroupCode=N'PaymentTerms' AND OptionCode=N'InsuredDueDays' AND IsActive=1 AND IsDeleted=0 ORDER BY IsDefault DESC,SortOrder;
    SET @BillingTypeCode=COALESCE(NULLIF(@BillingTypeCode,N''),N'AgencyBill');
    SET @DueDays=COALESCE(@DueDays,30);
    SET @InvoiceId=NEWID();
    INSERT Billing.Invoice(InvoiceId,TenantId,InvoiceNumber,AccountId,PolicyId,SourceEventId,InvoiceDate,DueDate,TotalAmount,BalanceAmount,CurrencyCode,BillingTypeCode,StatusCode,InvoiceStatusCodeId,CreatedDateUtc,IsDeleted)
    VALUES(@InvoiceId,@TenantId,CONCAT(CASE WHEN @Amount<0 THEN N'CM-' ELSE N'END-' END,RIGHT(REPLACE(CONVERT(NVARCHAR(36),@AccountingWorkId),N'-',N''),12)),@AccountId,@PolicyId,@AccountingWorkId,CONVERT(date,SYSUTCDATETIME()),DATEADD(day,@DueDays,CONVERT(date,SYSUTCDATETIME())),@Amount,@Amount,@CurrencyCode,@BillingTypeCode,CASE WHEN @Amount=0 THEN N'Paid' ELSE N'Open' END,CASE WHEN @Amount=0 THEN N'Paid' ELSE N'Open' END,SYSUTCDATETIME(),0);
    INSERT Billing.InvoiceLine(InvoiceLineId,TenantId,InvoiceId,PolicyId,SourceEventId,LineOrder,LineTypeCode,ItemCode,Description,Amount,IsCarrierMoney,RevenueRecognitionCode,CreatedDateUtc,IsDeleted)
    VALUES(NEWID(),@TenantId,@InvoiceId,@PolicyId,@AccountingWorkId,1,N'Endorsement',@WorkTypeCode,CONCAT(N'Policy endorsement ',@WorkTypeCode),@Amount,1,N'OnInvoice',SYSUTCDATETIME(),0);
END;

IF EXISTS
(
    SELECT 1 FROM Policy.PolicyEndorsement
    WHERE TenantId=@TenantId AND EndorsementId=@EndorsementId AND CommissionImpactCode<>N'NoCommissionImpact' AND IsDeleted=0
)
BEGIN
    INSERT Commission.CommissionTransaction
        (TransactionId,TenantId,PayeeId,CommissionPlanId,SourceEntityName,SourceEntityId,TransactionDate,GrossAmount,CommissionRate,CommissionAmount,StatusCode,PayoutId,CreatedDateUtc,IsDeleted)
    SELECT NEWID(),source.TenantId,source.PayeeId,source.CommissionPlanId,N'PolicyEndorsement',@EndorsementId,CONVERT(date,SYSUTCDATETIME()),@PremiumAmount,source.CommissionRate,
           ROUND(@PremiumAmount*source.CommissionRate/100.0,2),N'Pending',NULL,SYSUTCDATETIME(),0
    FROM Commission.CommissionTransaction source
    WHERE source.TenantId=@TenantId AND source.SourceEntityName=N'Policy' AND source.SourceEntityId=@PolicyId AND source.IsDeleted=0
      AND NOT EXISTS(SELECT 1 FROM Commission.CommissionTransaction existing WHERE existing.TenantId=source.TenantId AND existing.SourceEntityName=N'PolicyEndorsement' AND existing.SourceEntityId=@EndorsementId AND existing.PayeeId=source.PayeeId AND existing.IsDeleted=0);

    INSERT Commission.CommissionCalculationResult
        (CalculationResultId,TenantId,TransactionId,PayeeId,CommissionPlanId,BaseAmount,RatePct,SplitPct,CalculatedAmount,AdjustedAmount,StatusCode,CalculatedDateUtc,CreatedDateUtc,IsDeleted)
    SELECT NEWID(),transactionRow.TenantId,transactionRow.TransactionId,transactionRow.PayeeId,transactionRow.CommissionPlanId,transactionRow.GrossAmount,transactionRow.CommissionRate,sourceResult.SplitPct,transactionRow.CommissionAmount,NULL,N'Calculated',SYSUTCDATETIME(),SYSUTCDATETIME(),0
    FROM Commission.CommissionTransaction transactionRow
    JOIN Commission.CommissionTransaction sourceTransaction ON sourceTransaction.TenantId=transactionRow.TenantId AND sourceTransaction.SourceEntityName=N'Policy' AND sourceTransaction.SourceEntityId=@PolicyId AND sourceTransaction.PayeeId=transactionRow.PayeeId AND sourceTransaction.CommissionPlanId=transactionRow.CommissionPlanId AND sourceTransaction.IsDeleted=0
    JOIN Commission.CommissionCalculationResult sourceResult ON sourceResult.TenantId=sourceTransaction.TenantId AND sourceResult.TransactionId=sourceTransaction.TransactionId AND sourceResult.PayeeId=sourceTransaction.PayeeId AND sourceResult.IsDeleted=0
    WHERE transactionRow.TenantId=@TenantId AND transactionRow.SourceEntityName=N'PolicyEndorsement' AND transactionRow.SourceEntityId=@EndorsementId AND transactionRow.IsDeleted=0
      AND NOT EXISTS(SELECT 1 FROM Commission.CommissionCalculationResult existing WHERE existing.TenantId=transactionRow.TenantId AND existing.TransactionId=transactionRow.TransactionId AND existing.PayeeId=transactionRow.PayeeId AND existing.IsDeleted=0);

    INSERT Commission.CommissionAccrualEntry
        (AccrualEntryId,TenantId,TransactionId,AccrualDate,AccruedAmount,StatusCode,CreatedDateUtc,IsDeleted)
    SELECT NEWID(),transactionRow.TenantId,transactionRow.TransactionId,CONVERT(date,SYSUTCDATETIME()),transactionRow.CommissionAmount,N'Pending',SYSUTCDATETIME(),0
    FROM Commission.CommissionTransaction transactionRow
    WHERE transactionRow.TenantId=@TenantId AND transactionRow.SourceEntityName=N'PolicyEndorsement' AND transactionRow.SourceEntityId=@EndorsementId AND transactionRow.IsDeleted=0
      AND NOT EXISTS(SELECT 1 FROM Commission.CommissionAccrualEntry existing WHERE existing.TenantId=transactionRow.TenantId AND existing.TransactionId=transactionRow.TransactionId AND existing.IsDeleted=0);
END;
COMMIT; SELECT @InvoiceId;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(sql, item, cancellationToken: cancellationToken));
    }

    private TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(10, _options.EndorsementPollIntervalSeconds));
    private TimeSpan Lease => TimeSpan.FromMinutes(Math.Clamp(_options.EndorsementClaimLeaseMinutes, 1, 120));
}
