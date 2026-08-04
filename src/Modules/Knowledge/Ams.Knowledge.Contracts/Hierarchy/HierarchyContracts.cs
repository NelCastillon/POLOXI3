namespace Ams.Knowledge.Contracts.Hierarchy;

public sealed record ConceptHierarchyNode(
    Guid ConceptId,
    string ConceptCode,
    string PreferredLabel,
    int Depth);

public interface IKnowledgeHierarchyService
{
    Task<bool> IsDescendantOfAsync(Guid tenantId, Guid conceptId, Guid ancestorConceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptHierarchyNode>> GetAncestorsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConceptHierarchyNode>> GetDescendantsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default);
}
