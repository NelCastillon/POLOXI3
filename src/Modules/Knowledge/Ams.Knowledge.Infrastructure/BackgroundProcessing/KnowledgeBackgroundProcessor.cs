using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Ams.Knowledge.Infrastructure.Configuration;

namespace Ams.Knowledge.Infrastructure.BackgroundProcessing;

public interface IKnowledgeBackgroundProcessor
{
    Task<KnowledgeBackgroundBatchResult> ProcessBatchAsync(string leaseOwner, CancellationToken cancellationToken = default);
}

public sealed record KnowledgeBackgroundBatchResult(int ImportsProcessed, int OutboxMessagesProcessed, int Failures);

public sealed class KnowledgeBackgroundProcessor : IKnowledgeBackgroundProcessor
{
    private readonly Persistence.KnowledgeSqlConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;
    private readonly string _importRootPath;

    public KnowledgeBackgroundProcessor(Persistence.KnowledgeSqlConnectionFactory connectionFactory, IMemoryCache cache, IOptions<KnowledgeSqlOptions> options)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
        _importRootPath = options.Value.ImportRootPath;
    }

    public async Task<KnowledgeBackgroundBatchResult> ProcessBatchAsync(string leaseOwner, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        var imports = await ClaimImportsAsync(leaseOwner, settings, cancellationToken);
        var messages = await ClaimMessagesAsync(leaseOwner, settings, cancellationToken);
        var failures = 0;

        foreach (var import in imports)
        {
            try
            {
                await ProcessImportAsync(import, leaseOwner, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures++;
                await FailImportAsync(import.ImportJobId, leaseOwner, exception, settings.MaximumRetries, cancellationToken);
            }
        }

        foreach (var message in messages)
        {
            try
            {
                await DispatchMessageAsync(message, cancellationToken);
                await CompleteMessageAsync(message.SemanticOutboxMessageId, leaseOwner, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures++;
                await FailMessageAsync(message.SemanticOutboxMessageId, leaseOwner, exception, settings.MaximumRetries, cancellationToken);
            }
        }

        return new KnowledgeBackgroundBatchResult(imports.Count, messages.Count, failures);
    }

    private async Task<ProcessorSettings> GetSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
SELECT ConfigurationCode, ConfigurationValue
FROM knowledge.Configuration
WHERE TenantId IS NULL AND IsActive = 1
  AND ConfigurationCode IN (N'WORKER_BATCH_SIZE', N'WORKER_MAX_RETRIES', N'WORKER_LEASE_SECONDS');
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var values = (await connection.QueryAsync<ConfigurationRow>(new CommandDefinition(sql, cancellationToken: cancellationToken)))
            .ToDictionary(row => row.ConfigurationCode, row => row.ConfigurationValue, StringComparer.OrdinalIgnoreCase);
        return new ProcessorSettings(
            ReadInteger(values, "WORKER_BATCH_SIZE"),
            ReadInteger(values, "WORKER_MAX_RETRIES"),
            ReadInteger(values, "WORKER_LEASE_SECONDS"));
    }

    private async Task<IReadOnlyList<ImportWorkItem>> ClaimImportsAsync(string leaseOwner, ProcessorSettings settings, CancellationToken cancellationToken)
    {
        const string sql = """
;WITH candidates AS
(
    SELECT TOP (@BatchSize) *
    FROM knowledge.ImportJob WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE IsDeleted = 0 AND RetryCount < @MaximumRetries
      AND (StatusCode IN (N'QUEUED', N'RETRY') OR (StatusCode = N'PROCESSING' AND LeaseExpiresDateUtc < SYSUTCDATETIME()))
    ORDER BY CreatedDateUtc
)
UPDATE candidates
SET StatusCode = N'PROCESSING', LeaseOwner = @LeaseOwner,
    LeaseExpiresDateUtc = DATEADD(SECOND, @LeaseSeconds, SYSUTCDATETIME()), ModifiedDateUtc = SYSUTCDATETIME()
OUTPUT inserted.ImportJobId, inserted.TenantId, inserted.ImportTypeCode, inserted.StorageReference, inserted.CorrelationId, inserted.RetryCount;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ImportWorkItem>(new CommandDefinition(sql, new { settings.BatchSize, settings.MaximumRetries, settings.LeaseSeconds, LeaseOwner = leaseOwner }, cancellationToken: cancellationToken))).AsList();
    }

    private async Task<IReadOnlyList<OutboxWorkItem>> ClaimMessagesAsync(string leaseOwner, ProcessorSettings settings, CancellationToken cancellationToken)
    {
        const string sql = """
;WITH candidates AS
(
    SELECT TOP (@BatchSize) *
    FROM knowledge.SemanticOutboxMessage WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE RetryCount < @MaximumRetries AND AvailableDateUtc <= SYSUTCDATETIME()
      AND (StatusCode IN (N'PENDING', N'RETRY') OR (StatusCode = N'PROCESSING' AND LeaseExpiresDateUtc < SYSUTCDATETIME()))
    ORDER BY OccurredDateUtc
)
UPDATE candidates
SET StatusCode = N'PROCESSING', LeaseOwner = @LeaseOwner,
    LeaseExpiresDateUtc = DATEADD(SECOND, @LeaseSeconds, SYSUTCDATETIME())
OUTPUT inserted.SemanticOutboxMessageId, inserted.TenantId, inserted.EventTypeCode, inserted.AggregateTypeCode,
       inserted.AggregateId, inserted.PayloadJson, inserted.CorrelationId, inserted.RetryCount;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<OutboxWorkItem>(new CommandDefinition(sql, new { settings.BatchSize, settings.MaximumRetries, settings.LeaseSeconds, LeaseOwner = leaseOwner }, cancellationToken: cancellationToken))).AsList();
    }

    private async Task ProcessImportAsync(ImportWorkItem import, string leaseOwner, CancellationToken cancellationToken)
    {
        var importPath = ResolveImportPath(import.StorageReference);
        if (!File.Exists(importPath))
            throw new FileNotFoundException("The Knowledge import storage reference is not available to this worker.", import.StorageReference);

        await using var stream = File.OpenRead(importPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var records = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().Select(element => element.GetRawText()).ToArray()
            : [document.RootElement.GetRawText()];

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        const string insertSql = """
IF NOT EXISTS (SELECT 1 FROM knowledge.ImportStagingRecord WHERE ImportJobId = @ImportJobId AND RecordNumber = @RecordNumber)
    INSERT INTO knowledge.ImportStagingRecord
    (ImportStagingRecordId, ImportJobId, RecordNumber, SourceJson, NormalizedJson, StatusCode, CreatedDateUtc)
    VALUES (NEWID(), @ImportJobId, @RecordNumber, @SourceJson, @SourceJson, N'VALIDATED', SYSUTCDATETIME());
""";
        for (var index = 0; index < records.Length; index++)
            await connection.ExecuteAsync(new CommandDefinition(insertSql, new { import.ImportJobId, RecordNumber = index + 1, SourceJson = records[index] }, transaction, cancellationToken: cancellationToken));

        const string completeSql = """
UPDATE knowledge.ImportJob
SET StatusCode = N'STAGED', RecordsReceived = @RecordCount, RecordsProcessed = 0,
    RecordsFailed = 0, ErrorMessage = NULL, LeaseOwner = NULL, LeaseExpiresDateUtc = NULL, ModifiedDateUtc = SYSUTCDATETIME()
WHERE ImportJobId = @ImportJobId AND StatusCode = N'PROCESSING' AND LeaseOwner = @LeaseOwner;
""";
        var completed = await connection.ExecuteAsync(new CommandDefinition(completeSql, new { import.ImportJobId, RecordCount = records.Length, LeaseOwner = leaseOwner }, transaction, cancellationToken: cancellationToken));
        if (completed == 0)
            throw new InvalidOperationException("The Knowledge import lease was lost before staging completed.");
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task DispatchMessageAsync(OutboxWorkItem message, CancellationToken cancellationToken)
    {
        if (message.EventTypeCode is "CONCEPT_CREATED" or "CONCEPT_REVISED" or "RELATIONSHIP_ADDED")
            await RebuildHierarchyAsync(cancellationToken);

        if (message.EventTypeCode is "MAPPING_CREATED" or "MAPPING_REVIEWED" or "CONCEPT_CREATED" or "CONCEPT_REVISED" or "LABEL_ADDED")
        {
            _cache.Remove($"knowledge:resolution-policy:{message.TenantId}");
            _cache.Remove($"knowledge:validation-policy:{message.TenantId}");
        }

        JsonDocument.Parse(message.PayloadJson).Dispose();
    }

    private async Task RebuildHierarchyAsync(CancellationToken cancellationToken)
    {
        const string sql = """
DECLARE @LockResult INT;
EXEC @LockResult = sys.sp_getapplock @Resource = N'Ams.Knowledge.HierarchyRebuild', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 30000;
IF @LockResult < 0 THROW 51010, 'Could not acquire the Knowledge hierarchy rebuild lock.', 1;

DELETE FROM knowledge.ConceptHierarchyClosure;
;WITH Edges AS
(
    SELECT ParentConceptId AS ParentId, KnowledgeConceptId AS ChildId
    FROM knowledge.KnowledgeConcept
    WHERE ParentConceptId IS NOT NULL AND IsDeleted = 0 AND StatusCode IN (N'APPROVED', N'PUBLISHED')
    UNION
    SELECT CASE WHEN predicate.SubjectIsChild = 1 THEN relation.ObjectConceptId ELSE relation.SubjectConceptId END,
           CASE WHEN predicate.SubjectIsChild = 1 THEN relation.SubjectConceptId ELSE relation.ObjectConceptId END
    FROM knowledge.ConceptRelationship relation
    INNER JOIN knowledge.RelationshipPredicate predicate ON predicate.PredicateCode = relation.PredicateCode
    WHERE predicate.IsHierarchical = 1 AND predicate.IsActive = 1 AND relation.IsDeleted = 0
      AND relation.StatusCode IN (N'APPROVED', N'PUBLISHED')
), Hierarchy AS
(
    SELECT KnowledgeConceptId AS AncestorId, KnowledgeConceptId AS DescendantId, 0 AS Depth
    FROM knowledge.KnowledgeConcept WHERE IsDeleted = 0 AND StatusCode IN (N'APPROVED', N'PUBLISHED')
    UNION ALL
    SELECT hierarchy.AncestorId, edge.ChildId, hierarchy.Depth + 1
    FROM Hierarchy hierarchy INNER JOIN Edges edge ON edge.ParentId = hierarchy.DescendantId
)
INSERT INTO knowledge.ConceptHierarchyClosure(AncestorConceptId, DescendantConceptId, Depth, RefreshedDateUtc)
SELECT AncestorId, DescendantId, MIN(Depth), SYSUTCDATETIME() FROM Hierarchy GROUP BY AncestorId, DescendantId
OPTION (MAXRECURSION 32767);
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private Task CompleteMessageAsync(Guid id, string leaseOwner, CancellationToken cancellationToken)
        => ExecuteAsync("UPDATE knowledge.SemanticOutboxMessage SET StatusCode = N'COMPLETED', ProcessedDateUtc = SYSUTCDATETIME(), LeaseOwner = NULL, LeaseExpiresDateUtc = NULL, LastError = NULL WHERE SemanticOutboxMessageId = @Id AND StatusCode = N'PROCESSING' AND LeaseOwner = @LeaseOwner;", new { Id = id, LeaseOwner = leaseOwner }, cancellationToken);

    private Task FailImportAsync(Guid id, string leaseOwner, Exception exception, int maximumRetries, CancellationToken cancellationToken)
        => ExecuteAsync("UPDATE knowledge.ImportJob SET RetryCount = RetryCount + 1, StatusCode = CASE WHEN RetryCount + 1 >= @MaximumRetries THEN N'FAILED' ELSE N'RETRY' END, ErrorMessage = @Error, LeaseOwner = NULL, LeaseExpiresDateUtc = NULL, ModifiedDateUtc = SYSUTCDATETIME() WHERE ImportJobId = @Id AND StatusCode = N'PROCESSING' AND LeaseOwner = @LeaseOwner;", new { Id = id, LeaseOwner = leaseOwner, MaximumRetries = maximumRetries, Error = Limit(exception.Message) }, cancellationToken);

    private Task FailMessageAsync(Guid id, string leaseOwner, Exception exception, int maximumRetries, CancellationToken cancellationToken)
        => ExecuteAsync("UPDATE knowledge.SemanticOutboxMessage SET RetryCount = RetryCount + 1, StatusCode = CASE WHEN RetryCount + 1 >= @MaximumRetries THEN N'DEAD_LETTER' ELSE N'RETRY' END, AvailableDateUtc = DATEADD(SECOND, POWER(2, CASE WHEN RetryCount > 8 THEN 8 ELSE RetryCount END) * 30, SYSUTCDATETIME()), LastError = @Error, DeadLetterDateUtc = CASE WHEN RetryCount + 1 >= @MaximumRetries THEN SYSUTCDATETIME() ELSE NULL END, LeaseOwner = NULL, LeaseExpiresDateUtc = NULL WHERE SemanticOutboxMessageId = @Id AND StatusCode = N'PROCESSING' AND LeaseOwner = @LeaseOwner;", new { Id = id, LeaseOwner = leaseOwner, MaximumRetries = maximumRetries, Error = Limit(exception.Message) }, cancellationToken);

    private async Task ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static int ReadInteger(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new InvalidOperationException($"Knowledge configuration '{key}' must contain a positive integer.");

    private static string Limit(string value) => value.Length <= 4000 ? value : value[..4000];

    private string ResolveImportPath(string storageReference)
    {
        if (string.IsNullOrWhiteSpace(_importRootPath))
            throw new InvalidOperationException("Knowledge:ImportRootPath must be configured before imports can be processed.");
        if (string.IsNullOrWhiteSpace(storageReference) || Path.IsPathRooted(storageReference))
            throw new InvalidOperationException("Knowledge import storage references must be relative paths.");

        var root = Path.GetFullPath(_importRootPath);
        var candidate = Path.GetFullPath(Path.Combine(root, storageReference));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Knowledge import storage reference is outside the configured import root.");
        return candidate;
    }

    private sealed record ConfigurationRow(string ConfigurationCode, string ConfigurationValue);
    private sealed record ProcessorSettings(int BatchSize, int MaximumRetries, int LeaseSeconds);
    private sealed record ImportWorkItem(Guid ImportJobId, Guid TenantId, string ImportTypeCode, string StorageReference, string CorrelationId, int RetryCount);
    private sealed record OutboxWorkItem(Guid SemanticOutboxMessageId, Guid TenantId, string EventTypeCode, string AggregateTypeCode, Guid AggregateId, string PayloadJson, string CorrelationId, int RetryCount);
}
