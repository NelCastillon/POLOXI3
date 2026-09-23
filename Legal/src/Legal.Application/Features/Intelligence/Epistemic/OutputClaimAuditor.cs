using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Epistemic;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Epistemic Authority Layer — EA-5 output claim audit (§34, §35).
//
// The final answer may only surface propositions POLOXI authorized. An LLM-asserted claim that was
// never verified (authority = None) — or one that is contradicted/disputed — must not appear in
// output as if it were established fact. This auditor takes the set of claim IDs the composed output
// references and reports any that are not publishable.
//
// Gated by UseOutputClaimAudit so the release can be enabled/disabled in isolation. When the flag is
// off, the auditor reports no violations with a note and never blocks output.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// A single output-referenced claim that is not POLOXI-authorized for publication.
public sealed record OutputClaimViolation
{
    public required Guid ClaimId { get; init; }

    public required string ClaimText { get; init; }

    public required ClaimVerificationState VerificationState { get; init; }

    public required ClaimDecisionAuthority DecisionAuthority { get; init; }

    public required string Reason { get; init; }
}

// POLOXI-decided disposition for one output claim. POLOXI determines the disposition from the claim's
// authoritative verification/authority state; the composer only chooses the wording that satisfies it.
//   ALLOW    — authorized and supported: may be stated as established.
//   QUALIFY  — legitimate but unresolved/partial: must be stated as uncertainty, not as fact.
//   SUPPRESS — unsupported or foreign material assertion: must not appear as an assertion.
//   CORRECT  — contradicted by authoritative state: must be removed or corrected.
public enum OutputClaimDisposition
{
    Allow,
    Qualify,
    Suppress,
    Correct
}

public static class OutputClaimDispositions
{
    public const string Allow = "ALLOW";
    public const string Qualify = "QUALIFY";
    public const string Suppress = "SUPPRESS";
    public const string Correct = "CORRECT";

    public static string ToCode(OutputClaimDisposition disposition) => disposition switch
    {
        OutputClaimDisposition.Allow => Allow,
        OutputClaimDisposition.Qualify => Qualify,
        OutputClaimDisposition.Suppress => Suppress,
        OutputClaimDisposition.Correct => Correct,
        _ => Suppress,
    };

    // Core authorization rule (§34). POLOXI maps a claim's authoritative state onto a disposition.
    // Note: an UNVERIFIED claim is QUALIFIED (transformed into uncertainty), NOT suppressed — absence
    // of support is not evidence of falsity, so the reasoning stays visible without asserting the claim.
    // A foreign/unknown claim (not owned by this session) is SUPPRESSED; a CONTRADICTED claim is CORRECTED.
    public static OutputClaimDisposition Classify(
        ClaimVerificationState verificationState, ClaimDecisionAuthority authority)
    {
        // Contradicted by authoritative state → must be corrected/removed regardless of authority.
        if (verificationState == ClaimVerificationState.Contradicted)
            return OutputClaimDisposition.Correct;

        // Authorized (Limited/Full) and supported → may be stated as established.
        if (authority != ClaimDecisionAuthority.None && verificationState == ClaimVerificationState.Supported)
            return OutputClaimDisposition.Allow;

        // Legitimate but unresolved/partial → transform into uncertainty rather than suppress.
        if (verificationState is ClaimVerificationState.Proposed
            or ClaimVerificationState.VerificationRequired
            or ClaimVerificationState.VerificationInProgress
            or ClaimVerificationState.Disputed
            or ClaimVerificationState.Unverified)
            return OutputClaimDisposition.Qualify;

        // Unverifiable, or any unsupported material assertion → cannot appear as an assertion.
        return OutputClaimDisposition.Suppress;
    }

    public static OutputClaimDisposition EnforceMappingInvariant(
        bool isMaterial,
        ClaimMappingState mappingState,
        OutputClaimDisposition disposition) =>
        isMaterial && mappingState != ClaimMappingState.Mapped && disposition == OutputClaimDisposition.Allow
            ? mappingState == ClaimMappingState.Ambiguous
                ? OutputClaimDisposition.Qualify
                : OutputClaimDisposition.Suppress
            : disposition;
}

// A per-claim authorization decision: which claim, its authoritative state, and the disposition POLOXI
// assigned. This is the authoritative instruction the composer must satisfy through wording only.
public sealed record OutputClaimAuthorization
{
    public required Guid ClaimId { get; init; }

    public required string ClaimText { get; init; }

    public required ClaimVerificationState VerificationState { get; init; }

    public required ClaimDecisionAuthority DecisionAuthority { get; init; }

    public required OutputClaimDisposition Disposition { get; init; }

    // True when the claim is not owned by this session (foreign/unknown provenance).
    public bool IsForeign { get; init; }

    public required string Reason { get; init; }
}

public sealed record OutputClaimAuditResult
{
    public required bool IsClean { get; init; }

    public required bool Enforced { get; init; }

    public IReadOnlyList<OutputClaimViolation> Violations { get; init; } = [];

    public IReadOnlyList<Guid> UnknownClaimIds { get; init; } = [];

    // Per-claim disposition decisions (ALLOW/QUALIFY/SUPPRESS/CORRECT) for every referenced claim.
    // Enables claim-level output authorization: the composer repairs wording per disposition instead
    // of the whole answer being reframed as provisional.
    public IReadOnlyList<OutputClaimAuthorization> Authorizations { get; init; } = [];

    public IReadOnlyList<string> AuditNarrative { get; init; } = [];
}

