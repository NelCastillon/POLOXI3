using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Governance;

public sealed class KnowledgeImportJob : KnowledgeRecord
{
    public KnowledgeImportJob(Guid id, Guid tenantId, string importTypeCode, string sourceFileName, string storageReference, string statusCode, string correlationId, Guid createdByUserId, DateTime createdUtc)
        : base(id, tenantId, false, createdByUserId, createdUtc)
    {
        ImportTypeCode = KnowledgeGuard.Code(importTypeCode, "ImportTypeCode", 50);
        SourceFileName = KnowledgeGuard.Required(sourceFileName, "SourceFileName", 260);
        StorageReference = KnowledgeGuard.Required(storageReference, "StorageReference", 1000);
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
        CorrelationId = KnowledgeGuard.Required(correlationId, "CorrelationId", 120);
    }

    public string ImportTypeCode { get; }
    public string SourceFileName { get; }
    public string StorageReference { get; }
    public string StatusCode { get; private set; }
    public string CorrelationId { get; }
    public int RecordsReceived { get; private set; }
    public int RecordsProcessed { get; private set; }
    public int RecordsFailed { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void RecordProgress(string statusCode, int received, int processed, int failed, string? errorMessage, Guid actorUserId, DateTime modifiedUtc)
    {
        if (received < 0 || processed < 0 || failed < 0 || processed + failed > received)
            throw new KnowledgeDomainException("Import record counts are invalid.");
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
        RecordsReceived = received;
        RecordsProcessed = processed;
        RecordsFailed = failed;
        ErrorMessage = errorMessage;
        MarkModified(actorUserId, modifiedUtc);
    }
}
