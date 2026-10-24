using System.Security.Cryptography;
using System.Text;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Required-factor frontier — the DETERMINISTIC gap set that complements the scored proposition
// frontier. A legal decision is not resolved just because every extracted proposition is resolved:
// the domain pack declares the REQUIRED legal elements (IsRequiredCoverage concepts — duty, breach,
// causation, damages, notice, …) and any required element with NO admitted evidence coverage is a
// first-class resolution target even when no fact proposition exists for it yet. A required element
// with zero coverage is the STRONGEST possible resolution gap.
//
// This is pure projection over existing state (resolved domain pack + matter evidence graph). It
// makes NO scoring call and invents NO legal element names — every factor comes from the DB-backed
// domain pack concepts. Both What-To-Resolve-Next and Next-Best-Action consume it so the two
// surfaces stay consistent.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// A required domain concept (legal element) that has no admitted evidence coverage on the matter.
public sealed record RequiredFactorGap(
    Guid FactorId,            // deterministic & stable per pack+concept (so reloads and both services agree)
    string ConceptCode,
    string DimensionCode,
    string Name,
    string? Description,
    decimal DecisionRelevance); // deterministic leverage weight in [0,1]

public static class RequiredFactorFrontier
{
    // Project the uncovered REQUIRED legal elements from the resolved pack + evidence graph.
    public static IReadOnlyList<RequiredFactorGap> Compute(
        ResolvedDomainPack pack,
        LegalMatterEvidenceGraphDto graph)
    {
        // Concept codes / dimensions that already carry admitted (non-invalidated) evidence coverage.
        var coveredConceptCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var coveredDimensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in graph.Evidence ?? [])
        {
            if (string.Equals(e.EvidenceStateCode, LegalEvidenceStates.Invalidated, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(e.DomainConceptCode))
                coveredConceptCodes.Add(e.DomainConceptCode!);
            if (!string.IsNullOrWhiteSpace(e.DimensionCode))
                coveredDimensions.Add(e.DimensionCode);
        }

        var gaps = new List<RequiredFactorGap>();
        foreach (var c in pack.Concepts
                     .Where(c => c.IsRequiredCoverage)
                     .OrderBy(c => c.SortOrder))
        {
            var covered = coveredConceptCodes.Contains(c.ConceptCode)
                || (!string.IsNullOrWhiteSpace(c.DimensionCode) && coveredDimensions.Contains(c.DimensionCode));
            if (covered)
                continue;

            gaps.Add(new RequiredFactorGap(
                DeterministicFactorId(pack.PackCode, c.ConceptCode),
                c.ConceptCode,
                c.DimensionCode,
                string.IsNullOrWhiteSpace(c.Name) ? c.ConceptCode : c.Name,
                c.Description,
                // A required legal element with no admitted coverage is maximally decision-relevant.
                1.0m));
        }

        return gaps;
    }

    // Stable, collision-resistant GUID derived from pack+concept so both services (and page reloads)
    // agree on the same id, and the LLM can echo it back through the NBA eligibility gate.
    public static Guid DeterministicFactorId(string packCode, string conceptCode)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"FACTOR|{packCode}|{conceptCode}"));
        return new Guid(bytes);
    }
}
