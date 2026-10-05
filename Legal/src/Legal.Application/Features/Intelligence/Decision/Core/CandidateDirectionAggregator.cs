namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Pure per-candidate NET direction aggregation for LPI retrieval impacts.
//
// A single retrieved proposition can reach the SAME owning session candidate from several placements
// (directly, and through dependency / defeating-edge lineage). Writing one candidate impact per
// placement and keeping only the FIRST (first-wins dedup) silently discarded conflicting directions and
// was order-dependent. This aggregator collapses all contributions for one candidate into a single
// deterministic NET direction token read by DecisionReevaluationPlanner:
//   • all contributions agree              → that shared direction (Strengthened | Weakened)
//   • contributions OPPOSE (both present)  → RequiresEvaluation (contested → reopen, ZERO ranking bias)
//   • any neutral qualifier is present     → RequiresEvaluation (an unevaluated qualifier is never
//                                            overridden by a directional sibling)
// Severity escalates to Material if ANY contributor is material. POLOXI Core still owns the final
// magnitude / competition — this only resolves the qualitative direction, never a score.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class CandidateDirectionTokens
{
    public const string Strengthened = "Strengthened";
    public const string Weakened = "Weakened";
    public const string RequiresEvaluation = "RequiresEvaluation";
}

// Mutable accumulator for every placement→candidate contribution resolving to ONE owning candidate.
public sealed class CandidateDirectionAggregator
{
    private readonly List<string> _sampleBasis = [];
    private bool _hasStrengthen;
    private bool _hasWeaken;
    private bool _hasNeutral;

    public string CandidateCode { get; }
    public string? CandidateLabel { get; }
    public bool IsMaterial { get; private set; }

    public CandidateDirectionAggregator(string candidateCode, string? candidateLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateCode);
        CandidateCode = candidateCode;
        CandidateLabel = candidateLabel;
    }

    // Record one contribution. 'direction' is a token from CandidateDirectionTokens (any unrecognized or
    // RequiresEvaluation value is treated as neutral). 'basis' is a short audit label (e.g. node:relationship).
    public CandidateDirectionAggregator Add(string direction, bool isMaterial, string? basis = null)
    {
        if (string.Equals(direction, CandidateDirectionTokens.Strengthened, StringComparison.OrdinalIgnoreCase))
            _hasStrengthen = true;
        else if (string.Equals(direction, CandidateDirectionTokens.Weakened, StringComparison.OrdinalIgnoreCase))
            _hasWeaken = true;
        else
            _hasNeutral = true;

        IsMaterial |= isMaterial;
        if (!string.IsNullOrWhiteSpace(basis) && _sampleBasis.Count < 6)
            _sampleBasis.Add(basis);

        return this;
    }

    // True only when BOTH a strengthening AND a weakening contribution were recorded.
    public bool IsContested => _hasStrengthen && _hasWeaken;

    // Deterministic net direction. Contested OR any neutral qualifier ⇒ RequiresEvaluation.
    public string ResolveNetDirection()
    {
        if (_hasNeutral || IsContested)
            return CandidateDirectionTokens.RequiresEvaluation;
        if (_hasStrengthen)
            return CandidateDirectionTokens.Strengthened;
        if (_hasWeaken)
            return CandidateDirectionTokens.Weakened;
        return CandidateDirectionTokens.RequiresEvaluation;
    }

    public string BuildRationale(string netDirection)
    {
        var basis = string.Join(", ", _sampleBasis);
        return IsContested
            ? $"Candidate '{CandidateCode}' received CONTESTED contributions ({basis}); net RequiresEvaluation — branch reopened for attorney evaluation without biasing the ranking."
            : $"Candidate '{CandidateCode}' {netDirection.ToLowerInvariant()} by retrieved proposition placement(s) ({basis}).";
    }
}
