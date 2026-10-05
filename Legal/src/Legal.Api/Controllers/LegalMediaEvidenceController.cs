using Legal.Api.Security;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Decision.Lpi;
using Legal.Application.Features.Intelligence.Decision.Media;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// LegalMediaEvidenceController — the Media & Machine Evidence channel API (Phase 3, Slice 1).
//
// Covers the SENDING half end-to-end: register/upload an immutable asset, authorized ranged preview,
// run capability-aware processing (PHOTO/AUDIO) that PARKS anchored proposition proposals, manual
// annotation fallback, and reviewer confirmation of a proposition↔anchor evidence link. Acceptance of a
// parked proposition itself still flows through the SHARED retrieval review funnel on
// /api/legal_decision (IRetrievalPropositionReviewService → IPropositionIntegrationService). POLOXI Core
// alone scores — nothing here ranks, scores, or picks a winner.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/legal_media")]
public sealed class LegalMediaEvidenceController(
    IMediaEvidenceOrchestrationService orchestrationService,
    IMediaEvidenceRepository mediaRepository,
    IMediaBinaryStore mediaBinaryStore,
    IIntelligenceExecutionService executionService) : ControllerBase
{
    private const string CapabilityCode = JudzCapabilities.LegalDecision;
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    // Register + store a new immutable media asset version for a matter. Bytes are streamed to the private
    // media store; SQL keeps only the opaque storage key + content hash.
    [HttpPost("matters/{matterId:guid}/assets")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> RegisterAsset(Guid matterId, [FromForm] RegisterMediaAssetForm form, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        if (form.File is null || form.File.Length == 0)
            return BadRequest("A non-empty media file is required.");
        if (!Enum.TryParse<MediaAssetType>(form.AssetType, ignoreCase: true, out var assetType))
            return BadRequest($"Unknown asset type '{form.AssetType}'.");

        var registration = new MediaAssetRegistration(
            TenantId,
            ActorUserId,
            matterId,
            form.MatterResourceId,
            assetType,
            form.File.FileName,
            string.IsNullOrWhiteSpace(form.MimeType) ? form.File.ContentType : form.MimeType,
            form.SourceDescription,
            form.MetadataJson,
            form.CaptureAssertedAtUtc,
            form.DurationMs,
            form.WidthPx,
            form.HeightPx);

        await using var stream = form.File.OpenReadStream();
        var result = await orchestrationService.RegisterAssetAsync(registration, stream, cancellationToken);
        return Ok(result);
    }

    // List the media assets registered for a matter (tenant scoped).
    [HttpGet("matters/{matterId:guid}/assets")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetAssets(Guid matterId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await mediaRepository.GetAssetsAsync(TenantId, matterId, cancellationToken));
    }

    // List the immutable versions of an asset (newest first). The UI resolves the version id for preview,
    // range playback, and processing from this list — the asset list itself carries no version ids.
    [HttpGet("matters/{matterId:guid}/assets/{assetId:guid}/versions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetVersions(Guid matterId, Guid assetId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await mediaRepository.GetVersionsAsync(TenantId, assetId, cancellationToken));
    }

    // Authorized, range-capable preview/playback of a stored asset version. The tenant is verified against
    // the owning version before any byte is read; audio/video honor HTTP Range for streaming.
    [HttpGet("versions/{versionId:guid}/content")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetVersionContent(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await mediaRepository.GetVersionAsync(TenantId, versionId, cancellationToken);
        if (version is null)
            return NotFound();

        var asset = await mediaRepository.GetAssetAsync(TenantId, version.MediaAssetId, cancellationToken);
        if (asset is null)
            return NotFound();

        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, asset.DecisionMatterId, null, cancellationToken);
        if (denied is not null) return denied;

        var read = await mediaBinaryStore.OpenReadAsync(TenantId, version.StorageKey, null, null, cancellationToken);
        return File(read.Content, asset.MimeType, asset.OriginalFileName, enableRangeProcessing: true);
    }

    // Run ONE capability-aware processing pass over a stored asset version: PHOTO → image analysis,
    // AUDIO → timestamped transcription, then MEDIA_EXTRACTION_V1 → anchored proposition proposals PARKED
    // for review. CapabilityUnavailable leaves the asset for manual annotation; nothing is fabricated.
    [HttpPost("matters/{matterId:guid}/assets/{assetId:guid}/versions/{versionId:guid}/process")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Process(Guid matterId, Guid assetId, Guid versionId, [FromBody] ProcessMediaAssetRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        if (!Enum.TryParse<MediaAssetType>(request.AssetType, ignoreCase: true, out var assetType))
            return BadRequest($"Unknown asset type '{request.AssetType}'.");

        var result = await orchestrationService.ProcessAsync(new MediaProcessingRequest(
            TenantId,
            ActorUserId,
            matterId,
            assetId,
            versionId,
            assetType,
            request.CorrelationId ?? Guid.NewGuid().ToString("N")), cancellationToken);
        return Ok(result);
    }

    // Manually author ONE anchored proposition proposal (manual-annotation fallback / unsupported formats).
    // The anchor is validated against the asset version, the proposition is parked, and a link is created.
    [HttpPost("matters/{matterId:guid}/versions/{versionId:guid}/manual-proposal")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> CreateManualProposal(Guid matterId, Guid versionId, [FromBody] CreateMediaManualProposalRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        if (!Enum.TryParse<EvidenceAnchorType>(request.AnchorType, ignoreCase: true, out var anchorType))
            return BadRequest($"Unknown anchor type '{request.AnchorType}'.");
        if (!Enum.TryParse<MediaEvidenceKind>(request.EvidenceKind, ignoreCase: true, out var evidenceKind))
            return BadRequest($"Unknown evidence kind '{request.EvidenceKind}'.");

        var result = await orchestrationService.CreateManualProposalAsync(new MediaManualProposalRequest(
            TenantId,
            ActorUserId,
            matterId,
            versionId,
            anchorType,
            evidenceKind,
            request.PropositionText,
            request.AssertionType,
            request.AttributedTo,
            request.EffectiveAt,
            request.NormX, request.NormY, request.NormWidth, request.NormHeight,
            request.StartMs, request.EndMs, request.FrameNumber, request.SpeakerLabel, request.TranscriptSegmentRef,
            request.RecordReferenceJson), cancellationToken);
        return Ok(result);
    }

    // The proposition↔anchor evidence links for a parked/accepted proposition (tenant scoped). Feeds the
    // reviewer panel so the attorney can reopen the exact source region/interval/records.
    [HttpGet("matters/{matterId:guid}/propositions/{propositionId:guid}/evidence-links")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetEvidenceLinks(Guid matterId, Guid propositionId, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;
        return Ok(await mediaRepository.GetEvidenceLinksAsync(TenantId, propositionId, cancellationToken));
    }

    // Reviewer confirms or rejects ONE proposition↔anchor evidence link. This records that the attorney
    // verified the anchor matches the proposition; it does NOT score. Accept/withdraw of the proposition
    // itself still flows through the shared retrieval review funnel on /api/legal_decision.
    [HttpPost("matters/{matterId:guid}/evidence-links/{linkId:guid}/review")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ReviewEvidenceLink(Guid matterId, Guid linkId, [FromBody] ReviewMediaEvidenceLinkRequest request, CancellationToken cancellationToken)
    {
        var (denied, _) = await CapabilityGate.EnforceAsync(executionService, User, CapabilityCode, matterId, null, cancellationToken);
        if (denied is not null) return denied;

        if (!Enum.TryParse<EvidenceLinkReviewStatus>(request.Status, ignoreCase: true, out var status))
            return BadRequest($"Unknown review status '{request.Status}'.");

        await mediaRepository.SetEvidenceLinkReviewAsync(TenantId, ActorUserId, linkId, status, cancellationToken);
        return NoContent();
    }
}

// Multipart form for registering + uploading a new immutable media asset version.
public sealed class RegisterMediaAssetForm
{
    public IFormFile? File { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public Guid? MatterResourceId { get; set; }
    public string? MimeType { get; set; }
    public string? SourceDescription { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset? CaptureAssertedAtUtc { get; set; }
    public long? DurationMs { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }
}

// Request body for running a processing pass over a stored asset version.
public sealed record ProcessMediaAssetRequest(
    string AssetType,
    string? CorrelationId = null);

// Request body for manually authoring an anchored proposition proposal.
public sealed record CreateMediaManualProposalRequest(
    string AnchorType,
    string EvidenceKind,
    string PropositionText,
    LpiAssertionType AssertionType,
    string? AttributedTo = null,
    DateTimeOffset? EffectiveAt = null,
    decimal? NormX = null, decimal? NormY = null, decimal? NormWidth = null, decimal? NormHeight = null,
    long? StartMs = null, long? EndMs = null, long? FrameNumber = null,
    string? SpeakerLabel = null, string? TranscriptSegmentRef = null,
    string? RecordReferenceJson = null);

// Request body for reviewer confirmation of a proposition↔anchor evidence link.
public sealed record ReviewMediaEvidenceLinkRequest(string Status);
