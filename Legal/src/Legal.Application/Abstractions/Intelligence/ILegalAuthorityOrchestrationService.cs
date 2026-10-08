using Legal.Application.Features.Intelligence.Decision.Lpi;

namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// ILegalAuthorityOrchestrationService — the LegalAuthority DRIVER (shared LPI funnel, park-only half).
//
// Where the LegalAuthority channel resolves qualitative contributions for diagnostics, this driver
// converts each VERIFIED legal-authority evidence item (statute / regulation / case law) into a shared
// RetrievedProposition + LpiPlacementProposal and PARKS it for attorney review — exactly like the
// Document-Retrieval and Media-Evidence drivers. It NEVER scores, applies, or ranks: POLOXI Core alone
// scores an accepted authority, and acceptance flows through the shared IPropositionIntegrationService
// funnel. A proposition is parked ReviewRequired when it matched an authoritative node, or
// NeedsHierarchyReview when no evidence-bearing node matched. Idempotent per evidence item.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface ILegalAuthorityOrchestrationService
{
    // Run one LegalAuthority pass for a matter: match every VERIFIED authority to an authoritative node
    // and park a proposition for review. Returns a batch summary (authorities scanned / matched / parked).
    Task<LegalAuthorityOrchestrationResult> RunAsync(
        LegalAuthorityOrchestrationRequest request, CancellationToken cancellationToken = default);
}

// Everything the driver needs to run one LegalAuthority pass. Revisions/scoring-config are resolved
// server-side so parked propositions and later acceptance share the manual ADI path's identity contract.
public sealed record LegalAuthorityOrchestrationRequest(
    Guid TenantId,
    Guid ActorUserId,
    Guid MatterId,
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    string ScoringConfigurationVersion);

// Outcome of one LegalAuthority pass. Items are preserved (never discarded); unmatched authorities are
// parked NeedsHierarchyReview so the promise that a proposal is ALWAYS preserved is kept.
public sealed record LegalAuthorityOrchestrationResult(
    int AuthoritiesScanned,
    int AuthoritiesMatched,
    int ItemsParked,
    string StatusCode,          // Completed | NoHierarchy | NoAuthorities | Failed
    string? FailureReason);
