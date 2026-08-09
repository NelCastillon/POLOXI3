using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.PolicyEndorsements;
using Ams.Worker.Automation;
using Dapper;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Endorsements;

public sealed class PolicyEndorsementDocumentWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<PolicyEndorsementDocumentWorkerService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public PolicyEndorsementDocumentWorkerService(IServiceProvider serviceProvider, IOptions<WorkerOptions> options, ILogger<PolicyEndorsementDocumentWorkerService> logger)
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
                var items = await repository.ClaimDocumentWorkAsync(_workerId, Math.Clamp(_options.MaxEndorsementWorkItemsPerPoll, 1, 100), Lease, stoppingToken);
                foreach (var item in items)
                    await ProcessAsync(repository, scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>(), item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Endorsement document polling cycle failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessAsync(IPolicyEndorsementRepository repository, ISqlConnectionFactory connectionFactory, PolicyEndorsementDocumentWorkItem item, CancellationToken cancellationToken)
    {
        try
        {
            var documentId = await FindDocumentAsync(connectionFactory, item, cancellationToken)
                ?? throw new InvalidOperationException($"A real '{item.DocumentTypeCode}' document must be generated, received, or uploaded and linked before this work item can complete.");
            await repository.CompleteDocumentWorkAsync(item.DocumentWorkId, _workerId, new(documentId), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await repository.FailDocumentWorkAsync(item.DocumentWorkId, _workerId, new(exception.Message, true), cancellationToken);
            _logger.LogError(exception, "Endorsement document work {WorkId} failed.", item.DocumentWorkId);
        }
    }

    private static async Task<Guid?> FindDocumentAsync(ISqlConnectionFactory connectionFactory, PolicyEndorsementDocumentWorkItem item, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TOP 1 DocumentId FROM Policy.PolicyDocumentLink WHERE TenantId=@TenantId AND PolicyId=@PolicyId AND SourceEntityName=N'PolicyEndorsement' AND SourceEntityId=@EndorsementId AND DocumentRoleCode=@DocumentTypeCode AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, item, cancellationToken: cancellationToken));
    }

    private TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(10, _options.EndorsementPollIntervalSeconds));
    private TimeSpan Lease => TimeSpan.FromMinutes(Math.Clamp(_options.EndorsementClaimLeaseMinutes, 1, 120));
}
