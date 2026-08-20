namespace Ams.Knowledge.Contracts.Events;

public interface IKnowledgeIntegrationEvent
{
    Guid EventId { get; }
    Guid TenantId { get; }
    string CorrelationId { get; }
    DateTime OccurredUtc { get; }
}

public sealed record PolicyIssuedIntegrationEvent(Guid EventId, Guid TenantId, string CorrelationId, DateTime OccurredUtc, Guid PolicyId, Guid AccountId, Guid CarrierId, Guid? LineOfBusinessConceptId) : IKnowledgeIntegrationEvent;
public sealed record DocumentUploadedIntegrationEvent(Guid EventId, Guid TenantId, string CorrelationId, DateTime OccurredUtc, Guid DocumentId, Guid? ParentEntityId, string FileName) : IKnowledgeIntegrationEvent;
public sealed record SubmissionCreatedIntegrationEvent(Guid EventId, Guid TenantId, string CorrelationId, DateTime OccurredUtc, Guid SubmissionId, Guid AccountId, Guid? LineOfBusinessConceptId) : IKnowledgeIntegrationEvent;
public sealed record CarrierResponseReceivedIntegrationEvent(Guid EventId, Guid TenantId, string CorrelationId, DateTime OccurredUtc, Guid CarrierId, Guid? CarrierProductId, string SourceReference) : IKnowledgeIntegrationEvent;
