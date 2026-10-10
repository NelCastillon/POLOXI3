using Legal.Api.Security;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
using Legal.Application.Features.Intelligence.Decision.Lpi;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Legal.Infrastructure.Configuration;

namespace Legal.Api.Controllers;

// Isolated controller for the self-contained POLOXI Legal Decision Intelligence module
// (/legal/decision). Evolves independently from the Intelligence Wide (/legal/search) controller.
[ApiController]
[Route("api/legal_decision")]
public sealed class LegalDecisionController(ILegalDecisionService service,IIntelligenceExecutionService executionService,ILegalDocumentCorpusRepository documentCorpusRepository,IAttorneyDecisionInputRepository attorneyDecisionInputRepository,IAttorneyDecisionInputService attorneyDecisionInputService,ILegalDecisionContractService decisionContractService,ILegalDocumentIntakeService documentIntakeService,ILegalMatterCorpusActivationService corpusActivationService,IDecisionIntegrityRepository integrityRepository,IMatterPropositionInformationValueService propositionInformationValueService,INextBestActionService nextBestActionService,IWhatToResolveNextService whatToResolveNextService,IPropositionIntegrationService propositionIntegrationService,IRetrievalPropositionReviewService retrievalReviewService,IRetrievalOrchestrationService retrievalOrchestrationService,ILegalAuthorityOrchestrationService legalAuthorityOrchestrationService,IDecisionRevisionResolver decisionRevisionResolver,Legal.Application.Features.Intelligence.Decision.Channels.IChannelScoringLpiService channelScoringLpiService,Legal.Application.Features.Intelligence.Decision.Channels.IMatterScoringTraceReader matterScoringTraceReader,IOptions<DocumentIntelligenceOptions> documentOptions) : ControllerBase
{
    private const string CapabilityCode = JudzCapabilities.LegalDecision;
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("decide")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Decide([FromBody] DecisionSearchRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, request.MatterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await service.DecideAsync(
            request with
            {
                TenantId = TenantId,
                UserId = ActorUserId,
                GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User)
            },
            cancellationToken));
    }

    // Apply ONE attorney-reviewed retrieved proposition through the shared LPI integration funnel.
    // Retrieval supplies the proposition; the shared service validates, optionally LPI-initializes
    // (CONTEXT_ONLY excluded), commits atomically, and enqueues the existing POLOXI reassessment.
    // POLOXI Core alone scores candidates and selects the winner.
    [HttpPost("propositions/integrate")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> IntegrateProposition([FromBody] IntegrateReviewedPropositionRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, request.MatterId, null, cancellationToken);
        if (denied is not null) return denied;

        var proposition = new RetrievedProposition(
            request.ProposalId,
            request.MatterId,
            request.DocumentVersionId,
            request.SourceLocator,
            request.SourceText,
            request.PropositionText,
            request.AssertionType,
            request.AttributedTo,
            request.EffectiveAt);

        var context = new LpiIntegrationContext(
            TenantId,
            ActorUserId,
            request.MatterId,
            request.DecisionContractRevision,
            request.CandidateSetRevision,
            request.HierarchyRevision,
            request.DocumentVersionId,
            ActorUserId,
            request.ScoringConfigurationVersion,
            request.IdempotencyKey);

        var result = await propositionIntegrationService.ApplyAsync(
            proposition, request.Placements, context, LpiOperationKind.Add, cancellationToken);
        return Ok(result);
    }

    // Document-Retrieval review queue: every parked, non-terminal retrieved proposition awaiting
    // attorney action for a matter. Feeds the Retrieved Proposition Review panel. Review is read-only;
    // no scoring happens until a proposition is accepted through the shared funnel.
    [HttpGet("matters/{matterId:guid}/propositions/review")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetPendingPropositionReview(Guid matterId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await retrievalReviewService.GetPendingAsync(TenantId, matterId, cancellationToken));
    }

    // Accept a reviewed retrieved proposition (optionally a reviewed subset of its placements) and route
    // it through the SHARED integration funnel. The returned reassessment status (Applied | Idempotent |
    // Rejected | EvaluationPending | EvaluationFailed) lets the UI avoid showing the old ranking as current.
    [HttpPost("matters/{matterId:guid}/propositions/review/{propositionId:guid}/accept")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> AcceptPropositionReview(Guid matterId, Guid propositionId, [FromBody] AcceptRetrievedPropositionRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        var revisions = await decisionRevisionResolver.ResolveAsync(TenantId, matterId, cancellationToken);
        var result = await retrievalReviewService.AcceptAsync(new LpiReviewAcceptRequest(
            TenantId,
            ActorUserId,
            propositionId,
            revisions.DecisionContractRevision,
            revisions.CandidateSetRevision,
            revisions.HierarchyRevision,
            revisions.ScoringConfigurationVersion,
            request.AcceptedPlacementTargetNodeIds), cancellationToken);
        return Ok(result);
    }

    // Terminal reject: preserve the proposition but mark it Rejected with the reviewer reason. No change
    // event, no reassessment.
    [HttpPost("matters/{matterId:guid}/propositions/review/{propositionId:guid}/reject")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> RejectPropositionReview(Guid matterId, Guid propositionId, [FromBody] RejectRetrievedPropositionRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        await retrievalReviewService.RejectAsync(new LpiReviewRejectRequest(
            TenantId, ActorUserId, propositionId, request.Reason), cancellationToken);
        return NoContent();
    }

    // Revise an ACCEPTED proposition: supersede it with a corrected one through the SHARED funnel. The
    // prior proposition is preserved (state Superseded); the correction drives a fresh reassessment.
    [HttpPost("matters/{matterId:guid}/propositions/review/{propositionId:guid}/revise")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> RevisePropositionReview(Guid matterId, Guid propositionId, [FromBody] ReviseRetrievedPropositionRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        var revisions = await decisionRevisionResolver.ResolveAsync(TenantId, matterId, cancellationToken);
        var result = await retrievalReviewService.ReviseAsync(new LpiReviewReviseRequest(
            TenantId,
            ActorUserId,
            propositionId,
            request.PropositionText,
            revisions.DecisionContractRevision,
            revisions.CandidateSetRevision,
            revisions.HierarchyRevision,
            revisions.ScoringConfigurationVersion,
            request.Placements,
            request.Reason), cancellationToken);
        return Ok(result);
    }

    // Withdraw an ACCEPTED proposition: retract its contribution through the SHARED funnel so POLOXI Core
    // recompetes without it. The source record is preserved (state Withdrawn); nothing is deleted.
    [HttpPost("matters/{matterId:guid}/propositions/review/{propositionId:guid}/withdraw")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> WithdrawPropositionReview(Guid matterId, Guid propositionId, [FromBody] WithdrawRetrievedPropositionRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        var revisions = await decisionRevisionResolver.ResolveAsync(TenantId, matterId, cancellationToken);
        var result = await retrievalReviewService.WithdrawAsync(new LpiReviewWithdrawRequest(
            TenantId,
            ActorUserId,
            propositionId,
            revisions.DecisionContractRevision,
            revisions.CandidateSetRevision,
            revisions.HierarchyRevision,
            revisions.ScoringConfigurationVersion,
            request.Reason), cancellationToken);
        return Ok(result);
    }

    // Run ONE Document-Retrieval pass for a matter: enumerate the passages worth processing, extract
    // atomic propositions, and PARK each for attorney review. Proposal-only — it never scores, applies,
    // or ranks; acceptance still flows through the shared funnel and POLOXI Core remains the sole authority.
    [HttpPost("matters/{matterId:guid}/propositions/retrieval/run")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> RunRetrievalPass(Guid matterId, [FromBody] RunRetrievalPassRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        var mode = Enum.TryParse<LpiRetrievalMode>(request.Mode, ignoreCase: true, out var parsedMode)
            ? parsedMode
            : LpiRetrievalMode.ConditionDirected;

        var revisions = await decisionRevisionResolver.ResolveAsync(TenantId, matterId, cancellationToken);
        var result = await retrievalOrchestrationService.RunAsync(new RetrievalOrchestrationRequest(
            TenantId,
            ActorUserId,
            matterId,
            mode,
            request.DecisionQuestion,
            revisions.DecisionContractRevision,
            revisions.CandidateSetRevision,
            revisions.HierarchyRevision,
            revisions.ScoringConfigurationVersion,
            request.CorrelationId ?? Guid.NewGuid().ToString("N"),
            request.RetrievalQuery,
            request.DocumentVersionIds,
            request.MaxPassages), cancellationToken);
        return Ok(result);
    }

    // Run ONE LegalAuthority pass for a matter: match every VERIFIED legal-authority evidence item to an
    // authoritative hierarchy node and PARK each as a proposition for attorney review. Proposal-only — it
    // never scores, applies, or ranks; acceptance flows through the SAME shared funnel as Document
    // Retrieval and Media Evidence, and POLOXI Core remains the sole competition authority.
    [HttpPost("matters/{matterId:guid}/propositions/authority/run")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> RunAuthorityPass(Guid matterId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        var revisions = await decisionRevisionResolver.ResolveAsync(TenantId, matterId, cancellationToken);
        var result = await legalAuthorityOrchestrationService.RunAsync(new LegalAuthorityOrchestrationRequest(
            TenantId,
            ActorUserId,
            matterId,
            revisions.DecisionContractRevision,
            revisions.CandidateSetRevision,
            revisions.HierarchyRevision,
            revisions.ScoringConfigurationVersion), cancellationToken);
        return Ok(result);
    }

    // Database-backed model options for the decision Model dropdown.
    [HttpGet("models")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Models(CancellationToken cancellationToken)
        => Ok(await service.GetModelsAsync(TenantId, cancellationToken));

    // Database-backed context options for the decision Context dropdown (POLOXI.Legal_DecisionContext).
    [HttpGet("contexts")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Contexts(CancellationToken cancellationToken)
        => Ok(await service.GetContextsAsync(TenantId, cancellationToken));

    // ── Configuration Mode admin surface (DB-backed execution settings per mode) ──────────────────
    // Read is available to anyone who can run decisions; write requires the Intelligence.Configure policy.
    [HttpGet("execution-modes")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ExecutionModes(CancellationToken cancellationToken)
        => Ok(await service.GetExecutionModesAsync(TenantId, cancellationToken));

    [HttpPut("execution-modes/{executionModeCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveExecutionMode(string executionModeCode, [FromBody] SaveDecisionExecutionModeRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(executionModeCode, request.ExecutionModeCode, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Execution mode code in the route must match the request body.");

        await service.SaveExecutionModeAsync(TenantId, ActorUserId, request, cancellationToken);
        return NoContent();
    }

    // ── Matter dashboard / cockpit ──────────────────────────────────────────────────────────────
    [HttpGet("matters")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Matters(CancellationToken cancellationToken)
        => Ok(await service.GetMattersAsync(TenantId, AuthenticatedRequestContext.IsSystemAdmin(User), cancellationToken));

    // Distinct free-form facet values (matter type / jurisdiction / posture) to pre-populate dropdowns.
    [HttpGet("matters/facets")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterFacets(CancellationToken cancellationToken)
        => Ok(await service.GetMatterFacetsAsync(TenantId, cancellationToken));

    // Database-backed Domain Pack (practice-area domain semantics) for the decision cockpit.
    [HttpGet("domainpacks")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> DomainPacks(CancellationToken cancellationToken)
        => Ok(await service.GetDomainPacksAsync(TenantId, cancellationToken));

    [HttpGet("domainpacks/{packCode}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> DomainPack(string packCode, CancellationToken cancellationToken)
    {
        var pack = await service.GetDomainPackAsync(TenantId, packCode, cancellationToken);
        return pack is null ? NotFound() : Ok(pack);
    }

    // ── Domain Pack CRUD (tenant-scoped writes; global seed rows immutable) ──
    [HttpPut("domainpacks")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDomainPack([FromBody] SaveDomainPackRequest request, CancellationToken cancellationToken)
    {
        await service.SaveDomainPackAsync(TenantId, ActorUserId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("domainpacks/{packCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteDomainPack(string packCode, CancellationToken cancellationToken)
    {
        await service.DeleteDomainPackAsync(TenantId, ActorUserId, packCode, cancellationToken);
        return NoContent();
    }

    [HttpPut("domainpacks/{packCode}/dimensions")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDomainPackDimension(string packCode, [FromBody] SaveDomainPackDimensionRequest request, CancellationToken cancellationToken)
    {
        await service.SaveDomainPackDimensionAsync(TenantId, ActorUserId, packCode, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("domainpacks/{packCode}/dimensions/{dimensionCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteDomainPackDimension(string packCode, string dimensionCode, CancellationToken cancellationToken)
    {
        await service.DeleteDomainPackDimensionAsync(TenantId, ActorUserId, packCode, dimensionCode, cancellationToken);
        return NoContent();
    }

    [HttpPut("domainpacks/{packCode}/evidencetypes")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDomainPackEvidenceType(string packCode, [FromBody] SaveDomainPackEvidenceTypeRequest request, CancellationToken cancellationToken)
    {
        await service.SaveDomainPackEvidenceTypeAsync(TenantId, ActorUserId, packCode, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("domainpacks/{packCode}/evidencetypes/{evidenceTypeCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteDomainPackEvidenceType(string packCode, string evidenceTypeCode, CancellationToken cancellationToken)
    {
        await service.DeleteDomainPackEvidenceTypeAsync(TenantId, ActorUserId, packCode, evidenceTypeCode, cancellationToken);
        return NoContent();
    }

    [HttpPut("domainpacks/{packCode}/verificationprofiles")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDomainPackVerificationProfile(string packCode, [FromBody] SaveDomainPackVerificationProfileRequest request, CancellationToken cancellationToken)
    {
        await service.SaveDomainPackVerificationProfileAsync(TenantId, ActorUserId, packCode, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("domainpacks/{packCode}/verificationprofiles/{profileCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteDomainPackVerificationProfile(string packCode, string profileCode, CancellationToken cancellationToken)
    {
        await service.DeleteDomainPackVerificationProfileAsync(TenantId, ActorUserId, packCode, profileCode, cancellationToken);
        return NoContent();
    }

    [HttpPut("domainpacks/{packCode}/mattertypes")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDomainPackMatterType(string packCode, [FromBody] SaveDomainPackMatterTypeRequest request, CancellationToken cancellationToken)
    {
        await service.SaveDomainPackMatterTypeAsync(TenantId, ActorUserId, packCode, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("domainpacks/{packCode}/mattertypes/{matterTypeCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteDomainPackMatterType(string packCode, string matterTypeCode, CancellationToken cancellationToken)
    {
        await service.DeleteDomainPackMatterTypeAsync(TenantId, ActorUserId, packCode, matterTypeCode, cancellationToken);
        return NoContent();
    }

    [HttpPut("domainpacks/{packCode}/concepts")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDomainPackConcept(string packCode, [FromBody] SaveDomainPackConceptRequest request, CancellationToken cancellationToken)
    {
        await service.SaveDomainPackConceptAsync(TenantId, ActorUserId, packCode, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("domainpacks/{packCode}/concepts/{conceptCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteDomainPackConcept(string packCode, string conceptCode, CancellationToken cancellationToken)
    {
        await service.DeleteDomainPackConceptAsync(TenantId, ActorUserId, packCode, conceptCode, cancellationToken);
        return NoContent();
    }

    [HttpPut("domainpacks/{packCode}/conceptrelations")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDomainPackConceptRelation(string packCode, [FromBody] SaveDomainPackConceptRelationRequest request, CancellationToken cancellationToken)
    {
        await service.SaveDomainPackConceptRelationAsync(TenantId, ActorUserId, packCode, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("domainpacks/{packCode}/conceptrelations/{sourceConceptCode}/{targetConceptCode}/{relationTypeCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteDomainPackConceptRelation(string packCode, string sourceConceptCode, string targetConceptCode, string relationTypeCode, CancellationToken cancellationToken)
    {
        await service.DeleteDomainPackConceptRelationAsync(TenantId, ActorUserId, packCode, sourceConceptCode, targetConceptCode, relationTypeCode, cancellationToken);
        return NoContent();
    }

    [HttpPut("domainpacks/{packCode}/outcomecandidates")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveDomainPackOutcomeCandidate(string packCode, [FromBody] SaveDomainPackOutcomeCandidateRequest request, CancellationToken cancellationToken)
    {
        await service.SaveDomainPackOutcomeCandidateAsync(TenantId, ActorUserId, packCode, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("domainpacks/{packCode}/outcomecandidates/{outcomeCode}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteDomainPackOutcomeCandidate(string packCode, string outcomeCode, CancellationToken cancellationToken)
    {
        await service.DeleteDomainPackOutcomeCandidateAsync(TenantId, ActorUserId, packCode, outcomeCode, cancellationToken);
        return NoContent();
    }

    [HttpGet("matters/{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Matter(Guid matterId, CancellationToken cancellationToken)
    {
        var matter = await service.GetMatterAsync(TenantId, matterId, cancellationToken);
        return matter is null ? NotFound() : Ok(matter);
    }

    [HttpPost("matters")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CreateMatter([FromBody] DecisionMatterCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await service.CreateMatterAsync(TenantId, ActorUserId, request, cancellationToken);
            return Ok(new { DecisionMatterId = id });
        }
        catch (DuplicateMatterException ex)
        {
            // Blocked duplicate: surface a clean 409 so the UI shows a friendly message.
            return Conflict(ex.Message);
        }
    }

    // Full decision-session history for a matter (dashboard/detail history list).
    [HttpGet("matters/{matterId:guid}/sessions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterSessions(Guid matterId, CancellationToken cancellationToken)
        => Ok(await service.GetMatterSessionsAsync(TenantId, matterId, cancellationToken));

    // Composed, DB-backed Candidate Full Analysis read model for one candidate (1-based rank index).
    [HttpGet("matters/{matterId:guid}/candidate-analysis/{candidateIndex:int}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CandidateFullAnalysis(Guid matterId, int candidateIndex, CancellationToken cancellationToken)
    {
        var analysis = await service.GetCandidateFullAnalysisAsync(TenantId, matterId, candidateIndex, cancellationToken);
        return analysis is null ? NotFound() : Ok(analysis);
    }

    // Read-only Channel Scoring LPI trace: how each verified channel contribution's typed δ was folded
    // into the latest decision session's POLOXI candidate competition. Returns an honest empty model when
    // the matter has no session / no channel contributions. POLOXI remains the sole scorer; this only explains.
    [HttpGet("matters/{matterId:guid}/channel-scoring")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterChannelScoring(Guid matterId, CancellationToken cancellationToken)
    {
        var matter = await service.GetMatterAsync(TenantId, matterId, cancellationToken);
        if (matter is null)
            return NotFound();

        if (matter.LatestSessionId is not { } sessionId)
            return Ok(Legal.Application.Features.Intelligence.Decision.Channels.ChannelScoringLpiReadModel.Empty(matterId));

        var trace = await channelScoringLpiService.GetForSessionAsync(TenantId, sessionId, matterId, cancellationToken);
        return Ok(trace);
    }

    // Read-only matter scoring trace: the hierarchy levels (L1..Ln), named branches/candidates and exact
    // persisted scoring values from the matter's latest Wide execution. Explains the formulas, values,
    // levels and names that produced the outcome when there is no DecisionSession channel contribution yet.
    [HttpGet("matters/{matterId:guid}/scoring-trace")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterScoringTrace(Guid matterId, CancellationToken cancellationToken)
    {
        var matter = await service.GetMatterAsync(TenantId, matterId, cancellationToken);
        if (matter is null)
            return NotFound();

        var trace = await matterScoringTraceReader.GetLatestForMatterAsync(TenantId, matterId, cancellationToken);
        return Ok(trace);
    }

    [HttpGet("matters/{matterId:guid}/documents")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterDocuments(Guid matterId, CancellationToken cancellationToken)
        => Ok(await documentCorpusRepository.GetMatterDocumentsAsync(TenantId, matterId, cancellationToken));

    // Matter-scoped evidence ↔ proposition graph for the Document Intelligence workspace.
    [HttpGet("matters/{matterId:guid}/evidence-graph")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterEvidenceGraph(Guid matterId, CancellationToken cancellationToken)
        => Ok(await documentCorpusRepository.GetMatterEvidenceGraphAsync(TenantId, matterId, cancellationToken));

    // Appends an immutable, hash-verified source anchor (POLOXI.Legal_SourceAssertion) that pins an
    // evidence item and/or proposition-support edge to an exact character span in a document passage.
    [HttpPost("matters/{matterId:guid}/source-assertions")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> AppendSourceAssertion(Guid matterId, [FromBody] LegalSourceAssertionCreateRequest request, CancellationToken cancellationToken)
    {
        if (matterId != request.MatterId)
            return BadRequest("Matter id in the route must match the request body.");
        var assertionId = await documentCorpusRepository.AppendSourceAssertionAsync(TenantId, ActorUserId, request, cancellationToken);
        return Ok(new { LegalSourceAssertionId = assertionId });
    }

    // Attorney Decision Input (Human Intelligence): DB-backed canonical decision nodes with attorney
    // relative assessments and the single approved matter assessment per node. Read-only (Phase 1).
    [HttpGet("matters/{matterId:guid}/human-intelligence")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterHumanIntelligence(Guid matterId, CancellationToken cancellationToken)
        => Ok(await attorneyDecisionInputRepository.GetMatterHumanIntelligenceAsync(TenantId, matterId, cancellationToken));

    // ── Decision Contract (first-class, versioned problem specification) ────────────────────────────
    // The authoritative attorney-defined decision boundary above POLOXI. Auto-provisions a DRAFT on
    // first open; edits are section commands with optimistic concurrency; status changes only through
    // governed lifecycle commands. POLOXI remains the single authoritative evaluator.

    [HttpGet("matters/{matterId:guid}/decision-contract")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetDecisionContract(Guid matterId, CancellationToken cancellationToken)
        => Ok(await decisionContractService.GetWorkspaceAsync(TenantId, ActorUserId, matterId, cancellationToken));

    [HttpGet("decision-contract/options")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetDecisionContractOptions(CancellationToken cancellationToken)
        => Ok(await decisionContractService.GetOptionsAsync(TenantId, cancellationToken));

    [HttpPut("matters/{matterId:guid}/decision-contract/decision")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> UpdateDecisionContractDecision(Guid matterId, [FromBody] DecisionContractDecisionCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.UpdateDecisionAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPut("matters/{matterId:guid}/decision-contract/legal-context")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> UpdateDecisionContractLegalContext(Guid matterId, [FromBody] DecisionContractLegalContextCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.UpdateLegalContextAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPut("matters/{matterId:guid}/decision-contract/burden")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> UpdateDecisionContractBurden(Guid matterId, [FromBody] DecisionContractBurdenCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.UpdateBurdenAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPut("matters/{matterId:guid}/decision-contract/boundaries")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> UpdateDecisionContractBoundaries(Guid matterId, [FromBody] DecisionContractBoundariesCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.UpdateBoundariesAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPut("matters/{matterId:guid}/decision-contract/candidates")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> UpdateDecisionContractCandidates(Guid matterId, [FromBody] DecisionContractCandidatesCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.UpdateCandidatesAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPut("matters/{matterId:guid}/decision-contract/settings")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> UpdateDecisionContractSettings(Guid matterId, [FromBody] DecisionContractSettingsCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.UpdateSettingsAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPost("matters/{matterId:guid}/decision-contract/submit")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> SubmitDecisionContract(Guid matterId, [FromBody] DecisionContractLifecycleCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.SubmitAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPost("matters/{matterId:guid}/decision-contract/approve")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> ApproveDecisionContract(Guid matterId, [FromBody] DecisionContractLifecycleCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.ApproveAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPost("matters/{matterId:guid}/decision-contract/return")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> ReturnDecisionContract(Guid matterId, [FromBody] DecisionContractLifecycleCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.ReturnAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPost("matters/{matterId:guid}/decision-contract/activate")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> ActivateDecisionContract(Guid matterId, [FromBody] DecisionContractLifecycleCommand command, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.ActivateAsync(TenantId, ActorUserId, matterId, command, cancellationToken));

    [HttpPost("matters/{matterId:guid}/decision-contract/{decisionContractId:guid}/new-version")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public Task<IActionResult> NewDecisionContractVersion(Guid matterId, Guid decisionContractId, CancellationToken cancellationToken)
        => RunContract(matterId, () => decisionContractService.CreateNewVersionAsync(TenantId, ActorUserId, matterId, decisionContractId, cancellationToken));

    private async Task<IActionResult> RunContract(Guid matterId, Func<Task<DecisionContractWorkspaceDto>> action)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, HttpContext.RequestAborted);
        if (denied is not null) return denied;
        try
        {
            return Ok(await action());
        }
        catch (DecisionContractConcurrencyException ex)
        {
            return Conflict(new { code = "CONCURRENCY_CONFLICT", message = ex.Message });
        }
        catch (DecisionContractStateException ex)
        {
            return BadRequest(new { code = "STATE_ERROR", message = ex.Message });
        }
    }


    // ── Attorney Decision Input write path (§19/§20) ─────────────────────────────────────────────
    // Add-Proposition workflow: Define → Placement → Preview → Confirm, plus node-scoped
    // assessment/approval/challenge/reposition. POLOXI remains the single authoritative evaluator.

    // Define — validate + build a draft with deterministic (and, when available, AI) structural checks.
    [HttpPost("matters/{matterId:guid}/decision-input/drafts")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CreateAttorneyDraft(Guid matterId, [FromBody] CreateAttorneyInputCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await attorneyDecisionInputService.CreateDraftAsync(TenantId, ActorUserId, command with { MatterId = matterId }, cancellationToken));
    }

    // Placement — suggested midpoint + neighbor bounds for the chosen position (§4).
    [HttpPost("matters/{matterId:guid}/decision-input/placement-analysis")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> AnalyzeAttorneyPlacement(Guid matterId, [FromBody] AnalyzePlacementCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await attorneyDecisionInputService.AnalyzePlacementAsync(TenantId, command with { MatterId = matterId }, cancellationToken));
    }

    // Preview — non-committing projection through the existing POLOXI scoring path (§15).
    [HttpPost("matters/{matterId:guid}/decision-input/preview")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> PreviewAttorneyInput(Guid matterId, [FromBody] PreviewAttorneyInputCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await attorneyDecisionInputService.PreviewAsync(TenantId, ActorUserId, command with { MatterId = matterId }, cancellationToken));
    }

    // Confirm/Commit — node + assessment (+optional approval) + edges + audit + outbox in one tx (§16).
    [HttpPost("matters/{matterId:guid}/decision-input/commit")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CommitAttorneyInput(Guid matterId, [FromBody] CommitAttorneyInputCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        try
        {
            return Ok(await attorneyDecisionInputService.CommitAsync(TenantId, ActorUserId, command with { MatterId = matterId }, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Retract/Undo — soft-delete a committed attorney-supplied node and queue POLOXI recompute (§16/§23).
    [HttpDelete("matters/{matterId:guid}/decision-input/nodes/{nodeId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> RetractAttorneyInput(Guid matterId, Guid nodeId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        var command = new RetractAttorneyInputCommand(matterId, nodeId, Guid.NewGuid());
        return Ok(await attorneyDecisionInputService.RetractAsync(TenantId, ActorUserId, command, cancellationToken));
    }

    // Submit a node-scoped relative assessment (multi-attorney; never auto-averaged) (§5).
    [HttpPost("matters/{matterId:guid}/nodes/{nodeId:guid}/attorney-assessments")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> SubmitAttorneyAssessment(Guid matterId, Guid nodeId, [FromBody] SubmitAttorneyAssessmentCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await attorneyDecisionInputService.SubmitAssessmentAsync(TenantId, ActorUserId, command with { MatterId = matterId, DecisionNodeId = nodeId }, cancellationToken));
    }

    // Approve a specific assessment as the single active Approved Matter Assessment for the node (§5).
    [HttpPost("matters/{matterId:guid}/nodes/{nodeId:guid}/assessment-approval")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ApproveMatterAssessment(Guid matterId, Guid nodeId, [FromBody] ApproveMatterAssessmentCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await attorneyDecisionInputService.ApproveAssessmentAsync(TenantId, ActorUserId, command with { MatterId = matterId, DecisionNodeId = nodeId }, cancellationToken));
    }

    // Raise a challenge against a node or relationship (§3).
    [HttpPost("matters/{matterId:guid}/nodes/{nodeId:guid}/challenge")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> RaiseAttorneyChallenge(Guid matterId, Guid nodeId, [FromBody] RaiseChallengeCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(new { ChallengeId = await attorneyDecisionInputService.RaiseChallengeAsync(TenantId, ActorUserId, command with { MatterId = matterId, DecisionNodeId = nodeId }, cancellationToken) });
    }

    // Reposition an existing node — creates a new version and DecisionDelta (§4).
    [HttpPost("matters/{matterId:guid}/nodes/{nodeId:guid}/reposition")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> RepositionAttorneyNode(Guid matterId, Guid nodeId, [FromBody] RepositionNodeCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        try
        {
            return Ok(await attorneyDecisionInputService.RepositionAsync(TenantId, ActorUserId, command with { MatterId = matterId, DecisionNodeId = nodeId }, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }


    // Resolve-or-create the decision node for a selected live Wide hierarchy branch (and ancestor chain)
    // so an attorney can add a proposition from the Hierarchy tab's surviving branches (§2/§7). Idempotent.
    [HttpPost("matters/{matterId:guid}/decision-input/resolve-branch-node")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ResolveBranchNode(Guid matterId, [FromBody] ResolveBranchNodeCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await attorneyDecisionInputService.ResolveBranchNodeAsync(TenantId, ActorUserId, command with { MatterId = matterId }, cancellationToken));
    }


    // Advisory proposition-level Information Value: scores each atomic matter fact-proposition on POLOXI's
    // shared VIV scale (reusing ClaimVerificationPrioritizer) so the cockpit can surface which propositions
    // are most worth investigating next. Display-only — it never blocks or alters the authoritative decision.
    // persist:false — this is a read-only GET the Overview page calls on every load/return; persisting claim
    // upserts here would silently re-process (re-write) the matter every time the user navigates back.
    [HttpGet("matters/{matterId:guid}/sessions/{sessionId:guid}/proposition-information-value")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterPropositionInformationValue(Guid matterId, Guid sessionId, CancellationToken cancellationToken)
        => Ok(await propositionInformationValueService.ScoreAsync(TenantId, matterId, sessionId, ActorUserId, persist: false, cancellationToken));

    // Next Best Action: advisory, decision-directed actions that resolve the highest-value unresolved
    // propositions. POLOXI selects what matters, HRR supplies where/why, the Domain Pack supplies domain
    // actions, an LLM proposes and a deterministic gate selects. Display-only — never alters the decision.
    [HttpGet("matters/{matterId:guid}/sessions/{sessionId:guid}/next-best-actions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterNextBestActions(Guid matterId, Guid sessionId, CancellationToken cancellationToken)
        => Ok(await nextBestActionService.GenerateAsync(TenantId, matterId, sessionId, ActorUserId, cancellationToken));

    // What To Resolve Next: a thin projection over the SAME proposition VIV / LegalADV frontier. Returns
    // ranked unresolved decision-material propositions with four explicit states (Calculated,
    // NotCalculated, Blocked, NoneRequired). No new scoring and no operational action generation.
    [HttpGet("matters/{matterId:guid}/sessions/{sessionId:guid}/what-to-resolve-next")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterWhatToResolveNext(Guid matterId, Guid sessionId, CancellationToken cancellationToken)
        => Ok(await whatToResolveNextService.GetAsync(TenantId, matterId, sessionId, ActorUserId, cancellationToken));

    // Generates a randomized, source-traceable test corpus for a matter (used by "Generate Test Matter").
    // Idempotent: returns the number of documents created (0 when the matter already has documents).
    [HttpPost("matters/{matterId:guid}/generate-test-corpus")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GenerateTestCorpus(Guid matterId, CancellationToken cancellationToken)
        => Ok(new { DocumentsCreated = await documentCorpusRepository.GenerateRandomTestCorpusAsync(TenantId, ActorUserId, matterId, cancellationToken) });

    // ── Continuous Decision Integrity — Decision Change Review workspace ─────────────────────────
    // Tenant-wide list of matters with change activity (workspace landing list).
    [HttpGet("change-reviews")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ChangeReviews(CancellationToken cancellationToken)
        => Ok(await integrityRepository.GetChangeReviewSummariesAsync(TenantId, cancellationToken));

    // Immutable snapshot history for a matter (readiness at eval time + current reliance).
    [HttpGet("matters/{matterId:guid}/snapshots")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterSnapshots(Guid matterId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await integrityRepository.GetMatterSnapshotsAsync(TenantId, matterId, cancellationToken));
    }

    // All change events for a matter.
    [HttpGet("matters/{matterId:guid}/change-events")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterChangeEvents(Guid matterId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await integrityRepository.GetMatterChangeEventsAsync(TenantId, matterId, cancellationToken));
    }

    // Open review tasks for a matter.
    [HttpGet("matters/{matterId:guid}/review-tasks")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterReviewTasks(Guid matterId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await integrityRepository.GetOpenReviewTasksAsync(TenantId, matterId, cancellationToken));
    }

    // Composed before/after Decision Change Review for a single change event.
    [HttpGet("change-events/{changeEventId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ChangeReviewDetail(Guid changeEventId, CancellationToken cancellationToken)
    {
        var changeEvent = await integrityRepository.GetChangeEventAsync(TenantId, changeEventId, cancellationToken);
        if (changeEvent is null) return NotFound();
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, changeEvent.DecisionMatterId, null, cancellationToken);
        if (denied is not null) return denied;

        var impacts = await integrityRepository.GetImpactsForEventAsync(TenantId, changeEventId, cancellationToken);
        var reviewTasks = await integrityRepository.GetReviewTasksForEventAsync(TenantId, changeEventId, cancellationToken);
        var affectedSnapshot = changeEvent.DecisionMatterId == Guid.Empty
            ? null
            : await integrityRepository.GetLatestMatterSnapshotAsync(TenantId, changeEvent.DecisionMatterId, cancellationToken);
        return Ok(new DecisionChangeReviewDto(changeEvent, affectedSnapshot, impacts, reviewTasks));
    }

    // Decision Change Intelligence — first-class DecisionDelta "what changed since" timeline for a matter.
    [HttpGet("matters/{matterId:guid}/deltas")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterDecisionDeltas(Guid matterId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await integrityRepository.GetMatterDecisionDeltasAsync(TenantId, matterId, cancellationToken));
    }

    // The DecisionDelta produced by reevaluating a single change event (null if not yet reevaluated).
    [HttpGet("change-events/{changeEventId:guid}/delta")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ChangeEventDelta(Guid changeEventId, CancellationToken cancellationToken)
    {
        var delta = await integrityRepository.GetDecisionDeltaForEventAsync(TenantId, changeEventId, cancellationToken);
        return delta is null ? NotFound() : Ok(delta);
    }

    // Attorney action on a review task (acknowledge / resolve / dismiss). Never auto-executes
    // consequential changes; the attorney remains in control (Phase 3 governs replacing an approved decision).
    [HttpPut("review-tasks/{reviewTaskId:guid}/status")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> UpdateReviewTaskStatus(Guid reviewTaskId, [FromBody] UpdateReviewTaskStatusRequest request, CancellationToken cancellationToken)
    {
        var updated = await integrityRepository.UpdateReviewTaskStatusAsync(TenantId, ActorUserId, reviewTaskId, request.StatusCode, request.ResolutionNotes, cancellationToken);
        return updated ? NoContent() : NotFound();
    }


    [HttpPost("matters/{matterId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> UploadMatterDocument(
        Guid matterId,
        IFormFile file,
        [FromForm] string? documentTypeCode,
        [FromForm] string? domainPackCode,
        [FromForm] string? modelCode,
        [FromForm] string? idempotencyKey,
        [FromForm] Guid? uploadBatchId,
        [FromForm] string? sourceTypeCode,
        [FromForm] string? custodian,
        [FromForm] string? producedBy,
        [FromForm] string? productionId,
        [FromForm] string? batesStart,
        [FromForm] string? batesEnd,
        CancellationToken cancellationToken)
    {
        // Upload is prepare-only and must NOT consume the metered legal.decision run quota.
        // Gate on the matter-management capability instead; the expensive Stage 1 semantic
        // enrichment and Continuous Decision Integrity are metered later on corpus activation.
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        if (file.Length <= 0)
            return BadRequest("A non-empty document is required.");
        if (file.Length > documentOptions.Value.MaximumFileSizeBytes)
            return BadRequest($"The document exceeds the configured {documentOptions.Value.MaximumFileSizeBytes} byte limit.");

        await using var content = file.OpenReadStream();
        var request = new LegalDocumentIntakeRequest(
            TenantId, ActorUserId, matterId, Path.GetFileName(file.FileName),
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            file.Length, HttpContext.TraceIdentifier)
        {
            DocumentTypeCode = documentTypeCode,
            DomainPackCode = domainPackCode,
            ModelCode = modelCode,
            // Enterprise evidence-upload provenance (all optional; intake is unchanged when absent).
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey,
            UploadBatchId = uploadBatchId,
            Source = new EvidenceSourceDescriptor
            {
                SourceTypeCode = string.IsNullOrWhiteSpace(sourceTypeCode) ? "USER_UPLOAD" : sourceTypeCode,
                Custodian = custodian,
                ProducedBy = producedBy,
                ProductionId = productionId,
                BatesStart = batesStart,
                BatesEnd = batesEnd,
                OriginalPath = Path.GetFileName(file.FileName)
            },
            // Upload only prepares the document (validate, scan, store, persist, extract). The metered
            // Stage 1 semantic enrichment and Continuous Decision Integrity run later, on the
            // Disambiguate & Answer path, via corpus activation.
            PrepareOnly = true
        };
        return Ok(await documentIntakeService.IngestAsync(request, content, cancellationToken));
    }

    // Opens a new upload batch (one "add evidence" action) for a matter. Batch counters and provenance
    // occurrences are grouped under the returned batch id for the enterprise upload dashboard.
    [HttpPost("matters/{matterId:guid}/upload-batches")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> StartUploadBatch(Guid matterId, [FromBody] StartUploadBatchCommand command, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        var batchId = await documentCorpusRepository.CreateUploadBatchAsync(TenantId, ActorUserId, matterId, command, cancellationToken);
        return Ok(batchId);
    }

    // Read-only upload batch reports for the enterprise upload dashboard (newest first).
    [HttpGet("matters/{matterId:guid}/upload-batches")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterUploadBatches(Guid matterId, CancellationToken cancellationToken)
        => Ok(await documentCorpusRepository.GetUploadBatchesAsync(TenantId, matterId, cancellationToken));

    // Read-only legal provenance occurrences for a matter (same bytes can appear as multiple occurrences).
    [HttpGet("matters/{matterId:guid}/evidence-occurrences")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterEvidenceOccurrences(Guid matterId, CancellationToken cancellationToken)
        => Ok(await documentCorpusRepository.GetEvidenceOccurrencesAsync(TenantId, matterId, cancellationToken));

    // Declares how many files a batch expects to process, so the dashboard shows discovered-vs-accepted.
    [HttpPut("matters/{matterId:guid}/upload-batches/{batchId:guid}/discovered")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> SetUploadBatchDiscovered(Guid matterId, Guid batchId, [FromBody] SetBatchDiscoveredBody body, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        await documentCorpusRepository.SetUploadBatchDiscoveredAsync(TenantId, batchId, body.FilesDiscovered, cancellationToken);
        return NoContent();
    }

    // Closes a batch once its files are processed (idempotent). Default terminal status is CLOSED.
    [HttpPost("matters/{matterId:guid}/upload-batches/{batchId:guid}/close")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CloseUploadBatch(Guid matterId, Guid batchId, [FromBody] CloseBatchBody? body, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        await documentCorpusRepository.CloseUploadBatchAsync(TenantId, ActorUserId, batchId, body?.StatusCode ?? "CLOSED", cancellationToken);
        return NoContent();
    }

    // Read-only evidence lineage groups (independent-source identity) with their members for a matter.
    [HttpGet("matters/{matterId:guid}/evidence-lineage")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterEvidenceLineage(Guid matterId, CancellationToken cancellationToken)
        => Ok(await documentCorpusRepository.GetEvidenceLineageAsync(TenantId, matterId, cancellationToken));

    // Creates a lineage group representing one underlying independent source.
    [HttpPost("matters/{matterId:guid}/evidence-lineage")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CreateEvidenceLineageGroup(Guid matterId, [FromBody] CreateLineageGroupBody body, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        var groupId = await documentCorpusRepository.CreateEvidenceLineageGroupAsync(TenantId, ActorUserId, matterId, body.LineageLabel, body.OriginDescription, body.IndependenceBasisCode ?? "UNRESOLVED", cancellationToken);
        return Ok(groupId);
    }

    // Adds an occurrence to a lineage group as ORIGINAL (primary source) or DERIVATIVE (quotes/restates it).
    [HttpPost("matters/{matterId:guid}/evidence-lineage/{groupId:guid}/members")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> AddEvidenceLineageMember(Guid matterId, Guid groupId, [FromBody] AddLineageMemberBody body, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        var memberId = await documentCorpusRepository.AddEvidenceLineageMemberAsync(TenantId, ActorUserId, groupId, body.OccurrenceId, body.RoleCode ?? "ORIGINAL", body.DerivationNote, cancellationToken);
        return Ok(memberId);
    }

    // Creates a document family (email + attachments / archive + children) for a matter.
    [HttpPost("matters/{matterId:guid}/document-families")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CreateDocumentFamily(Guid matterId, [FromBody] CreateDocumentFamilyBody body, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        var familyId = await documentCorpusRepository.CreateDocumentFamilyAsync(TenantId, ActorUserId, matterId, body.FamilyLabel, body.ContainerTypeCode ?? "LOOSE", cancellationToken);
        return Ok(familyId);
    }

    // Attaches an occurrence to a family with its container position (depth/ordinal) and optional parent.
    [HttpPost("matters/{matterId:guid}/document-families/{familyId:guid}/members")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> LinkOccurrenceToFamily(Guid matterId, Guid familyId, [FromBody] LinkFamilyMemberBody body, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        await documentCorpusRepository.LinkOccurrenceToFamilyAsync(TenantId, ActorUserId, body.OccurrenceId, familyId, body.ParentOccurrenceId, body.FamilyDepth, body.FamilyOrdinal, cancellationToken);
        return NoContent();
    }

    public sealed record SetBatchDiscoveredBody(int FilesDiscovered);
    public sealed record CloseBatchBody(string? StatusCode);
    public sealed record CreateLineageGroupBody(string? LineageLabel, string? OriginDescription, string? IndependenceBasisCode);
    public sealed record AddLineageMemberBody(Guid OccurrenceId, string? RoleCode, string? DerivationNote);
    public sealed record CreateDocumentFamilyBody(string? FamilyLabel, string? ContainerTypeCode);
    public sealed record LinkFamilyMemberBody(Guid OccurrenceId, Guid? ParentOccurrenceId, int FamilyDepth, int FamilyOrdinal);

    // Activates prepared corpus documents (one bounded batch per call): runs Stage 1 semantic
    // enrichment and Continuous Decision Integrity for versions uploaded prepare-only. The answer
    // path calls this in a loop (one call per bounded batch), so it must NOT charge the metered
    // legal.decision.run meter per batch -- that would burn dozens of decision-run units per answer.
    // The single metered decision charge is applied on the 'decide' answer endpoint; activation is
    // gated on the non-metered 'legal.matters' capability.
    [HttpPost("matters/{matterId:guid}/corpus/activate")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ActivateMatterCorpus(
        Guid matterId,
        [FromQuery] string? modelCode,
        [FromQuery] int batchSize,
        CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await corpusActivationService.ActivateAsync(
            TenantId, ActorUserId, matterId, modelCode, batchSize <= 0 ? 3 : batchSize, cancellationToken));
    }

    // Read-only enrichment/activation state for a matter's corpus (prepared vs activated counts).
    [HttpGet("matters/{matterId:guid}/corpus/enrichment-status")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterCorpusEnrichmentStatus(Guid matterId, CancellationToken cancellationToken)
        => Ok(await corpusActivationService.GetStatusAsync(TenantId, matterId, cancellationToken));

    public sealed record PurgeMatterDocumentsBody(IReadOnlyCollection<Guid>? DocumentIds);

    // Hard-deletes document-derived evidence for a matter (single/multiple documents, or ALL when the
    // body has no DocumentIds), then synchronously recomputes: re-activation re-enriches any remaining
    // prepared documents and re-runs Continuous Decision Integrity so all associated scoring, evidence/
    // proposition states and decision statuses are refreshed. Document-evidence only; the matter and its
    // configuration are preserved. Gated on the non-metered 'legal.matters' capability like activation.
    [HttpDelete("matters/{matterId:guid}/corpus/documents")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> PurgeMatterDocuments(
        Guid matterId,
        [FromBody] PurgeMatterDocumentsBody? body,
        CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, JudzCapabilities.Matters, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        var purge = await documentCorpusRepository.PurgeMatterDocumentEvidenceAsync(
            TenantId, ActorUserId, matterId, new LegalMatterDocumentPurgeRequest(body?.DocumentIds), cancellationToken);

        // Synchronous recompute of all associated scoring/statuses for the surviving corpus.
        var recompute = await corpusActivationService.ActivateAsync(
            TenantId, ActorUserId, matterId, null, 3, cancellationToken);

        return Ok(new LegalMatterDocumentPurgeOutcome(purge, recompute));
    }

    [HttpGet("documents/versions/{documentVersionId:guid}/passages")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> DocumentPassages(Guid documentVersionId, CancellationToken cancellationToken)
    {
        var matterId = await documentCorpusRepository.GetDocumentMatterIdAsync(TenantId, documentVersionId, cancellationToken);
        if (matterId is null) return NotFound();
        return Ok(await documentCorpusRepository.GetDocumentPassagesAsync(TenantId, documentVersionId, cancellationToken));
    }

    [HttpGet("matters/{matterId:guid}/retrieval-telemetry")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterRetrievalTelemetry(Guid matterId, CancellationToken cancellationToken)
        => Ok(await documentCorpusRepository.GetRetrievalTelemetryAsync(TenantId, matterId, null, cancellationToken));

    [HttpPut("matters/{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> UpdateMatter(Guid matterId, [FromBody] DecisionMatterUpdateRequest request, CancellationToken cancellationToken)
    {
        var updated = await service.UpdateMatterAsync(TenantId, ActorUserId, matterId, request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpPost("matters/{matterId:guid}/status")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> UpdateMatterStatus(Guid matterId, [FromBody] DecisionMatterStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!DecisionMatterStatusCodes.IsValid(request.StatusCode))
            return BadRequest($"'{request.StatusCode}' is not a valid matter status.");
        var updated = await service.UpdateMatterStatusAsync(TenantId, ActorUserId, matterId, request.StatusCode, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("matters/{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> DeleteMatter(Guid matterId, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteMatterAsync(TenantId, ActorUserId, matterId, AuthenticatedRequestContext.IsSystemAdmin(User), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    // Decision-history timeline for a session (sourced from POLOXI.Legal_DecisionEvent).
    [HttpGet("sessions/{sessionId:guid}/timeline")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Timeline(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await service.GetSessionResultAsync(TenantId, sessionId, cancellationToken);
        if (session is null) return NotFound();
        return Ok(await service.GetTimelineAsync(TenantId, sessionId, cancellationToken));
    }

    [HttpGet("sessions/{sessionId:guid}/retrieval-telemetry")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> SessionRetrievalTelemetry(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await service.GetSessionResultAsync(TenantId, sessionId, cancellationToken);
        if (session is null) return NotFound();
        return Ok(await documentCorpusRepository.GetRetrievalTelemetryAsync(TenantId, null, sessionId, cancellationToken));
    }

    // Full persisted decision result for a session, including the V2 dependency graph when present.
    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Session(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await service.GetSessionResultAsync(TenantId, sessionId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // POLOXI Legal V2.1 — synchronous closed loop: apply one edge verification change and run
    // dependency propagation → Candidate×Branch recompetition → frontier/IV → ResearchNeed.
    [HttpPost("sessions/{sessionId:guid}/verify")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Verify(Guid sessionId, [FromBody] DecisionVerificationChangeRequest request, CancellationToken cancellationToken)
        => Ok(await service.ApplyVerificationChangeAsync(TenantId, ActorUserId, sessionId, request, cancellationToken));

    // POLOXI Bounded Research Loop — cockpit entrypoint ("Research this decision"). Runs the autonomous
    // Retrieval → Verification → Evidence Promotion → Dependency Update → Recompetition loop until an
    // explicit STOP condition. No-op (LOOP_DISABLED) unless the research-loop feature flag is enabled.
    [HttpPost("sessions/{sessionId:guid}/research")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Research(Guid sessionId, CancellationToken cancellationToken)
        => Ok(await service.RunResearchLoopAsync(TenantId, ActorUserId, sessionId, cancellationToken));

    // ── Personal Injury (Domain Pack: PERSONAL_INJURY) manual matter wizard ─────────────────────────
    // Database-backed PI option sets for the wizard/dashboard dropdowns.
    [HttpGet("personalinjury/options")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> PersonalInjuryOptions(CancellationToken cancellationToken)
        => Ok(await service.GetPersonalInjuryOptionsAsync(TenantId, cancellationToken));

    // Structured PI matter profile (1:1 extension + child aggregates) for a matter. Returns an empty
    // profile shell for a matter that has no PI data yet so the manual wizard always binds.
    [HttpGet("matters/{matterId:guid}/personalinjury")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> PersonalInjuryProfile(Guid matterId, CancellationToken cancellationToken)
    {
        var profile = await service.GetPersonalInjuryProfileAsync(TenantId, matterId, cancellationToken);
        return Ok(profile ?? new PersonalInjuryProfileDto(matterId));
    }

    // Manual wizard save — upserts the PI profile and replaces the child aggregates.
    [HttpPut("matters/{matterId:guid}/personalinjury")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> SavePersonalInjuryProfile(Guid matterId, [FromBody] PersonalInjuryProfileSaveRequest request, CancellationToken cancellationToken)
    {
        await service.SavePersonalInjuryProfileAsync(TenantId, ActorUserId, matterId, request, cancellationToken);
        return NoContent();
    }

    // ── Generate-New-Matter draft / provenance (scaffold; extraction wired later) ──────────────────
    // Creates a pending PI matter draft (proposed field values + provenance) for attorney review.
    [HttpPost("personalinjury/drafts")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CreatePersonalInjuryDraft([FromBody] PersonalInjuryMatterDraftCreateRequest request, CancellationToken cancellationToken)
    {
        var draftId = await service.CreatePersonalInjuryDraftAsync(TenantId, ActorUserId, request, cancellationToken);
        return Ok(new { DecisionPIMatterDraftId = draftId });
    }

    // Retrieves a PI matter draft with its proposed fields and provenance for the review UI.
    [HttpGet("personalinjury/drafts/{draftId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> PersonalInjuryDraft(Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await service.GetPersonalInjuryDraftAsync(TenantId, draftId, cancellationToken);
        return draft is null ? NotFound() : Ok(draft);
    }

    // Marks a draft confirmed once the attorney has created the authoritative matter from it.
    [HttpPost("personalinjury/drafts/{draftId:guid}/confirm/{matterId:guid}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ConfirmPersonalInjuryDraft(Guid draftId, Guid matterId, CancellationToken cancellationToken)
    {
        var confirmed = await service.MarkPersonalInjuryDraftConfirmedAsync(TenantId, ActorUserId, draftId, matterId, cancellationToken);
        return confirmed ? NoContent() : NotFound();
    }

    // ── PI Decision Intelligence (Domain Pack: PERSONAL_INJURY) ────────────────────────────────────
    // DB-backed PI decision types for the decision-type dropdown.
    [HttpGet("personalinjury/decisiontypes")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> PersonalInjuryDecisionTypes(CancellationToken cancellationToken)
        => Ok(await service.GetPersonalInjuryDecisionTypesAsync(TenantId, cancellationToken));

    // DB-backed PI stage → default decision-type map (drives the default decision-type selection).
    [HttpGet("personalinjury/stagedecisionmap")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> PersonalInjuryStageDecisionMap(CancellationToken cancellationToken)
        => Ok(await service.GetPersonalInjuryStageDecisionMapAsync(TenantId, cancellationToken));

    // Runs the PI decision context through the existing POLOXI decision pipeline (Core unchanged).
    // Enriches the query from the PI matter profile + decision type, then decides.
    [HttpPost("personalinjury/decide")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> DecidePersonalInjury([FromBody] PersonalInjuryDecisionContext context, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, context.DecisionMatterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await service.DecidePersonalInjuryAsync(
            TenantId, ActorUserId, context, AuthenticatedRequestContext.GetGrantedPermissions(User), cancellationToken));
    }
}

// Request body for POST api/legal_decision/propositions/integrate. Carries ONE attorney-reviewed
// retrieved proposition and its accepted placements plus the revision/idempotency context. TenantId,
// ActorUserId, and ReviewerUserId are derived from the authenticated context, never the client.
public sealed record IntegrateReviewedPropositionRequest(
    Guid ProposalId,
    Guid MatterId,
    Guid DocumentVersionId,
    string SourceLocator,
    string SourceText,
    string PropositionText,
    LpiAssertionType AssertionType,
    string? AttributedTo,
    DateTimeOffset? EffectiveAt,
    IReadOnlyList<LpiPlacementProposal> Placements,
    long DecisionContractRevision,
    long CandidateSetRevision,
    long HierarchyRevision,
    string ScoringConfigurationVersion,
    string IdempotencyKey);

// Request body for accepting a parked retrieved proposition. TenantId/ReviewerUserId come from the
// authenticated context; the proposition id comes from the route. AcceptedPlacementTargetNodeIds is an
// optional reviewed subset of the proposed placements (null/empty = accept all proposed placements).
public sealed record AcceptRetrievedPropositionRequest(
    IReadOnlyList<Guid>? AcceptedPlacementTargetNodeIds = null);

// Request body for rejecting a parked retrieved proposition. The proposition is preserved, not deleted.
public sealed record RejectRetrievedPropositionRequest(string Reason);

// Request body for revising an ACCEPTED proposition. The prior proposition (route id) is superseded by a
// corrected one. Placements optionally change the node/relationship; when null the originals are reused.
public sealed record ReviseRetrievedPropositionRequest(
    string PropositionText,
    IReadOnlyList<LpiReviewPlacementEdit>? Placements = null,
    string? Reason = null);

// Request body for withdrawing an ACCEPTED proposition. The source is preserved (state Withdrawn).
public sealed record WithdrawRetrievedPropositionRequest(
    string Reason);

// Request body for running one Document-Retrieval pass. Mode is parsed to LpiRetrievalMode
// (ConditionDirected | DocumentDirected); unknown/empty falls back to ConditionDirected.
public sealed record RunRetrievalPassRequest(
    string Mode,
    string DecisionQuestion,
    string? CorrelationId = null,
    string? RetrievalQuery = null,
    IReadOnlyList<Guid>? DocumentVersionIds = null,
    int MaxPassages = 50);
