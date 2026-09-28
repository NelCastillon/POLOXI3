namespace Legal.Application.Features.Intelligence.Decision;

// ──────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI APR + MECE + Light Evidence Graph v4.0 — the LIGHT EVIDENCE GRAPH (third pillar).
//
// A pure, in-memory, string-id graph that binds propositions to the evidence that supports/conflicts
// with them and to the derivations between propositions. It is intentionally SEPARATE from the heavy,
// persistence-backed Core DecisionGraph (Guid ids, materiality-weighted support recompute, DB rows):
// this layer is deterministic, side-effect-free, and its ONLY job is to derive the per-node signals the
// §9 Proposition Integrity Gate consumes — HasSupportingEvidence, HasSourceSpan, EntailmentSatisfied,
// IsCorrelatedOnly, HasGraphCycle. It performs no retrieval, no LLM call, and adds no scoring/IV engine.
//
// Design rules mirrored from the other v4.0 slices:
//   • The graph classifies bindings the LLM/upstream proposed; it never invents support.
//   • Missing evidence is NOT false: a proposition with no admitted support is simply "unsupported"
//     (feeds PASS_WITH_UNRESOLVED downstream), never blocked here.
//   • Correlated-only support (all admitted supporting bindings collapse onto one source document) is
//     reported so it never silently inflates apparent corroboration — same independence idea as
//     LegalEvidenceRedundancyPolicy, surfaced as a boolean signal for the gate.
//   • A supporting binding is only ADMISSIBLE when it has a source span AND satisfies entailment; the
//     graph reports the first failing admissibility condition so the gate can request a repair.
// ──────────────────────────────────────────────────────────────────────────────────────────────────

// The role a binding plays between an evidence unit (or proposition) and a target proposition.
public static class LegalEvidenceBindingRoles
{
    public const string Supports = "SUPPORTS";
    public const string Conflicts = "CONFLICTS";
    public const string Derives = "DERIVES"; // proposition → proposition entailment/derivation.
}

// An evidence unit: an admissible span of a source document that a binding can cite.
public sealed record LegalEvidenceUnit(
    string EvidenceId,
    string SourceDocumentId,
    // A concrete located span (page/char range or quote id). Absent span ⇒ the binding cannot ground.
    string? SourceSpan = null);

// A proposition node in the light graph.
public sealed record LegalEvidenceProposition(
    string PropositionId,
    // Whether this proposition asserts a legal conclusion that requires authority verification (§3).
    bool RequiresAuthority = false,
    bool AuthorityVerified = false);

// A binding between a source (evidence unit or proposition) and a target proposition. EntailmentSatisfied
// is the upstream verifier's opinion that the source actually entails/undermines the target — the graph
// does not re-derive it, it only requires it for a SUPPORTS binding to count as admissible.
public sealed record LegalEvidenceBinding(
    string BindingId,
    string Role,
    // For SUPPORTS/CONFLICTS this is an EvidenceId; for DERIVES this is a source PropositionId.
    string SourceId,
    string TargetPropositionId,
    bool EntailmentSatisfied = true);

// The per-node evidence signals derived for one proposition, shaped to feed LegalPropositionIntegrityInput.
public sealed record LegalEvidenceNodeSignals(
    string PropositionId,
    bool HasSupportingEvidence,
    bool HasSourceSpan,
    bool EntailmentSatisfied,
    bool IsCorrelatedOnly,
    bool HasConflictingEvidence,
    bool AuthorityRequired,
    bool AuthorityVerified,
    IReadOnlyList<string> Notes);

public static class LegalEvidenceNodeNotes
{
    public const string NoAdmittedSupport = "NO_ADMITTED_SUPPORT";
    public const string SourceSpanMissing = "SOURCE_SPAN_MISSING";
    public const string EntailmentFailed = "ENTAILMENT_FAILED";
    public const string CorrelatedSingleSource = "CORRELATED_SINGLE_SOURCE";
    public const string ConflictPresent = "CONFLICT_PRESENT";
    public const string UnknownEvidenceReference = "UNKNOWN_EVIDENCE_REFERENCE";
}

