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
DECLARE @InvoiceId UNIQUEIDENTIFIER,@AccountId UNIQUEIDENTIFIER,@PolicyNumber NVARCHAR(100),@Amount DECIMAL(18,2)=@TotalAmount;
SELECT @InvoiceId=InvoiceId FROM Billing.Invoice WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND SourceEventId=@AccountingWorkId AND IsDeleted=0;
IF @InvoiceId IS NULL
BEGIN
    SELECT @AccountId=AccountId,@PolicyNumber=PolicyNumber FROM Submissions.BoundPolicy WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND IsDeleted=0;
    IF @AccountId IS NULL THROW 52510,N'The policy account was not found for endorsement accounting.',1;
    SET @InvoiceId=NEWID();
    INSERT Billing.Invoice(InvoiceId,TenantId,InvoiceNumber,AccountId,PolicyId,SourceEventId,InvoiceDate,DueDate,TotalAmount,BalanceAmount,CurrencyCode,BillingTypeCode,StatusCode,InvoiceStatusCodeId,CreatedDateUtc,IsDeleted)
    VALUES(@InvoiceId,@TenantId,CONCAT(CASE WHEN @Amount<0 THEN N'CM-' ELSE N'END-' END,RIGHT(REPLACE(CONVERT(NVARCHAR(36),@AccountingWorkId),N'-',N''),12)),@AccountId,@PolicyId,@AccountingWorkId,CONVERT(date,SYSUTCDATETIME()),DATEADD(day,30,CONVERT(date,SYSUTCDATETIME())),@Amount,@Amount,@CurrencyCode,N'AgencyBill',CASE WHEN @Amount=0 THEN N'Paid' ELSE N'Open' END,CASE WHEN @Amount=0 THEN N'Paid' ELSE N'Open' END,SYSUTCDATETIME(),0);
    INSERT Billing.InvoiceLine(InvoiceLineId,TenantId,InvoiceId,PolicyId,SourceEventId,LineOrder,LineTypeCode,ItemCode,Description,Amount,IsCarrierMoney,RevenueRecognitionCode,CreatedDateUtc,IsDeleted)
    VALUES(NEWID(),@TenantId,@InvoiceId,@PolicyId,@AccountingWorkId,1,N'Endorsement',@WorkTypeCode,CONCAT(N'Policy endorsement ',@WorkTypeCode),@Amount,1,N'OnInvoice',SYSUTCDATETIME(),0);
END;
COMMIT; SELECT @InvoiceId;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(sql, item, cancellationToken: cancellationToken));
    }

    private TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(10, _options.EndorsementPollIntervalSeconds));
    private TimeSpan Lease => TimeSpan.FromMinutes(Math.Clamp(_options.EndorsementClaimLeaseMinutes, 1, 120));
}
