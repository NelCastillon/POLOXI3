using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Contracts.Hierarchy;

namespace Ams.Knowledge.Application.Services;

public sealed class KnowledgeHierarchyService : IKnowledgeHierarchyService
{
    private readonly IKnowledgeHierarchyRepository _repository;

    public KnowledgeHierarchyService(IKnowledgeHierarchyRepository repository) => _repository = repository;

    public Task<bool> IsDescendantOfAsync(Guid tenantId, Guid conceptId, Guid ancestorConceptId, CancellationToken cancellationToken = default)
        => _repository.IsDescendantOfAsync(tenantId, conceptId, ancestorConceptId, cancellationToken);

    public Task<IReadOnlyCollection<ConceptHierarchyNode>> GetAncestorsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default)
        => _repository.GetAncestorsAsync(tenantId, conceptId, cancellationToken);

    public Task<IReadOnlyCollection<ConceptHierarchyNode>> GetDescendantsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default)
        => _repository.GetDescendantsAsync(tenantId, conceptId, cancellationToken);
}
