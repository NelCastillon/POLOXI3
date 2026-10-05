using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision.Lpi;

// Loads the authoritative decision identity revisions for a matter from the current contract, the
// promoted hierarchy authority/execution, and the LPI stale-guard version. Proposal/identity-only —
// POLOXI Core remains the sole evaluator; nothing here scores, ranks, or mutates.
public sealed class DecisionRevisionResolver(
    ILegalDecisionContractRepository decisionContractRepository,
    ILegalHierarchyExecutionRepository hierarchyRepository,
    ILpiPropositionIntegrationRepository integrationRepository) : IDecisionRevisionResolver
{
    public async Task<DecisionRevisionSnapshot> ResolveAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        // HierarchyRevision is the exact value the shared funnel's stale-guard compares against; it is
        // authoritative on its own even when no contract/execution metadata is available.
        var hierarchyRevision = await integrationRepository.GetHierarchyVersionAsync(
            tenantId, matterId, cancellationToken);

        var contract = await decisionContractRepository.GetCurrentContractAsync(
            tenantId, matterId, cancellationToken);
        if (contract is null)
            return new DecisionRevisionSnapshot(0, 0, hierarchyRevision, string.Empty);

        var authority = await hierarchyRepository.GetCurrentAuthorityAsync(
            tenantId, matterId, contract.DecisionContractId, contract.VersionNumber, cancellationToken);
        if (authority is null)
            return new DecisionRevisionSnapshot(contract.VersionNumber, 0, hierarchyRevision, string.Empty);

        var execution = await hierarchyRepository.GetExecutionAsync(
            tenantId, authority.HierarchyExecutionId, cancellationToken);

        var scoringConfigurationVersion = execution?.Execution.AlgorithmVersion ?? string.Empty;

        return new DecisionRevisionSnapshot(
            contract.VersionNumber,
            authority.RunNumber,
            hierarchyRevision,
            scoringConfigurationVersion);
    }
}
