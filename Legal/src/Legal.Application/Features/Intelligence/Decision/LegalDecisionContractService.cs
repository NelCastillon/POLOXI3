using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision;

// ─── Decision Contract orchestration ───────────────────────────────────────────────────────────────
// Auto-provisions a DRAFT contract per matter, applies section updates with optimistic concurrency,
// computes completion + structural/semantic validation, and drives the governed lifecycle state
// machine (DRAFT → READY_FOR_REVIEW → APPROVED → ACTIVE, with return-to-draft). POLOXI stays the
// single authoritative evaluator; this service never scores candidates or picks a winner.
public sealed class LegalDecisionContractService(ILegalDecisionContractRepository repository) : ILegalDecisionContractService
{
    public async Task<DecisionContractWorkspaceDto> GetWorkspaceAsync(Guid tenantId, Guid userId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var contract = await repository.ProvisionAsync(tenantId, userId, matterId, cancellationToken);
        return await BuildWorkspaceAsync(tenantId, matterId, contract, cancellationToken);
    }

    public Task<IReadOnlyList<DecisionContractOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetOptionsAsync(tenantId, cancellationToken);

    public async Task<DecisionContractWorkspaceDto> UpdateDecisionAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractDecisionCommand command, CancellationToken cancellationToken = default)
    {
        await repository.UpdateDecisionAsync(tenantId, userId, command, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> UpdateLegalContextAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLegalContextCommand command, CancellationToken cancellationToken = default)
    {
        await repository.UpdateLegalContextAsync(tenantId, userId, command, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> UpdateBurdenAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractBurdenCommand command, CancellationToken cancellationToken = default)
    {
        await repository.UpdateBurdenAsync(tenantId, userId, command, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> UpdateBoundariesAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractBoundariesCommand command, CancellationToken cancellationToken = default)
    {
        await repository.UpdateBoundariesAsync(tenantId, userId, command, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> UpdateCandidatesAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractCandidatesCommand command, CancellationToken cancellationToken = default)
    {
        await repository.UpdateCandidatesAsync(tenantId, userId, command, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> UpdateSettingsAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractSettingsCommand command, CancellationToken cancellationToken = default)
    {
        await repository.UpdateSettingsAsync(tenantId, userId, command, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> SubmitAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLifecycleCommand command, CancellationToken cancellationToken = default)
    {
        var contract = await repository.GetContractByIdAsync(tenantId, command.DecisionContractId, cancellationToken)
            ?? throw new DecisionContractStateException("Decision Contract not found.");
        var validation = Validate(contract);
        if (validation.Any(v => v.Severity == "ERROR"))
            throw new DecisionContractStateException("Resolve all blocking validation errors before submitting for review.");

        await repository.TransitionStatusAsync(tenantId, userId, command.DecisionContractId, contract.RowVersion,
            DecisionContractStatuses.Draft, DecisionContractStatuses.ReadyForReview, "SUBMITTED", command.Comment, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> ApproveAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLifecycleCommand command, CancellationToken cancellationToken = default)
    {
        await repository.TransitionStatusAsync(tenantId, userId, command.DecisionContractId, command.RowVersion,
            DecisionContractStatuses.ReadyForReview, DecisionContractStatuses.Approved, "APPROVED", command.Comment, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> ReturnAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLifecycleCommand command, CancellationToken cancellationToken = default)
    {
        await repository.TransitionStatusAsync(tenantId, userId, command.DecisionContractId, command.RowVersion,
            DecisionContractStatuses.ReadyForReview, DecisionContractStatuses.Draft, "RETURNED", command.Comment, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> ActivateAsync(Guid tenantId, Guid userId, Guid matterId, DecisionContractLifecycleCommand command, CancellationToken cancellationToken = default)
    {
        await repository.ActivateAsync(tenantId, userId, command.DecisionContractId, command.RowVersion, cancellationToken);
        return await ReloadAsync(tenantId, matterId, command.DecisionContractId, cancellationToken);
    }

    public async Task<DecisionContractWorkspaceDto> CreateNewVersionAsync(Guid tenantId, Guid userId, Guid matterId, Guid decisionContractId, CancellationToken cancellationToken = default)
    {
        var contract = await repository.CreateNewVersionAsync(tenantId, userId, decisionContractId, cancellationToken);
        return await BuildWorkspaceAsync(tenantId, matterId, contract, cancellationToken);
    }

    // ── Workspace assembly ──────────────────────────────────────────────────────────────────────────

    private async Task<DecisionContractWorkspaceDto> ReloadAsync(Guid tenantId, Guid matterId, Guid decisionContractId, CancellationToken cancellationToken)
    {
        var contract = await repository.GetContractByIdAsync(tenantId, decisionContractId, cancellationToken)
            ?? throw new DecisionContractStateException("Decision Contract not found.");
        return await BuildWorkspaceAsync(tenantId, matterId, contract, cancellationToken);
    }

    private async Task<DecisionContractWorkspaceDto> BuildWorkspaceAsync(Guid tenantId, Guid matterId, DecisionContractDto contract, CancellationToken cancellationToken)
    {
        var title = await repository.GetMatterTitleAsync(tenantId, matterId, cancellationToken);
        var history = await repository.GetVersionHistoryAsync(tenantId, matterId, cancellationToken);
        var reviews = await repository.GetReviewsAsync(tenantId, contract.DecisionContractId, cancellationToken);
        var poloxiWeights = await repository.GetPoloxiWeightsAsync(cancellationToken);

        var sections = BuildSections(contract);
        var completion = sections.Count == 0 ? 0 : (int)Math.Round(100.0 * sections.Count(s => s.Complete) / sections.Count);
        var validation = Validate(contract);

        var isDraft = contract.StatusCode == DecisionContractStatuses.Draft;
        var isReady = contract.StatusCode == DecisionContractStatuses.ReadyForReview;
        var isApproved = contract.StatusCode == DecisionContractStatuses.Approved;
        var noBlocking = validation.All(v => v.Severity != "ERROR");

        return new DecisionContractWorkspaceDto(
            matterId, title, contract, completion, sections, validation, history, reviews,
            poloxiWeights,
            CanEdit: isDraft,
            CanSubmit: isDraft && noBlocking,
            CanApprove: isReady,
            CanReturn: isReady,
            CanActivate: isApproved,
            CanCreateVersion: contract.StatusCode is DecisionContractStatuses.Active or DecisionContractStatuses.Approved or DecisionContractStatuses.Superseded);
    }

    private static IReadOnlyList<DecisionContractSectionCompletionDto> BuildSections(DecisionContractDto c)
    {
        var activeCandidates = c.Candidates.Count(x => x.StateCode == "ACTIVE");
        return
        [
            new("Decision", "Decision", !string.IsNullOrWhiteSpace(c.DecisionQuestion) && !string.IsNullOrWhiteSpace(c.ClientObjective)),
            new("LegalContext", "Legal Context", !string.IsNullOrWhiteSpace(c.Jurisdiction) && !string.IsNullOrWhiteSpace(c.ProceduralPosture) && !string.IsNullOrWhiteSpace(c.GoverningLaw)),
            new("Burden", "Burden & Standard", !string.IsNullOrWhiteSpace(c.MovingParty) && !string.IsNullOrWhiteSpace(c.InitialBurden) && !string.IsNullOrWhiteSpace(c.UltimateBurden) && !string.IsNullOrWhiteSpace(c.StandardOfProofOrReview)),
            new("Boundaries", "Decision Boundaries", !string.IsNullOrWhiteSpace(c.EvidenceBoundary) && !string.IsNullOrWhiteSpace(c.AuthorityBoundary)),
            new("Outcomes", "Competing Outcomes", activeCandidates >= 2),
            new("Settings", "Additional Settings", !string.IsNullOrWhiteSpace(c.SemanticValidationMode)),
        ];
    }

    private static IReadOnlyList<DecisionContractValidationIssueDto> Validate(DecisionContractDto c)
    {
        var issues = new List<DecisionContractValidationIssueDto>();

        void Error(string code, string section, string message, string? field = null) => issues.Add(new(code, section, "ERROR", message, field));
        void Warn(string code, string section, string message, string? field = null) => issues.Add(new(code, section, "WARNING", message, field));

        // Structural
        if (string.IsNullOrWhiteSpace(c.DecisionQuestion)) Error("DECISION_QUESTION_REQUIRED", "Decision", "A decision question is required.", nameof(c.DecisionQuestion));
        if (string.IsNullOrWhiteSpace(c.ClientObjective)) Error("CLIENT_OBJECTIVE_REQUIRED", "Decision", "A client objective is required.", nameof(c.ClientObjective));
        if (string.IsNullOrWhiteSpace(c.Jurisdiction)) Error("JURISDICTION_REQUIRED", "Legal Context", "Jurisdiction is required.", nameof(c.Jurisdiction));
        if (string.IsNullOrWhiteSpace(c.ProceduralPosture)) Error("POSTURE_REQUIRED", "Legal Context", "Procedural posture is required.", nameof(c.ProceduralPosture));
        if (string.IsNullOrWhiteSpace(c.GoverningLaw)) Error("GOVERNING_LAW_REQUIRED", "Legal Context", "Governing law is required.", nameof(c.GoverningLaw));

        // Burden
        if (string.IsNullOrWhiteSpace(c.MovingParty)) Error("MOVING_PARTY_REQUIRED", "Burden & Standard", "Moving party is required.", nameof(c.MovingParty));
        if (string.IsNullOrWhiteSpace(c.StandardOfProofOrReview)) Error("STANDARD_REQUIRED", "Burden & Standard", "Decision standard is required.", nameof(c.StandardOfProofOrReview));

        // Boundaries
        if (string.IsNullOrWhiteSpace(c.EvidenceBoundary)) Error("EVIDENCE_BOUNDARY_REQUIRED", "Decision Boundaries", "Evidence boundary must be defined.", nameof(c.EvidenceBoundary));
        if (string.IsNullOrWhiteSpace(c.AuthorityBoundary)) Error("AUTHORITY_BOUNDARY_REQUIRED", "Decision Boundaries", "Authority boundary must be defined.", nameof(c.AuthorityBoundary));

        // Candidate validation — at least two distinguishable active outcomes.
        var active = c.Candidates.Where(x => x.StateCode == "ACTIVE").ToArray();
        if (active.Length < 2)
            Error("OUTCOMES_MIN_TWO", "Competing Outcomes", "At least two active competing outcomes are required.");
        var distinct = active.Select(x => x.OutcomeText.Trim().ToLowerInvariant()).Distinct().Count();
        if (active.Length >= 2 && distinct < active.Length)
            Warn("OUTCOMES_DISTINCT", "Competing Outcomes", "Competing outcomes should be distinguishable from one another.");

        // Advisory semantic checks.
        if (!string.IsNullOrWhiteSpace(c.DecisionQuestion) && c.DecisionQuestion.Trim().Split(' ').Length < 4)
            Warn("QUESTION_TOO_BROAD", "Decision", "The decision question may be too broad; specify claim, posture, and relief.", nameof(c.DecisionQuestion));

        var unknownFacts = c.FactBoundaries.Count(f => f.FactStateCode == DecisionContractFactStates.Unknown);
        if (unknownFacts > 0)
            Warn("UNKNOWN_FACTS", "Decision Boundaries", $"{unknownFacts} unknown fact(s) may affect material propositions.");

        return issues;
    }
}
