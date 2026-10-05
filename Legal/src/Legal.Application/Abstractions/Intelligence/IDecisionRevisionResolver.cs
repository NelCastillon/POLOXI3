namespace Legal.Application.Abstractions.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// IDecisionRevisionResolver — authoritative, SERVER-SIDE source of the decision identity revisions a
// retrieved-proposition lifecycle operation (accept / revise / withdraw / retrieval run) must carry.
//
// The UI must never supply contract/candidate/hierarchy revisions or the scoring-config version: those
// are decided by POLOXI Core state, not the client. This resolver loads them from the authoritative
// records for a matter so the shared integration funnel's stale-hierarchy guard and provenance columns
// receive the CURRENT values. It never scores, ranks, or mutates anything.
//
//   * HierarchyRevision            — the exact value the stale-guard compares against
//                                    (ILpiPropositionIntegrationRepository.GetHierarchyVersionAsync).
//   * DecisionContractRevision     — current authoritative contract VersionNumber.
//   * CandidateSetRevision         — authoritative hierarchy execution RunNumber.
//   * ScoringConfigurationVersion  — authoritative execution AlgorithmVersion.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public interface IDecisionRevisionResolver
{
    Task<DecisionRevisionSnapshot> ResolveAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);
}

// Authoritative identity snapshot for a matter's current decision state. When no contract or promoted
// hierarchy exists yet, revisions fall back to 0 / empty — the funnel treats HierarchyRevision 0 as
// "no prior hierarchy to be stale against", preserving the existing fail-soft behavior.
public sealed record DecisionRevisionSnapshot(
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    string ScoringConfigurationVersion);
