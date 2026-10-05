using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Lpi;
using Legal.Application.Features.Intelligence.Decision.Media;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision.Media;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// MediaEvidenceOrchestrationService — the Media & Machine Evidence → proposition SENDING half (Phase 3).
//
// For one stored asset version it: (1) creates an idempotent MediaProcessingRun; (2) runs the capability-
// aware processor (PHOTO → image analysis, AUDIO → timestamped transcription); (3) on CapabilityUnavailable
// leaves the asset for manual annotation WITHOUT fabricating anything; (4) stores transcript/observation
// derivatives; (5) runs the DB-backed MEDIA_EXTRACTION_V1 prompt over the media-derived content to produce
// atomic proposition proposals + proposed placements, each attached to an EXACT anchor; (6) VALIDATES each
// anchor against the asset version; (7) PARKS each proposition via the shared integration repository and
// records a PropositionEvidenceLink. Acceptance flows later through IRetrievalPropositionReviewService →
// IPropositionIntegrationService (the SAME funnel as Document Retrieval / manual ADI). Never scores.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class MediaEvidenceOrchestrationService(
    ILegalDecisionRepository decisionRepository,
    ILegalHierarchyExecutionRepository hierarchyRepository,
    ILegalDecisionContractRepository decisionContractRepository,
    IMediaEvidenceRepository mediaRepository,
    IMediaBinaryStore mediaBinaryStore,
    IMediaImageAnalyzer imageAnalyzer,
    IMediaAudioTranscriber audioTranscriber,
    IAiProviderRouter aiProviderRouter,
    ILpiPropositionIntegrationRepository integrationRepository,
    IDecisionRevisionResolver revisionResolver,
    ILogger<MediaEvidenceOrchestrationService> logger) : IMediaEvidenceOrchestrationService
{
    private const string ExtractionPromptCode = "MEDIA_EXTRACTION_V1";
    private const string FeatureCode = "DECISION_EXTRACTION";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MediaAssetRegistrationResult> RegisterAssetAsync(
        MediaAssetRegistration registration, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(content);

        // Generate the ids up front so the storage key and SQL rows agree. Buffer the bytes once so we can
        // both compute the content hash and hand an independent stream to the immutable store.
        var mediaAssetId = Guid.NewGuid();
        var mediaAssetVersionId = Guid.NewGuid();

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var contentHash = Convert.ToHexString(SHA256.HashData(bytes));

        using var storeStream = new MemoryStream(bytes, writable: false);
        var stored = await mediaBinaryStore.StoreImmutableAsync(
            registration.TenantId, mediaAssetId, mediaAssetVersionId,
            registration.OriginalFileName, storeStream, cancellationToken);

        return await mediaRepository.RegisterAssetAsync(
            registration, mediaAssetId, mediaAssetVersionId, stored.StorageKey, contentHash,
            stored.ByteLength, cancellationToken);
    }

    public async Task<MediaProcessingResult> ProcessAsync(
        MediaProcessingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var version = await mediaRepository.GetVersionAsync(request.TenantId, request.MediaAssetVersionId, cancellationToken);
        if (version is null)
            return new MediaProcessingResult(Guid.Empty, MediaProcessingStatus.Failed, 0, [], null, "Failed", "The media asset version was not found for this tenant.");

        var processorCode = request.AssetType switch
        {
            MediaAssetType.Photo => "PHOTO",
            MediaAssetType.Audio => "AUDIO",
            _ => request.AssetType.ToString().ToUpperInvariant()
        };

        // Idempotent run: the (tenant, idempotencyKey) uniqueness dedupes re-dispatch.
        var idempotencyKey = $"media-run:{request.MediaAssetVersionId:N}:{processorCode}";
        var run = await mediaRepository.CreateProcessingRunAsync(new MediaProcessingRun(
            Guid.NewGuid(), request.TenantId, request.DecisionMatterId, request.MediaAssetVersionId,
            processorCode, null, MediaProcessingStatus.Running, null, null, null, idempotencyKey,
            DateTimeOffset.UtcNow, null), request.ActorUserId, cancellationToken);

        // Retry-safe: if a prior run for this (tenant, idempotencyKey) already reached a terminal state,
        // return it instead of reprocessing the asset (prevents duplicate derivatives/proposals).
        if (run.Status is MediaProcessingStatus.Completed or MediaProcessingStatus.CapabilityUnavailable or MediaProcessingStatus.Failed)
            return new MediaProcessingResult(run.MediaProcessingRunId, run.Status, 0, [], run.CoverageJson,
                run.Status == MediaProcessingStatus.Completed ? "AlreadyProcessed"
                    : run.Status == MediaProcessingStatus.CapabilityUnavailable ? "CapabilityUnavailable" : "Failed",
                run.ErrorMessage);

        try
        {
            string derivedContentJson;
            string? coverageNote;
            string? processorVersion;

            if (request.AssetType == MediaAssetType.Photo)
            {
                var read = await mediaBinaryStore.OpenReadAsync(request.TenantId, version.StorageKey, null, null, cancellationToken);
                MediaImageAnalysisResult analysis;
                await using (read.Content)
                    analysis = await imageAnalyzer.AnalyzeAsync(request.TenantId, version, read.Content, request.CorrelationId, cancellationToken);

                if (analysis.Outcome != MediaProcessorOutcome.Produced)
                    return await FinishUnavailableOrFailedAsync(request, run, analysis.Outcome, analysis.CoverageNote, analysis.ErrorCode, analysis.ErrorMessage, cancellationToken);

                coverageNote = analysis.CoverageNote;
                processorVersion = analysis.ProcessorVersion;
                derivedContentJson = SerializeImageObservations(version, analysis);
                await mediaRepository.AddDerivativeAsync(new MediaDerivative(
                    Guid.NewGuid(), version.MediaAssetVersionId, run.MediaProcessingRunId,
                    MediaDerivativeType.Preview, null, "application/json", derivedContentJson),
                    request.TenantId, request.ActorUserId, cancellationToken);
            }
            else if (request.AssetType == MediaAssetType.Audio)
            {
                var read = await mediaBinaryStore.OpenReadAsync(request.TenantId, version.StorageKey, null, null, cancellationToken);
                MediaAudioTranscriptionResult transcription;
                await using (read.Content)
                    transcription = await audioTranscriber.TranscribeAsync(request.TenantId, version, read.Content, request.CorrelationId, cancellationToken);

                if (transcription.Outcome != MediaProcessorOutcome.Produced)
                    return await FinishUnavailableOrFailedAsync(request, run, transcription.Outcome, transcription.CoverageNote, transcription.ErrorCode, transcription.ErrorMessage, cancellationToken);

                coverageNote = transcription.CoverageNote;
                processorVersion = transcription.ProcessorVersion;
                derivedContentJson = SerializeTranscript(version, transcription);
                await mediaRepository.AddDerivativeAsync(new MediaDerivative(
                    Guid.NewGuid(), version.MediaAssetVersionId, run.MediaProcessingRunId,
                    MediaDerivativeType.Transcript, null, "application/json", derivedContentJson),
                    request.TenantId, request.ActorUserId, cancellationToken);
            }
            else
            {
                // Slice 1 automates only PHOTO and AUDIO. Other types stay available for manual annotation.
                await mediaRepository.UpdateProcessingRunAsync(request.TenantId, request.ActorUserId, run.MediaProcessingRunId,
                    MediaProcessingStatus.CapabilityUnavailable, null, "CAPABILITY_UNAVAILABLE",
                    $"Automated processing for {request.AssetType} is not available; annotate manually.",
                    null, DateTimeOffset.UtcNow, cancellationToken);
                return new MediaProcessingResult(run.MediaProcessingRunId, MediaProcessingStatus.CapabilityUnavailable, 0, [], null,
                    "CapabilityUnavailable", $"Automated processing for {request.AssetType} is not available; annotate manually.");
            }

            // Run MEDIA_EXTRACTION_V1 to turn media-derived content into anchored proposition proposals.
            var extraction = await ExtractAndParkAsync(request, version, derivedContentJson, run.MediaProcessingRunId, cancellationToken);

            await mediaRepository.UpdateProcessingRunAsync(request.TenantId, request.ActorUserId, run.MediaProcessingRunId,
                extraction.StatusCode == "Processed" ? MediaProcessingStatus.Completed : MediaProcessingStatus.Failed,
                coverageNote, extraction.StatusCode == "Processed" ? null : extraction.StatusCode, extraction.Explanation,
                null, DateTimeOffset.UtcNow, cancellationToken);

            return extraction with { CoverageNote = coverageNote };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Media processing failed for version {VersionId}.", request.MediaAssetVersionId);
            await mediaRepository.UpdateProcessingRunAsync(request.TenantId, request.ActorUserId, run.MediaProcessingRunId,
                MediaProcessingStatus.Failed, null, ex.GetType().Name, ex.Message, null, DateTimeOffset.UtcNow, cancellationToken);
            return new MediaProcessingResult(run.MediaProcessingRunId, MediaProcessingStatus.Failed, 0, [], null, "Failed", ex.Message);
        }
    }

    public async Task<MediaProposalResult> CreateManualProposalAsync(
        MediaManualProposalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var version = await mediaRepository.GetVersionAsync(request.TenantId, request.MediaAssetVersionId, cancellationToken);
        if (version is null)
            return new MediaProposalResult(Guid.Empty, Guid.Empty, Guid.Empty, "InvalidAnchor", "The media asset version was not found for this tenant.");

        if (!EvidenceAnchorValidator.TryValidate(request.AnchorType, version,
                request.NormX, request.NormY, request.NormWidth, request.NormHeight,
                request.StartMs, request.EndMs, request.FrameNumber, request.RecordReferenceJson, out var error))
            return new MediaProposalResult(Guid.Empty, Guid.Empty, Guid.Empty, "InvalidAnchor", error);

        var anchorId = await mediaRepository.AddAnchorAsync(new EvidenceAnchor(
            Guid.NewGuid(), request.TenantId, request.DecisionMatterId, request.MediaAssetVersionId, request.AnchorType,
            request.NormX, request.NormY, request.NormWidth, request.NormHeight,
            request.StartMs, request.EndMs, request.FrameNumber, request.SpeakerLabel, request.TranscriptSegmentRef,
            request.RecordReferenceJson), request.ActorUserId, cancellationToken);

        var proposalId = await ParkPropositionAsync(
            request.TenantId, request.ActorUserId, request.DecisionMatterId, request.MediaAssetVersionId,
            request.PropositionText, request.AssertionType, request.AttributedTo, request.EffectiveAt,
            [], cancellationToken);

        var linkId = await mediaRepository.AddEvidenceLinkAsync(new PropositionEvidenceLink(
            Guid.NewGuid(), request.TenantId, request.DecisionMatterId, proposalId, anchorId,
            request.EvidenceKind, null, EvidenceLinkReviewStatus.Pending), request.ActorUserId, cancellationToken);

        return new MediaProposalResult(proposalId, anchorId, linkId, "Parked",
            "Proposition parked for review with a validated manual anchor.");
    }

    // ── Extraction ────────────────────────────────────────────────────────────────────────────────
    private async Task<MediaProcessingResult> ExtractAndParkAsync(
        MediaProcessingRequest request, MediaAssetVersion version, string derivedContentJson,
        Guid processingRunId, CancellationToken cancellationToken)
    {
        var prompt = await decisionRepository.GetPromptAsync(ExtractionPromptCode, cancellationToken);
        if (prompt is null)
            return new MediaProcessingResult(processingRunId, MediaProcessingStatus.Failed, 0, [], null, "ExtractionFailed", $"Prompt {ExtractionPromptCode} is not configured.");

        var (question, contextJson) = await BuildHierarchyContextAsync(request, cancellationToken);
        var artifact = BuildArtifact(request, version, derivedContentJson);

        var userPrompt = prompt.UserPromptTemplate
            .Replace("{{QUERY}}", question, StringComparison.Ordinal)
            .Replace("{{CONTEXT}}", contextJson, StringComparison.Ordinal)
            .Replace("{{ARTIFACT}}", artifact, StringComparison.Ordinal);

        AiGenerationResult generation;
        try
        {
            generation = await aiProviderRouter.GenerateAsync(
                request.TenantId, FeatureCode, prompt.SystemPrompt, userPrompt,
                prompt.OutputSchemaJson, request.CorrelationId, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Media extraction AI call failed for matter {Matter}.", request.DecisionMatterId);
            return new MediaProcessingResult(processingRunId, MediaProcessingStatus.Failed, 0, [], null, "ExtractionFailed", $"Extraction model call failed: {ex.Message}");
        }

        List<ExtractedMediaProposition> parsed;
        try
        {
            parsed = Parse(generation.StructuredOutputJson ?? generation.Content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Media extraction JSON parse failed for matter {Matter}.", request.DecisionMatterId);
            return new MediaProcessingResult(processingRunId, MediaProcessingStatus.Failed, 0, [], null, "ExtractionFailed", $"Extraction output could not be parsed: {ex.Message}");
        }

        if (parsed.Count == 0)
            return new MediaProcessingResult(processingRunId, MediaProcessingStatus.Completed, 0, [], null, "NoProposals", null);

        var parkedIds = new List<Guid>(parsed.Count);
        foreach (var extracted in parsed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Validate the proposed anchor against the asset version; drop fabricated/out-of-bounds anchors.
            var anchorType = MapAnchorType(extracted.Anchor?.AnchorType);
            if (anchorType is null)
            {
                logger.LogWarning("Media proposition skipped: unmappable or missing anchor type for matter {Matter}.", request.DecisionMatterId);
                continue;
            }

            if (!EvidenceAnchorValidator.TryValidate(anchorType.Value, version,
                    extracted.Anchor!.NormX, extracted.Anchor.NormY, extracted.Anchor.NormWidth, extracted.Anchor.NormHeight,
                    extracted.Anchor.StartMs, extracted.Anchor.EndMs, extracted.Anchor.FrameNumber, extracted.Anchor.RecordReference,
                    out var anchorError))
            {
                logger.LogWarning("Media proposition skipped: invalid anchor ({Error}) for matter {Matter}.", anchorError, request.DecisionMatterId);
                continue;
            }

            var anchorId = await mediaRepository.AddAnchorAsync(new EvidenceAnchor(
                Guid.NewGuid(), request.TenantId, request.DecisionMatterId, version.MediaAssetVersionId, anchorType.Value,
                extracted.Anchor.NormX, extracted.Anchor.NormY, extracted.Anchor.NormWidth, extracted.Anchor.NormHeight,
                extracted.Anchor.StartMs, extracted.Anchor.EndMs, extracted.Anchor.FrameNumber,
                extracted.Anchor.SpeakerLabel, extracted.Anchor.TranscriptSegmentRef, extracted.Anchor.RecordReference),
                request.ActorUserId, cancellationToken);

            var placements = MapPlacements(extracted);
            var proposalId = await ParkPropositionAsync(
                request.TenantId, request.ActorUserId, request.DecisionMatterId, version.MediaAssetVersionId,
                extracted.PropositionText ?? string.Empty, MapAssertion(extracted.AssertionType),
                extracted.AttributedTo, ParseEffectiveAt(extracted.EffectiveAt), placements, cancellationToken);

            await mediaRepository.AddEvidenceLinkAsync(new PropositionEvidenceLink(
                Guid.NewGuid(), request.TenantId, request.DecisionMatterId, proposalId, anchorId,
                MapEvidenceKind(extracted.EvidenceKind), processingRunId, EvidenceLinkReviewStatus.Pending),
                request.ActorUserId, cancellationToken);

            parkedIds.Add(proposalId);
        }

        return new MediaProcessingResult(processingRunId, MediaProcessingStatus.Completed, parkedIds.Count, parkedIds, null, "Processed", null);
    }

    // Park a proposition through the shared integration repository (NeedsHierarchyReview when unplaceable).
    private async Task<Guid> ParkPropositionAsync(
        Guid tenantId, Guid actorUserId, Guid decisionMatterId, Guid mediaAssetVersionId,
        string propositionText, LpiAssertionType assertionType, string? attributedTo, DateTimeOffset? effectiveAt,
        IReadOnlyList<LpiPlacementProposal> placements, CancellationToken cancellationToken)
    {
        var proposalId = Guid.NewGuid();
        var snapshot = await revisionResolver.ResolveAsync(tenantId, decisionMatterId, cancellationToken);

        // Media uses the shared RetrievedProposition contract; the source version id carries the media
        // asset version so provenance resolves back to the exact bytes.
        var proposition = new RetrievedProposition(
            proposalId, decisionMatterId, mediaAssetVersionId,
            SourceLocator: $"media-version:{mediaAssetVersionId:N}",
            SourceText: propositionText,
            PropositionText: propositionText,
            AssertionType: assertionType,
            AttributedTo: attributedTo,
            EffectiveAt: effectiveAt);

        var reviewState = placements.Count == 0
            ? LpiProposalState.NeedsHierarchyReview.ToString()
            : LpiProposalState.ReviewRequired.ToString();
        var reviewReason = placements.Count == 0
            ? "Media proposal has no proposed placement; attorney must place it in the hierarchy."
            : "Media proposal parked for attorney review before integration.";

        var context = new LpiIntegrationContext(
            tenantId, actorUserId, decisionMatterId,
            snapshot.DecisionContractRevision, snapshot.CandidateSetRevision, snapshot.HierarchyRevision,
            SourceDocumentVersionId: mediaAssetVersionId, ReviewerUserId: actorUserId,
            ScoringConfigurationVersion: snapshot.ScoringConfigurationVersion,
            IdempotencyKey: $"media-park:{proposalId:N}");

        await integrationRepository.ParkForReviewAsync(new LpiReviewPark(
            tenantId, actorUserId, decisionMatterId, LpiOperationKind.Add,
            proposition, placements, context, reviewState, reviewReason,
            RetrievalModeCode: nameof(LpiRetrievalMode.MediaDirected)), cancellationToken);

        return proposalId;
    }

    private async Task<MediaProcessingResult> FinishUnavailableOrFailedAsync(
        MediaProcessingRequest request, MediaProcessingRun run, MediaProcessorOutcome outcome,
        string? coverageNote, string? errorCode, string? errorMessage, CancellationToken cancellationToken)
    {
        var status = outcome == MediaProcessorOutcome.CapabilityUnavailable
            ? MediaProcessingStatus.CapabilityUnavailable
            : MediaProcessingStatus.Failed;
        await mediaRepository.UpdateProcessingRunAsync(request.TenantId, request.ActorUserId, run.MediaProcessingRunId,
            status, coverageNote, errorCode, errorMessage, null, DateTimeOffset.UtcNow, cancellationToken);
        return new MediaProcessingResult(run.MediaProcessingRunId, status, 0, [], coverageNote,
            status == MediaProcessingStatus.CapabilityUnavailable ? "CapabilityUnavailable" : "Failed",
            errorMessage ?? coverageNote);
    }

    // ── Hierarchy context + artifact ───────────────────────────────────────────────────────────────
    private async Task<(string Question, string ContextJson)> BuildHierarchyContextAsync(
        MediaProcessingRequest request, CancellationToken cancellationToken)
    {
        // Reuse the authoritative contract → hierarchy execution path the retrieval orchestrator uses so
        // the extractor matches placements against real node ids. When no promoted hierarchy exists yet,
        // return an empty context so the extractor flags hierarchy gaps instead of force-fitting.
        var matter = await decisionRepository.GetMatterAsync(request.TenantId, request.DecisionMatterId, cancellationToken);
        var question = matter?.Title ?? string.Empty;

        var contract = await decisionContractRepository.GetCurrentContractAsync(
            request.TenantId, request.DecisionMatterId, cancellationToken);
        if (contract is null)
            return (question, EmptyContextJson(question));

        var authority = await hierarchyRepository.GetCurrentAuthorityAsync(
            request.TenantId, request.DecisionMatterId, contract.DecisionContractId, contract.VersionNumber, cancellationToken);
        if (authority is null)
            return (question, EmptyContextJson(question));

        var execution = await hierarchyRepository.GetExecutionAsync(
            request.TenantId, authority.HierarchyExecutionId, cancellationToken);
        if (execution is null || execution.Nodes.Count == 0)
            return (question, EmptyContextJson(question));

        var context = LpiHierarchyContextBuilder.Build(question, execution.Nodes);
        return (question, context.ContextJson);
    }

    private static string EmptyContextJson(string question)
        => JsonSerializer.Serialize(new { question, candidates = Array.Empty<object>(), nodes = Array.Empty<object>() }, JsonOptions);

    private static string BuildArtifact(MediaProcessingRequest request, MediaAssetVersion version, string derivedContentJson)
        => JsonSerializer.Serialize(new
        {
            mediaAssetVersionId = version.MediaAssetVersionId,
            assetType = request.AssetType.ToString().ToUpperInvariant(),
            durationMs = version.DurationMs,
            widthPx = version.WidthPx,
            heightPx = version.HeightPx,
            derived = JsonNode.Parse(derivedContentJson)
        }, JsonOptions);

    private static string SerializeImageObservations(MediaAssetVersion version, MediaImageAnalysisResult analysis)
        => JsonSerializer.Serialize(new
        {
            kind = "image_observations",
            coverageNote = analysis.CoverageNote,
            observations = analysis.Observations.Select(o => new
            {
                o.Description, o.NormX, o.NormY, o.NormWidth, o.NormHeight
            })
        }, JsonOptions);

    private static string SerializeTranscript(MediaAssetVersion version, MediaAudioTranscriptionResult transcription)
        => JsonSerializer.Serialize(new
        {
            kind = "audio_transcript",
            coverageNote = transcription.CoverageNote,
            segments = transcription.Segments.Select(s => new
            {
                s.SegmentRef, s.StartMs, s.EndMs, s.Text, s.SpeakerLabel
            })
        }, JsonOptions);

    // ── Parsing + mapping ──────────────────────────────────────────────────────────────────────────
    private static List<ExtractedMediaProposition> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        var envelope = JsonSerializer.Deserialize<ExtractionEnvelope>(json, JsonOptions);
        return envelope?.Propositions ?? [];
    }

    private IReadOnlyList<LpiPlacementProposal> MapPlacements(ExtractedMediaProposition extracted)
    {
        var placements = new List<LpiPlacementProposal>();
        foreach (var placement in extracted.Placements ?? [])
        {
            if (!TryMapRelationship(placement.Relationship, out var relationship))
                continue;
            if (!Guid.TryParse(placement.TargetNodeCode, out var targetNodeId))
                continue;
            placements.Add(new LpiPlacementProposal(
                Guid.Empty, HierarchyRevisionId: Guid.Empty, TargetNodeId: targetNodeId,
                LeftNeighborId: Guid.TryParse(placement.LeftNeighborCode, out var l) ? l : null,
                RightNeighborId: Guid.TryParse(placement.RightNeighborCode, out var r) ? r : null,
                PlacementFraction: placement.PlacementFraction,
                Relationship: relationship,
                Rationale: placement.Rationale ?? string.Empty));
        }
        return placements;
    }

    private static EvidenceAnchorType? MapAnchorType(string? code) => code?.ToUpperInvariant() switch
    {
        "IMAGE_REGION" => EvidenceAnchorType.ImageRegion,
        "VIDEO_INTERVAL" => EvidenceAnchorType.VideoInterval,
        "AUDIO_INTERVAL" => EvidenceAnchorType.AudioInterval,
        "STRUCTURED_RECORD" => EvidenceAnchorType.StructuredRecord,
        _ => null
    };

    private static MediaEvidenceKind MapEvidenceKind(string? code) => code?.ToUpperInvariant() switch
    {
        "ATTRIBUTED_ASSERTION" => MediaEvidenceKind.AttributedAssertion,
        "PROPOSED_INTERPRETATION" => MediaEvidenceKind.ProposedInterpretation,
        _ => MediaEvidenceKind.Observation
    };

    private static LpiAssertionType MapAssertion(string? code) => code switch
    {
        "Reports" => LpiAssertionType.Reports,
        "Documents" => LpiAssertionType.Documents,
        "StatesLaw" => LpiAssertionType.StatesLaw,
        "Infers" => LpiAssertionType.Infers,
        _ => LpiAssertionType.Asserts
    };

    private static bool TryMapRelationship(string? code, out LpiRelationship relationship)
    {
        switch (code?.ToUpperInvariant())
        {
            case "SUPPORTS": relationship = LpiRelationship.Supports; return true;
            case "CONTRADICTS": relationship = LpiRelationship.Contradicts; return true;
            case "QUALIFIES": relationship = LpiRelationship.Qualifies; return true;
            case "CONTEXT_ONLY": relationship = LpiRelationship.ContextOnly; return true;
            default: relationship = LpiRelationship.ContextOnly; return false;
        }
    }

    private static DateTimeOffset? ParseEffectiveAt(string? value)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    // ── Extraction DTOs ────────────────────────────────────────────────────────────────────────────
    private sealed record ExtractionEnvelope(List<ExtractedMediaProposition>? Propositions);

    private sealed record ExtractedMediaProposition(
        string? PropositionText,
        string? EvidenceKind,
        string? AssertionType,
        string? AttributedTo,
        string? EffectiveAt,
        string? UncertaintyNote,
        bool? NeedsHierarchyReview,
        string? ReviewReason,
        ExtractedAnchor? Anchor,
        List<ExtractedPlacement>? Placements);

    private sealed record ExtractedAnchor(
        string? AnchorType,
        decimal? NormX, decimal? NormY, decimal? NormWidth, decimal? NormHeight,
        long? StartMs, long? EndMs, long? FrameNumber,
        string? SpeakerLabel, string? TranscriptSegmentRef, string? RecordReference);

    private sealed record ExtractedPlacement(
        string? TargetNodeCode,
        string? Relationship,
        string? LeftNeighborCode,
        string? RightNeighborCode,
        decimal? PlacementFraction,
        string? Rationale);
}
