using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

// Orchestrates the Decision Contract workspace: auto-provision, section updates, validation, and
// governed lifecycle transitions. POLOXI remains the authoritative evaluator; this service only
// manages the attorney-defined problem specification and never scores candidates.
public interface ILegalDecisionContractService
{
    Task<DecisionContractWorkspaceDto> GetWorkspaceAsync(Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DecisionContractOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<DecisionContractWorkspaceDto> UpdateDecisionAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractDecisionCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> UpdateLegalContextAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLegalContextCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> UpdateBurdenAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractBurdenCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> UpdateBoundariesAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractBoundariesCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> UpdateCandidatesAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractCandidatesCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> UpdateSettingsAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractSettingsCommand command, CancellationToken cancellationToken = default);

    Task<DecisionContractWorkspaceDto> SubmitAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLifecycleCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> ApproveAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLifecycleCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> ReturnAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLifecycleCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> ActivateAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLifecycleCommand command, CancellationToken cancellationToken = default);
    Task<DecisionContractWorkspaceDto> CreateNewVersionAsync(Guid tenantId, Guid userId, Guid matterId, Guid decisionContractId, CancellationToken cancellationToken = default);
}
