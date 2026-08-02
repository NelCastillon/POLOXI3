using System.Text;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
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
                var connectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();
                var items = await repository.ClaimDocumentWorkAsync(_workerId, Math.Clamp(_options.MaxEndorsementWorkItemsPerPoll, 1, 100), Lease, stoppingToken);
                foreach (var item in items)
                    await ProcessAsync(scope.ServiceProvider, repository, connectionFactory, item, stoppingToken);
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

    private async Task ProcessAsync(IServiceProvider serviceProvider, IPolicyEndorsementRepository repository, ISqlConnectionFactory connectionFactory, PolicyEndorsementDocumentWorkItem item, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await FindDocumentAsync(connectionFactory, item, cancellationToken);
            var documentId = existing;
            if (!documentId.HasValue)
            {
                var storage = serviceProvider.GetRequiredService<IDocumentStorageService>();
                documentId = await CreateDocumentAsync(connectionFactory, storage, item, cancellationToken);
            }
            await repository.CompleteDocumentWorkAsync(item.DocumentWorkId, _workerId, new(documentId.Value), cancellationToken);
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
        const string sql = "SELECT TOP 1 DocumentId FROM DMS.Document WHERE TenantId=@TenantId AND EntityName=N'PolicyEndorsementDocumentWork' AND EntityId=@DocumentWorkId AND IsDeleted=0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(sql, item, cancellationToken: cancellationToken));
    }

    private static async Task<Guid> CreateDocumentAsync(ISqlConnectionFactory connectionFactory, IDocumentStorageService storage, PolicyEndorsementDocumentWorkItem item, CancellationToken cancellationToken)
    {
        var documentId = Guid.NewGuid();
        var fileName = $"endorsement-{item.EndorsementId:N}-{item.DocumentTypeCode}.txt";
        var content = Encoding.UTF8.GetBytes($"Endorsement: {item.EndorsementId}\nPolicy: {item.PolicyId}\nDocument type: {item.DocumentTypeCode}\nGenerated UTC: {DateTime.UtcNow:O}\n");
        await using var stream = new MemoryStream(content, writable: false);
        var upload = await storage.UploadAsync(new DocumentStorageUploadRequest { TenantId = item.TenantId, FileName = fileName, ContentType = "text/plain", Content = stream }, cancellationToken);

        const string sql = """
SET XACT_ABORT ON; BEGIN TRAN;
IF NOT EXISTS(SELECT 1 FROM DMS.Document WHERE TenantId=@TenantId AND EntityName=N'PolicyEndorsementDocumentWork' AND EntityId=@DocumentWorkId AND IsDeleted=0)
BEGIN
    INSERT DMS.Document(DocumentId,TenantId,DocumentTypeCode,CategoryCode,EntityName,EntityId,FileName,StoragePath,ContentType,FileSizeBytes,VersionNumber,StatusCode,Description,Tags,UploadedByName,CreatedDateUtc,IsDeleted)
    VALUES(@DocumentId,@TenantId,@DocumentTypeCode,N'Policy',N'PolicyEndorsementDocumentWork',@DocumentWorkId,@FileName,@StoragePath,@ContentType,@FileSizeBytes,1,N'Active',CONCAT(N'Generated endorsement ',@DocumentTypeCode),N'endorsement,generated',N'AMS Worker',SYSUTCDATETIME(),0);
    INSERT Policy.PolicyDocumentLink(PolicyDocumentLinkId,TenantId,PolicyId,DocumentId,DocumentRoleCode,SourceEntityName,SourceEntityId,CreatedDateUtc,IsDeleted)
    VALUES(NEWID(),@TenantId,@PolicyId,@DocumentId,@DocumentTypeCode,N'PolicyEndorsement',@EndorsementId,SYSUTCDATETIME(),0);
END
ELSE SELECT @DocumentId=DocumentId FROM DMS.Document WHERE TenantId=@TenantId AND EntityName=N'PolicyEndorsementDocumentWork' AND EntityId=@DocumentWorkId AND IsDeleted=0;
COMMIT; SELECT @DocumentId;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<Guid>(new CommandDefinition(sql, new { item.TenantId, item.DocumentWorkId, item.DocumentTypeCode, item.PolicyId, item.EndorsementId, DocumentId = documentId, FileName = fileName, upload.StoragePath, upload.ContentType, upload.FileSizeBytes }, cancellationToken: cancellationToken));
    }

    private TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(10, _options.EndorsementPollIntervalSeconds));
    private TimeSpan Lease => TimeSpan.FromMinutes(Math.Clamp(_options.EndorsementClaimLeaseMinutes, 1, 120));
}
