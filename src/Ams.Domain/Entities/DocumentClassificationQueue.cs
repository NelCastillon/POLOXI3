using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class DocumentClassificationQueue : AuditableEntity
{
    public Guid DocumentId { get; private set; }
    public string QueueStatus { get; private set; } = "Pending";
    public string ClassificationMethod { get; private set; } = "OCR";
    public decimal? OcrConfidence { get; private set; }
    public string? SuggestedCategory { get; private set; }
    public string? SuggestedDocType { get; private set; }
    public string? ExtractedText { get; private set; }
    public string? ExtractedMetadata { get; private set; }
    public Guid? AssignedToUserId { get; private set; }
    public string? AssignedToName { get; private set; }
    public DateTime? AssignedDateUtc { get; private set; }
    public Guid? ClassifiedByUserId { get; private set; }
    public string? ClassifiedByName { get; private set; }
    public DateTime? ClassifiedDateUtc { get; private set; }
    public string? FinalCategory { get; private set; }
    public string? FinalDocType { get; private set; }
    public string? ClassificationNotes { get; private set; }
    public string Priority { get; private set; } = "Normal";
    public DateTime? DueDateUtc { get; private set; }

    private DocumentClassificationQueue() { }

    public DocumentClassificationQueue(
        Guid tenantId,
        Guid documentId,
        string classificationMethod,
        decimal? ocrConfidence,
        string? suggestedCategory,
        string? suggestedDocType,
        string? extractedText,
        string? extractedMetadata,
        string priority,
        DateTime? dueDateUtc,
        Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        DocumentId = documentId;
        ClassificationMethod = classificationMethod;
        OcrConfidence = ocrConfidence;
        SuggestedCategory = suggestedCategory;
        SuggestedDocType = suggestedDocType;
        ExtractedText = extractedText;
        ExtractedMetadata = extractedMetadata;
        Priority = priority;
        DueDateUtc = dueDateUtc;
        QueueStatus = "Pending";
    }

    public void Assign(Guid assignedToUserId, string? assignedToName, Guid? modifiedByUserId)
    {
        AssignedToUserId = assignedToUserId;
        AssignedToName = assignedToName;
        AssignedDateUtc = DateTime.UtcNow;
        QueueStatus = "InReview";
        MarkModified(modifiedByUserId);
    }

    public void Classify(
        Guid classifiedByUserId,
        string? classifiedByName,
        string finalCategory,
        string finalDocType,
        string? classificationNotes,
        Guid? modifiedByUserId)
    {
        ClassifiedByUserId = classifiedByUserId;
        ClassifiedByName = classifiedByName;
        ClassifiedDateUtc = DateTime.UtcNow;
        FinalCategory = finalCategory;
        FinalDocType = finalDocType;
        ClassificationNotes = classificationNotes;
        QueueStatus = "Classified";
        MarkModified(modifiedByUserId);
    }

    public void MarkFailed(string? reason, Guid? modifiedByUserId)
    {
        QueueStatus = "Failed";
        ClassificationNotes = reason;
        MarkModified(modifiedByUserId);
    }

    public void Skip(string? reason, Guid? modifiedByUserId)
    {
        QueueStatus = "Skipped";
        ClassificationNotes = reason;
        MarkModified(modifiedByUserId);
    }
}
