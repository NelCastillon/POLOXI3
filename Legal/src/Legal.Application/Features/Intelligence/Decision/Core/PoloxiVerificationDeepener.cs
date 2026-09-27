namespace Legal.Application.Features.Intelligence.Decision.Core;

// Passthrough deepener: keeps the ambiguous semantic result unchanged. Used when POLOXI deepening is
// disabled by configuration.
public sealed class DisabledPoloxiVerificationDeepener : IPoloxiVerificationDeepener
{
    public Task<SemanticVerificationResult> DeepenAsync(
        PoloxiVerificationContract contract,
        SemanticVerificationResult current,
        CancellationToken cancellationToken = default) => Task.FromResult(current);
}

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Deterministic, bounded POLOXI verification deepener (Milestone C — investigation/deepening loop).
//
// Purpose: resolve an AMBIGUOUS decision-material semantic verification WITHOUT a new model call and
// WITHOUT lowering any evidence-admission gate. It reasons only over the component evidence already
// gathered by the semantic verifier (supported / unsupported / contradicted components and the
// unresolved discriminators carried on the contract) and collapses the ambiguity to the strongest
// outcome the evidence deterministically justifies.
//
// Fail-closed guarantees:
//   • Never elevates support beyond what the collected components prove. Any contradiction forces
//     Contradicted; any remaining unsupported component or unresolved discriminator caps the result at
//     PartiallySupported; only fully-supported-with-no-open-discriminators yields Supported.
//   • Only returns an outcome the contract explicitly permits (AllowedOutcomes). If the resolved
//     outcome is not allowed, it degrades to Unverifiable (the safe, non-authorizing state).
//   • Always marks the result Deepened and Ambiguous=false so the pipeline stops the bounded loop and
//     records exactly one deepening round in telemetry.
//   • Adds no tokens/latency; deepening is a pure, cost-free consolidation step.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DeterministicPoloxiVerificationDeepener : IPoloxiVerificationDeepener
{
    public Task<SemanticVerificationResult> DeepenAsync(
        PoloxiVerificationContract contract,
        SemanticVerificationResult current,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(current);

        // Nothing to do if the prior pass already settled (defensive: pipeline only calls when ambiguous).
        if (!current.Ambiguous)
            return Task.FromResult(current);

        var support = current.PropositionSupport;

        var hasContradiction = support.ContradictedComponents.Count > 0;
        var hasUnsupported = support.UnsupportedComponents.Count > 0;
        var hasOpenDiscriminators = contract.UnresolvedDiscriminators.Count > 0;
        var hasSupport = support.SupportedComponents.Count > 0;

        // Deterministic collapse of the ambiguity, strongest-justified-first, fail-closed.
        var resolved = hasContradiction
            ? PropositionSupportState.Contradicted
            : hasUnsupported || hasOpenDiscriminators
                ? (hasSupport ? PropositionSupportState.PartiallySupported : PropositionSupportState.Unsupported)
                : hasSupport
                    ? PropositionSupportState.Supported
                    : PropositionSupportState.Unverifiable;

        // Honor the contract's admissibility envelope. If the deterministically-resolved outcome is not
        // permitted, degrade to the safe non-authorizing state rather than forcing a stronger claim.
        if (!contract.AllowedOutcomes.Contains(resolved))
            resolved = PropositionSupportState.Unverifiable;

        var reasonCode = resolved switch
        {
            PropositionSupportState.Supported => "POLOXI_DEEPENED_SUPPORTED",
            PropositionSupportState.PartiallySupported => "POLOXI_DEEPENED_PARTIAL",
            PropositionSupportState.Unsupported => "POLOXI_DEEPENED_UNSUPPORTED",
            PropositionSupportState.Contradicted => "POLOXI_DEEPENED_CONTRADICTED",
            _ => "POLOXI_DEEPENED_UNVERIFIABLE",
        };

        var deepenedSupport = support with
        {
            State = resolved,
            ReasonCode = reasonCode,
            Reason = "Bounded deterministic deepening consolidated the ambiguous semantic components into "
                     + $"'{resolved}' without additional model calls or external retrieval.",
            VerificationMethod = "POLOXI_DETERMINISTIC_DEEPENING",
        };

        return Task.FromResult(current with
        {
            PropositionSupport = deepenedSupport,
            Ambiguous = false,
            Deepened = true,
        });
    }
}