public interface IOutputClaimAuditor
{
    Task<OutputClaimAuditResult> AuditAsync(
        Guid sessionId,
        Guid tenantId,
        IReadOnlyCollection<Guid> outputClaimIds,
        CancellationToken cancellationToken = default);
}

public sealed class OutputClaimAuditor(
    IEpistemicClaimRepository repository,
    EpistemicAuthoritySettings settings)
    : IOutputClaimAuditor
{
    public async Task<OutputClaimAuditResult> AuditAsync(
        Guid sessionId,
        Guid tenantId,
        IReadOnlyCollection<Guid> outputClaimIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outputClaimIds);

        var narrative = new List<string>();

        // §34 feature flag: output audit can be disabled in isolation. When off, the auditor does not
        // gate output — it simply advises.
        if (!settings.UseOutputClaimAudit)
        {
            narrative.Add("Output claim audit disabled (UseOutputClaimAudit=false); output not gated.");
            return new OutputClaimAuditResult
            {
                IsClean = true,
                Enforced = false,
                AuditNarrative = narrative,
            };
        }

        if (outputClaimIds.Count == 0)
        {
            narrative.Add("No output claims referenced; nothing to audit.");
            return new OutputClaimAuditResult
            {
                IsClean = true,
                Enforced = true,
                AuditNarrative = narrative,
            };
        }

        var violations = new List<OutputClaimViolation>();
        var unknown = new List<Guid>();
        var authorizations = new List<OutputClaimAuthorization>();

        foreach (var claimId in outputClaimIds.Distinct())
        {
            var row = await repository.GetClaimAsync(claimId, tenantId, cancellationToken);
            if (row is null || row.DecisionSessionId != sessionId)
            {
                // A claim referenced in output that POLOXI does not own for this session is itself a
                // violation of provenance — it cannot be authorized. Foreign material is SUPPRESSED.
                unknown.Add(claimId);
                authorizations.Add(new OutputClaimAuthorization
                {
                    ClaimId = claimId,
                    ClaimText = string.Empty,
                    VerificationState = ClaimVerificationState.Unverifiable,
                    DecisionAuthority = ClaimDecisionAuthority.None,
                    Disposition = OutputClaimDisposition.Suppress,
                    IsForeign = true,
                    Reason = "Claim is not owned by this session (foreign/unknown provenance); suppress.",
                });
                continue;
            }

            var claim = ClaimPersistenceMapper.ToDomain(row);

            // POLOXI decides the disposition from the claim's authoritative state; the composer only
            // chooses wording that satisfies it (ALLOW/QUALIFY/SUPPRESS/CORRECT).
            var disposition = OutputClaimDispositions.Classify(claim.VerificationState, claim.DecisionAuthority);
            authorizations.Add(new OutputClaimAuthorization
            {
                ClaimId = claim.ClaimId,
                ClaimText = claim.Text,
                VerificationState = claim.VerificationState,
                DecisionAuthority = claim.DecisionAuthority,
                Disposition = disposition,
                IsForeign = false,
                Reason = $"state={ClaimCodes.ToCode(claim.VerificationState)}, "
                    + $"authority={ClaimCodes.ToCode(claim.DecisionAuthority)} → {OutputClaimDispositions.ToCode(disposition)}.",
            });

            // Publishable == POLOXI-authorized. Authority=None covers proposed/unverified/contradicted/
            // disputed claims; none of those may be surfaced as established output.
            if (claim.DecisionAuthority != ClaimDecisionAuthority.None)
                continue;

            violations.Add(new OutputClaimViolation
            {
                ClaimId = claim.ClaimId,
                ClaimText = claim.Text,
                VerificationState = claim.VerificationState,
                DecisionAuthority = claim.DecisionAuthority,
                Reason = $"Claim is not POLOXI-authorized (state={ClaimCodes.ToCode(claim.VerificationState)}, "
                    + "authority=None); it must not appear in output as established.",
            });
        }

        // Output is clean only when EVERY referenced claim resolved to ALLOW. This is disposition-driven,
        // not authority-driven: Contradicted/Disputed claims carry Limited authority (not None) yet resolve
        // to CORRECT/QUALIFY, so an authority-only check would wrongly pass them. A CORRECT (contradicted)
        // or QUALIFY (unresolved) claim surfacing as an assertion must trigger enforcement.
        var isClean = authorizations.All(a => a.Disposition == OutputClaimDisposition.Allow);
        narrative.Add(isClean
            ? $"Output clean: all {outputClaimIds.Count} referenced claim(s) POLOXI-authorized (ALLOW)."
            : $"Output audit failed: {violations.Count} unauthorized claim(s), {unknown.Count} unknown/foreign claim(s), "
                + $"{authorizations.Count(a => a.Disposition != OutputClaimDisposition.Allow)} claim(s) requiring restatement.");

        var allowCount = authorizations.Count(a => a.Disposition == OutputClaimDisposition.Allow);
        var qualifyCount = authorizations.Count(a => a.Disposition == OutputClaimDisposition.Qualify);
        var suppressCount = authorizations.Count(a => a.Disposition == OutputClaimDisposition.Suppress);
        var correctCount = authorizations.Count(a => a.Disposition == OutputClaimDisposition.Correct);
        narrative.Add($"Dispositions: {allowCount} ALLOW, {qualifyCount} QUALIFY, {suppressCount} SUPPRESS, {correctCount} CORRECT.");

        return new OutputClaimAuditResult
        {
            IsClean = isClean,
            Enforced = true,
            Violations = violations,
            UnknownClaimIds = unknown,
            Authorizations = authorizations,
            AuditNarrative = narrative,
        };
    }
}
