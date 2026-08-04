using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Concepts;

public static class ConceptHierarchy
{
    public static void EnsureCanAddParent(
        Guid childConceptId,
        Guid parentConceptId,
        IReadOnlyCollection<(Guid ParentConceptId, Guid ChildConceptId)> approvedEdges)
    {
        if (childConceptId == Guid.Empty || parentConceptId == Guid.Empty)
            throw new KnowledgeDomainException("Hierarchy concept identifiers are required.");
        if (childConceptId == parentConceptId)
            throw new KnowledgeDomainException("A concept cannot be its own parent.");

        var childrenByParent = approvedEdges
            .GroupBy(edge => edge.ParentConceptId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ChildConceptId).ToArray());
        var pending = new Stack<Guid>();
        var visited = new HashSet<Guid>();
        pending.Push(childConceptId);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
                continue;
            if (current == parentConceptId)
                throw new KnowledgeDomainException("The hierarchy change would create a cycle.");
            if (childrenByParent.TryGetValue(current, out var children))
            {
                foreach (var child in children)
                    pending.Push(child);
            }
        }
    }
}
