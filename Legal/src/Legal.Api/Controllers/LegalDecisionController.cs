using Legal.Api.Security;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Epistemic;
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
public sealed class LegalDecisionController(ILegalDecisionService service,IIntelligenceExecutionService executionService,ILegalDocumentCorpusRepository documentCorpusRepository,ILegalDocumentIntakeService documentIntakeService,ILegalMatterCorpusActivationService corpusActivationService,IDecisionIntegrityRepository integrityRepository,IMatterPropositionInformationValueService propositionInformationValueService,IOptions<DocumentIntelligenceOptions> documentOptions) : ControllerBase
{
    private const string CapabilityCode = JudzCapabilities.LegalDecision;
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("decide")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Decide([FromBody] DecisionSearchRequest request, CancellationToken cancellationToken)
    {
        // Honor a client-supplied Idempotency-Key so a retried answer (network retry, double submit)
        // reuses the original governed execution instead of committing a second legal.decision.run unit.
        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var key) && !string.IsNullOrWhiteSpace(key)
            ? key.ToString()
            : null;
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, request.MatterId, idempotencyKey, cancellationToken);
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
        var id = await service.CreateMatterAsync(TenantId, ActorUserId, request, cancellationToken);
        return Ok(new { DecisionMatterId = id });
    }

    // Full decision-session history for a matter (dashboard/detail history list).
    [HttpGet("matters/{matterId:guid}/sessions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterSessions(Guid matterId, CancellationToken cancellationToken)
        => Ok(await service.GetMatterSessionsAsync(TenantId, matterId, cancellationToken));

    [HttpGet("matters/{matterId:guid}/documents")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterDocuments(Guid matterId, CancellationToken cancellationToken)
        => Ok(await documentCorpusRepository.GetMatterDocumentsAsync(TenantId, matterId, cancellationToken));

    // Matter-scoped evidence ↔ proposition graph for the Document Intelligence workspace.
    [HttpGet("matters/{matterId:guid}/evidence-graph")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterEvidenceGraph(Guid matterId, CancellationToken cancellationToken)
        => Ok(await documentCorpusRepository.GetMatterEvidenceGraphAsync(TenantId, matterId, cancellationToken));

    // Advisory proposition-level Information Value: scores each atomic matter fact-proposition on POLOXI's
    // shared VIV scale (reusing ClaimVerificationPrioritizer) so the cockpit can surface which propositions
    // are most worth investigating next. Display-only — it never blocks or alters the authoritative decision.
    [HttpGet("matters/{matterId:guid}/sessions/{sessionId:guid}/proposition-information-value")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> MatterPropositionInformationValue(Guid matterId, Guid sessionId, CancellationToken cancellationToken)
        => Ok(await propositionInformationValueService.ScoreAsync(TenantId, matterId, sessionId, ActorUserId, persist: true, cancellationToken));

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
            // Upload only prepares the document (validate, scan, store, persist, extract). The metered
            // Stage 1 semantic enrichment and Continuous Decision Integrity run later, on the
            // Disambiguate & Answer path, via corpus activation.
            PrepareOnly = true
        };
        return Ok(await documentIntakeService.IngestAsync(request, content, cancellationToken));
    }

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
        var deleted = await service.DeleteMatterAsync(TenantId, ActorUserId, matterId, cancellationToken);
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