public static class LegalLightEvidenceGraph
{
    // Derive the evidence signals for every proposition in the graph. Bindings that reference unknown
    // evidence/proposition ids are ignored for admissibility but noted (they add no corroboration signal).
    public static IReadOnlyList<LegalEvidenceNodeSignals> DeriveSignals(
        IReadOnlyCollection<LegalEvidenceProposition> propositions,
        IReadOnlyCollection<LegalEvidenceUnit> evidenceUnits,
        IReadOnlyCollection<LegalEvidenceBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(propositions);
        ArgumentNullException.ThrowIfNull(evidenceUnits);
        ArgumentNullException.ThrowIfNull(bindings);

        var evidenceById = evidenceUnits
            .GroupBy(e => e.EvidenceId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return propositions
            .Select(p => DeriveNodeSignals(p, evidenceById, bindings))
            .ToList();
    }

    private static LegalEvidenceNodeSignals DeriveNodeSignals(
        LegalEvidenceProposition proposition,
        IReadOnlyDictionary<string, LegalEvidenceUnit> evidenceById,
        IReadOnlyCollection<LegalEvidenceBinding> bindings)
    {
        var notes = new List<string>();

        var incoming = bindings
            .Where(b => string.Equals(b.TargetPropositionId, proposition.PropositionId, StringComparison.Ordinal))
            .ToList();

        var hasConflict = incoming.Any(b => string.Equals(b.Role, LegalEvidenceBindingRoles.Conflicts, StringComparison.Ordinal));
        if (hasConflict) notes.Add(LegalEvidenceNodeNotes.ConflictPresent);

        // Only SUPPORTS bindings that resolve to a known evidence unit are candidates for admission.
        var supportBindings = incoming
            .Where(b => string.Equals(b.Role, LegalEvidenceBindingRoles.Supports, StringComparison.Ordinal))
            .ToList();

        var admitted = new List<(LegalEvidenceBinding Binding, LegalEvidenceUnit Evidence)>();
        var anySpanMissing = false;
        var anyEntailmentFailed = false;

        foreach (var binding in supportBindings)
        {
            if (!evidenceById.TryGetValue(binding.SourceId, out var evidence))
            {
                notes.Add(LegalEvidenceNodeNotes.UnknownEvidenceReference);
                continue;
            }

            if (string.IsNullOrWhiteSpace(evidence.SourceSpan))
            {
                anySpanMissing = true;
                continue;
            }

            if (!binding.EntailmentSatisfied)
            {
                anyEntailmentFailed = true;
                continue;
            }

            admitted.Add((binding, evidence));
        }

        var hasSupportingEvidence = admitted.Count > 0;

        // A binding was proposed but could not ground → surface the first blocking admissibility reason so
        // the gate can request a repair rather than silently dropping the support.
        var hasSourceSpan = true;
        var entailmentSatisfied = true;
        if (!hasSupportingEvidence)
        {
            if (anySpanMissing)
            {
                hasSourceSpan = false;
                notes.Add(LegalEvidenceNodeNotes.SourceSpanMissing);
            }
            else if (anyEntailmentFailed)
            {
                entailmentSatisfied = false;
                notes.Add(LegalEvidenceNodeNotes.EntailmentFailed);
            }
            else
            {
                notes.Add(LegalEvidenceNodeNotes.NoAdmittedSupport);
            }
        }

        // Correlated-only: two or more admitted supporting bindings that all trace to a single source
        // document are one source echoed, not independent corroboration (same idea as the redundancy
        // policy). One admitted binding is trivially "not correlated" (nothing to be redundant against).
        var isCorrelatedOnly = false;
        if (admitted.Count > 1)
        {
            var distinctSources = admitted
                .Select(a => a.Evidence.SourceDocumentId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (distinctSources == 1)
            {
                isCorrelatedOnly = true;
                notes.Add(LegalEvidenceNodeNotes.CorrelatedSingleSource);
            }
        }

        return new LegalEvidenceNodeSignals(
            proposition.PropositionId,
            hasSupportingEvidence,
            hasSourceSpan,
            entailmentSatisfied,
            isCorrelatedOnly,
            hasConflict,
            proposition.RequiresAuthority,
            proposition.AuthorityVerified,
            notes);
    }

    // Detect a directed cycle over DERIVES (proposition → proposition) bindings. A cycle in the derivation
    // graph is a mandatory-repair condition for the Integrity Gate (§9 GRAPH_CYCLE): no proposition may
    // transitively derive itself. Iterative DFS with a recursion-stack colouring (no recursion depth risk).
    public static bool HasDerivationCycle(
        IReadOnlyCollection<LegalEvidenceProposition> propositions,
        IReadOnlyCollection<LegalEvidenceBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(propositions);
        ArgumentNullException.ThrowIfNull(bindings);

        // Adjacency over DERIVES edges only: SourceId (a proposition) → TargetPropositionId.
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in propositions)
            adjacency.TryAdd(node.PropositionId, []);

        foreach (var binding in bindings.Where(b => string.Equals(b.Role, LegalEvidenceBindingRoles.Derives, StringComparison.Ordinal)))
        {
            if (!adjacency.TryGetValue(binding.SourceId, out var targets))
            {
                targets = [];
                adjacency[binding.SourceId] = targets;
            }
            targets.Add(binding.TargetPropositionId);
        }

        // 0 = unvisited, 1 = in-stack (grey), 2 = done (black).
        var colour = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var start in adjacency.Keys)
        {
            if (colour.GetValueOrDefault(start) == 2)
                continue;

            var stack = new Stack<(string Node, int NextChild)>();
            stack.Push((start, 0));
            colour[start] = 1;

            while (stack.Count > 0)
            {
                var (node, childIndex) = stack.Pop();
                var children = adjacency.TryGetValue(node, out var list) ? list : [];

                if (childIndex < children.Count)
                {
                    // Re-push the current frame advanced to the next child, then descend.
                    stack.Push((node, childIndex + 1));
                    var child = children[childIndex];
                    var childColour = colour.GetValueOrDefault(child);
                    if (childColour == 1)
                        return true; // back-edge to a node still on the stack → cycle.
                    if (childColour == 0)
                    {
                        colour[child] = 1;
                        stack.Push((child, 0));
                    }
                }
                else
                {
                    colour[node] = 2; // fully explored.
                }
            }
        }

        return false;
    }
}
