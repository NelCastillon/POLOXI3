using System.ComponentModel.DataAnnotations;

namespace Legal.Application.Features.Intelligence.Science;

// ── POLOXI Math V1 — service request/response contracts (runnable slice) ────────────────────────────
// The public shape of a Math solve run. Mirrors the Intelligence request conventions (tenant/user/
// correlation) so it slots into the same auth + telemetry patterns as the Wide pipeline. The response
// exposes the honest outcome plus a transparency trail: the structured contract, the proof graph, the
// per-obligation verification results, and the ranked candidates — never a bare "answer".
public sealed record MathSolveRequest(
    Guid TenantId,
    Guid UserId,
    [Required, StringLength(8000, MinimumLength = 2)] string Problem,
    [Required, StringLength(120)] string CorrelationId = "")
{
    // Optional model routing override (e.g. a reasoning model for the synthesis stages).
    public string? ModelCode { get; init; }

    public IReadOnlyCollection<string> GrantedPermissions { get; init; } = [];
}

public sealed record MathSolveResponse
{
    public required string Outcome { get; init; }

    public string? FinalAnswer { get; init; }

    public string? CanonicalAnswer { get; init; }

    public string? SolutionSummary { get; init; }

    public IReadOnlyList<string> KeySteps { get; init; } = [];

    public string? RemainingUncertainty { get; init; }

    // How promising the leading direction is (0..1) — NEVER a probability the answer is correct.
    public double DiscoveryConfidence { get; init; }

    // Categorical verification status of the leading proof path.
    public required string VerificationStatus { get; init; }

    public double SelfConsistencyAgreement { get; init; }

    public required MathProblemContractProposal Contract { get; init; }

    public IReadOnlyList<MathObligationResult> Obligations { get; init; } = [];

    public IReadOnlyList<MathCandidateResult> Candidates { get; init; } = [];

    // False when the AI model route was unavailable for this tenant/feature and the pipeline degraded
    // to deterministic-only reasoning. The UI uses this to surface an actionable configuration message.
    public bool ModelAvailable { get; init; } = true;

    // Multidimensional epistemic state (R/E/V/F) for the leading direction, kept separate from the single
    // DiscoveryConfidence scalar so research priority is never read as mathematical verification.
    public double ResearchPriority { get; init; }
    public double EvidenceSupport { get; init; }
    public double MathematicalVerification { get; init; }
    public double FalsificationCoverage { get; init; }
    public string EpistemicStateLabel { get; init; } = "UNVERIFIED";

    // Soft "claim outruns proof" warnings from the certainty ceiling + prior-art gate. Non-empty means at
    // least one claim was asserted more strongly than its essential obligations support.
    public IReadOnlyList<string> ClaimWarnings { get; init; } = [];

    // First-class research-state output: what is verified, eliminated, surviving, still open, and the next
    // highest-information test — so valuable negative results are not buried under a new "final answer".
    public IReadOnlyList<string> VerifiedFacts { get; init; } = [];
    public IReadOnlyList<string> EliminatedRoutes { get; init; } = [];
    public IReadOnlyList<string> SurvivingCandidates { get; init; } = [];
    public IReadOnlyList<string> OpenObligations { get; init; } = [];
    public string? NextHighestInformationTest { get; init; }

    public required string CorrelationId { get; init; }
}

// A single proof obligation after deterministic verification — the audit trail proving nothing was
// silently accepted on LLM confidence.
public sealed record MathObligationResult(
    string ObligationId,
    string Statement,
    string VerificationMethod,
    string Status,
    string CounterexampleStatus,
    double DiscoveryConfidence,
    string? VerificationNote);

// A competing strategy/candidate after verification-weighted ranking.
public sealed record MathCandidateResult(
    string Id,
    string ObjectType,
    string Name,
    string? Description,
    double DiscoveryConfidence,
    string VerificationStatus);
