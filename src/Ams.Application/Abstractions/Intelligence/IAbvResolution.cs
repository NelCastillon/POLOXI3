using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Intelligence;

// POLOXI ABV (Actionable Business Value) abstractions. Flow:
// Converged Composite → LLM Intent Proposal → POLOXI Intent Validation → Deterministic
// Impact/Urgency/Owner/Action resolution (Domain-Pack config) → Actionability Gate → Persist.
// The LLM proposes intent ONLY; every business value resolves deterministically with provenance.

public interface IAbvResolutionEngine
{
    Task<AbvResolutionOutcome> ResolveAsync(AbvResolutionRequest request,CancellationToken cancellationToken=default);
}

// Deterministic resolution of impact/urgency/owner/action from a loaded Domain Pack. No LLM,
// no fabrication: unsupported values remain null.
public interface IAbvGovernanceEngine
{
    // Validates the proposed intent against the pack taxonomy; null when the proposal is rejected.
    AbvIntent? AcceptIntent(AbvIntentProposal proposal,InterpretationComposite composite,AbvDomainPack pack);
    AbvImpact ResolveImpact(AbvIntent intent,InterpretationComposite composite,AbvDomainPack pack);
    AbvUrgency ResolveUrgency(AbvIntent intent,AbvImpact impact,AbvDomainPack pack);
    AbvExecutionPath ResolveExecutionPath(AbvIntent intent,AbvImpact impact,AbvDomainPack pack);
    AbvActionability ResolveActionability(AbvResolutionStatus status,AbvExecutionPath? executionPath,AbvDomainPack pack);
}
