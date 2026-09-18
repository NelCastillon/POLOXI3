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

public sealed record OutputClaimAuditResult
{
    public required bool IsClean { get; init; }

    public required bool Enforced { get; init; }

    public IReadOnlyList<OutputClaimViolation> Violations { get; init; } = [];

    public IReadOnlyList<Guid> UnknownClaimIds { get; init; } = [];

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

        foreach (var claimId in outputClaimIds.Distinct())
        {
            var row = await repository.GetClaimAsync(claimId, tenantId, cancellationToken);
            if (row is null || row.DecisionSessionId != sessionId)
            {
                // A claim referenced in output that POLOXI does not own for this session is itself a
                // violation of provenance — it cannot be authorized.
                unknown.Add(claimId);
                continue;
            }

            var claim = ClaimPersistenceMapper.ToDomain(row);

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

        var isClean = violations.Count == 0 && unknown.Count == 0;
        narrative.Add(isClean
            ? $"Output clean: all {outputClaimIds.Count} referenced claim(s) POLOXI-authorized."
            : $"Output audit failed: {violations.Count} unauthorized claim(s), {unknown.Count} unknown/foreign claim(s).");

        return new OutputClaimAuditResult
        {
            IsClean = isClean,
            Enforced = true,
            Violations = violations,
            UnknownClaimIds = unknown,
            AuditNarrative = narrative,
        };
    }
}
