using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Mappings;

public sealed class MappingReview : KnowledgeRecord
{
    public MappingReview(Guid id, Guid externalConceptMappingId, string statusCode, string? recommendationJson, Guid tenantId, Guid createdByUserId, DateTime createdUtc)
        : base(id, tenantId, false, createdByUserId, createdUtc)
    {
        if (externalConceptMappingId == Guid.Empty)
            throw new KnowledgeDomainException("ExternalConceptMappingId is required.");
        ExternalConceptMappingId = externalConceptMappingId;
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
        RecommendationJson = recommendationJson;
    }

    public Guid ExternalConceptMappingId { get; }
    public string StatusCode { get; private set; }
    public string? RecommendationJson { get; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedUtc { get; private set; }
    public string? ReviewReason { get; private set; }

    public void Complete(string statusCode, string reason, Guid reviewerUserId, DateTime reviewedUtc)
    {
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
        ReviewReason = KnowledgeGuard.Required(reason, "ReviewReason", 1000);
        ReviewedByUserId = reviewerUserId;
        ReviewedUtc = reviewedUtc;
        MarkModified(reviewerUserId, reviewedUtc);
    }
}
