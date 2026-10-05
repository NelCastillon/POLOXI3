using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Media;

namespace Legal.Infrastructure.Persistence.Repositories;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Dapper repository for the Media & Machine Evidence channel (POLOXI.Legal_Media* / Legal_EvidenceAnchor
// / Legal_PropositionEvidenceLink, migration 0401). Tenant + matter scoped throughout. Owns only the
// media asset/anchor/run graph; accepted propositions flow through the shared LPI integration path.
// Enum codes are persisted as stable UPPER_SNAKE / PascalCase strings matching the migration comments.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class MediaEvidenceRepository(ISqlConnectionFactory connectionFactory) : IMediaEvidenceRepository
{
    public async Task<MediaAssetRegistrationResult> RegisterAssetAsync(
        MediaAssetRegistration registration, Guid mediaAssetId, Guid mediaAssetVersionId,
        string storageKey, string contentHash, long byteLength,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var assetId = mediaAssetId;
        var versionId = mediaAssetVersionId;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_MediaAsset
                (MediaAssetId, DecisionMatterId, MatterResourceId, AssetTypeCode, OriginalFileName,
                 MimeType, SourceDescription, UploadedByUserId, TenantId, CreatedByUserId)
            VALUES
                (@MediaAssetId, @DecisionMatterId, @MatterResourceId, @AssetTypeCode, @OriginalFileName,
                 @MimeType, @SourceDescription, @ActorUserId, @TenantId, @ActorUserId);

            INSERT INTO POLOXI.Legal_MediaAssetVersion
                (MediaAssetVersionId, MediaAssetId, VersionNumber, StorageKey, ContentHash, ByteLength,
                 MetadataJson, CaptureAssertedAtUtc, DurationMs, WidthPx, HeightPx, TenantId, CreatedByUserId)
            VALUES
                (@MediaAssetVersionId, @MediaAssetId, 1, @StorageKey, @ContentHash, @ByteLength,
                 @MetadataJson, @CaptureAssertedAtUtc, @DurationMs, @WidthPx, @HeightPx, @TenantId, @ActorUserId);
            """,
            new
            {
                MediaAssetId = assetId,
                registration.DecisionMatterId,
                registration.MatterResourceId,
                AssetTypeCode = ToAssetTypeCode(registration.AssetType),
                registration.OriginalFileName,
                registration.MimeType,
                registration.SourceDescription,
                registration.ActorUserId,
                registration.TenantId,
                MediaAssetVersionId = versionId,
                StorageKey = storageKey,
                ContentHash = contentHash,
                ByteLength = byteLength,
                registration.MetadataJson,
                CaptureAssertedAtUtc = registration.CaptureAssertedAtUtc?.UtcDateTime,
                registration.DurationMs,
                registration.WidthPx,
                registration.HeightPx
            },
            cancellationToken: cancellationToken));

        return new MediaAssetRegistrationResult(assetId, versionId, 1, contentHash, byteLength);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetAssetsAsync(
        Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AssetRow>(new CommandDefinition(
            """
            SELECT MediaAssetId, DecisionMatterId, MatterResourceId, AssetTypeCode, OriginalFileName,
                   MimeType, SourceDescription, UploadedByUserId, CreatedDateUtc
            FROM POLOXI.Legal_MediaAsset
            WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND IsDeleted = 0
            ORDER BY CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, DecisionMatterId = decisionMatterId }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToModel(tenantId)).ToList();
    }

    public async Task<MediaAsset?> GetAssetAsync(
        Guid tenantId, Guid mediaAssetId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AssetRow>(new CommandDefinition(
            """
            SELECT MediaAssetId, DecisionMatterId, MatterResourceId, AssetTypeCode, OriginalFileName,
                   MimeType, SourceDescription, UploadedByUserId, CreatedDateUtc
            FROM POLOXI.Legal_MediaAsset
            WHERE TenantId = @TenantId AND MediaAssetId = @MediaAssetId AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, MediaAssetId = mediaAssetId }, cancellationToken: cancellationToken));
        return row?.ToModel(tenantId);
    }

    public async Task<IReadOnlyList<MediaAssetVersion>> GetVersionsAsync(
        Guid tenantId, Guid mediaAssetId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<VersionRow>(new CommandDefinition(
            """
            SELECT MediaAssetVersionId, MediaAssetId, VersionNumber, StorageKey, ContentHash, ByteLength,
                   MetadataJson, CaptureAssertedAtUtc, IngestedAtUtc, DurationMs, WidthPx, HeightPx
            FROM POLOXI.Legal_MediaAssetVersion
            WHERE TenantId = @TenantId AND MediaAssetId = @MediaAssetId AND IsDeleted = 0
            ORDER BY VersionNumber DESC;
            """,
            new { TenantId = tenantId, MediaAssetId = mediaAssetId }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<MediaAssetVersion?> GetVersionAsync(
        Guid tenantId, Guid mediaAssetVersionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<VersionRow>(new CommandDefinition(
            """
            SELECT MediaAssetVersionId, MediaAssetId, VersionNumber, StorageKey, ContentHash, ByteLength,
                   MetadataJson, CaptureAssertedAtUtc, IngestedAtUtc, DurationMs, WidthPx, HeightPx
            FROM POLOXI.Legal_MediaAssetVersion
            WHERE TenantId = @TenantId AND MediaAssetVersionId = @MediaAssetVersionId AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, MediaAssetVersionId = mediaAssetVersionId }, cancellationToken: cancellationToken));
        return row?.ToModel();
    }

    public async Task<IReadOnlyList<MediaDerivative>> GetDerivativesAsync(
        Guid tenantId, Guid mediaAssetVersionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DerivativeRow>(new CommandDefinition(
            """
            SELECT MediaDerivativeId, MediaAssetVersionId, MediaProcessingRunId, DerivativeTypeCode,
                   StorageKey, MimeType, PayloadJson
            FROM POLOXI.Legal_MediaDerivative
            WHERE TenantId = @TenantId AND MediaAssetVersionId = @MediaAssetVersionId AND IsDeleted = 0
            ORDER BY CreatedDateUtc ASC;
            """,
            new { TenantId = tenantId, MediaAssetVersionId = mediaAssetVersionId }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<Guid> AddDerivativeAsync(
        MediaDerivative derivative, Guid tenantId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var id = derivative.MediaDerivativeId == Guid.Empty ? Guid.NewGuid() : derivative.MediaDerivativeId;
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_MediaDerivative
                (MediaDerivativeId, MediaAssetVersionId, MediaProcessingRunId, DerivativeTypeCode,
                 StorageKey, MimeType, PayloadJson, TenantId, CreatedByUserId)
            VALUES
                (@MediaDerivativeId, @MediaAssetVersionId, @MediaProcessingRunId, @DerivativeTypeCode,
                 @StorageKey, @MimeType, @PayloadJson, @TenantId, @ActorUserId);
            """,
            new
            {
                MediaDerivativeId = id,
                derivative.MediaAssetVersionId,
                derivative.MediaProcessingRunId,
                DerivativeTypeCode = ToDerivativeTypeCode(derivative.DerivativeType),
                derivative.StorageKey,
                derivative.MimeType,
                derivative.PayloadJson,
                TenantId = tenantId,
                ActorUserId = actorUserId
            },
            cancellationToken: cancellationToken));
        return id;
    }

    public async Task<MediaProcessingRun> CreateProcessingRunAsync(
        MediaProcessingRun run, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var existing = await connection.QuerySingleOrDefaultAsync<RunRow>(new CommandDefinition(
            """
            SELECT MediaProcessingRunId, DecisionMatterId, MediaAssetVersionId, ProcessorCode, ProcessorVersion,
                   StatusCode, CoverageJson, ErrorCode, ErrorMessage, IdempotencyKey, StartedAtUtc, CompletedAtUtc
            FROM POLOXI.Legal_MediaProcessingRun
            WHERE TenantId = @TenantId AND IdempotencyKey = @IdempotencyKey AND IsDeleted = 0;
            """,
            new { run.TenantId, run.IdempotencyKey }, cancellationToken: cancellationToken));
        if (existing is not null)
            return existing.ToModel(run.TenantId);

        var id = run.MediaProcessingRunId == Guid.Empty ? Guid.NewGuid() : run.MediaProcessingRunId;
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_MediaProcessingRun
                (MediaProcessingRunId, DecisionMatterId, MediaAssetVersionId, ProcessorCode, ProcessorVersion,
                 StatusCode, CoverageJson, ErrorCode, ErrorMessage, IdempotencyKey, StartedAtUtc, CompletedAtUtc,
                 TenantId, CreatedByUserId)
            VALUES
                (@MediaProcessingRunId, @DecisionMatterId, @MediaAssetVersionId, @ProcessorCode, @ProcessorVersion,
                 @StatusCode, @CoverageJson, @ErrorCode, @ErrorMessage, @IdempotencyKey, @StartedAtUtc, @CompletedAtUtc,
                 @TenantId, @ActorUserId);
            """,
            new
            {
                MediaProcessingRunId = id,
                run.DecisionMatterId,
                run.MediaAssetVersionId,
                run.ProcessorCode,
                run.ProcessorVersion,
                StatusCode = run.Status.ToString(),
                run.CoverageJson,
                run.ErrorCode,
                run.ErrorMessage,
                run.IdempotencyKey,
                StartedAtUtc = run.StartedAtUtc?.UtcDateTime,
                CompletedAtUtc = run.CompletedAtUtc?.UtcDateTime,
                run.TenantId,
                ActorUserId = actorUserId
            },
            cancellationToken: cancellationToken));

        return run with { MediaProcessingRunId = id };
    }

    public async Task<MediaProcessingRun?> GetProcessingRunAsync(
        Guid tenantId, Guid mediaProcessingRunId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<RunRow>(new CommandDefinition(
            """
            SELECT MediaProcessingRunId, DecisionMatterId, MediaAssetVersionId, ProcessorCode, ProcessorVersion,
                   StatusCode, CoverageJson, ErrorCode, ErrorMessage, IdempotencyKey, StartedAtUtc, CompletedAtUtc
            FROM POLOXI.Legal_MediaProcessingRun
            WHERE TenantId = @TenantId AND MediaProcessingRunId = @MediaProcessingRunId AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, MediaProcessingRunId = mediaProcessingRunId }, cancellationToken: cancellationToken));
        return row?.ToModel(tenantId);
    }

    public async Task UpdateProcessingRunAsync(
        Guid tenantId, Guid actorUserId, Guid mediaProcessingRunId, MediaProcessingStatus status,
        string? coverageJson, string? errorCode, string? errorMessage,
        DateTimeOffset? startedAtUtc, DateTimeOffset? completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_MediaProcessingRun
            SET StatusCode = @StatusCode,
                CoverageJson = @CoverageJson,
                ErrorCode = @ErrorCode,
                ErrorMessage = @ErrorMessage,
                StartedAtUtc = COALESCE(@StartedAtUtc, StartedAtUtc),
                CompletedAtUtc = COALESCE(@CompletedAtUtc, CompletedAtUtc),
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @ActorUserId
            WHERE TenantId = @TenantId AND MediaProcessingRunId = @MediaProcessingRunId AND IsDeleted = 0;
            """,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                MediaProcessingRunId = mediaProcessingRunId,
                StatusCode = status.ToString(),
                CoverageJson = coverageJson,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                StartedAtUtc = startedAtUtc?.UtcDateTime,
                CompletedAtUtc = completedAtUtc?.UtcDateTime
            },
            cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddAnchorAsync(
        EvidenceAnchor anchor, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var id = anchor.EvidenceAnchorId == Guid.Empty ? Guid.NewGuid() : anchor.EvidenceAnchorId;
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_EvidenceAnchor
                (EvidenceAnchorId, DecisionMatterId, MediaAssetVersionId, AnchorTypeCode,
                 NormX, NormY, NormWidth, NormHeight, StartMs, EndMs, FrameNumber, SpeakerLabel,
                 TranscriptSegmentRef, RecordReferenceJson, TenantId, CreatedByUserId)
            VALUES
                (@EvidenceAnchorId, @DecisionMatterId, @MediaAssetVersionId, @AnchorTypeCode,
                 @NormX, @NormY, @NormWidth, @NormHeight, @StartMs, @EndMs, @FrameNumber, @SpeakerLabel,
                 @TranscriptSegmentRef, @RecordReferenceJson, @TenantId, @ActorUserId);
            """,
            new
            {
                EvidenceAnchorId = id,
                anchor.DecisionMatterId,
                anchor.MediaAssetVersionId,
                AnchorTypeCode = ToAnchorTypeCode(anchor.AnchorType),
                anchor.NormX,
                anchor.NormY,
                anchor.NormWidth,
                anchor.NormHeight,
                anchor.StartMs,
                anchor.EndMs,
                anchor.FrameNumber,
                anchor.SpeakerLabel,
                anchor.TranscriptSegmentRef,
                anchor.RecordReferenceJson,
                anchor.TenantId,
                ActorUserId = actorUserId
            },
            cancellationToken: cancellationToken));
        return id;
    }

    public async Task<EvidenceAnchor?> GetAnchorAsync(
        Guid tenantId, Guid evidenceAnchorId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AnchorRow>(new CommandDefinition(
            """
            SELECT EvidenceAnchorId, DecisionMatterId, MediaAssetVersionId, AnchorTypeCode,
                   NormX, NormY, NormWidth, NormHeight, StartMs, EndMs, FrameNumber, SpeakerLabel,
                   TranscriptSegmentRef, RecordReferenceJson
            FROM POLOXI.Legal_EvidenceAnchor
            WHERE TenantId = @TenantId AND EvidenceAnchorId = @EvidenceAnchorId AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, EvidenceAnchorId = evidenceAnchorId }, cancellationToken: cancellationToken));
        return row?.ToModel(tenantId);
    }

    public async Task<Guid> AddEvidenceLinkAsync(
        PropositionEvidenceLink link, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var id = link.PropositionEvidenceLinkId == Guid.Empty ? Guid.NewGuid() : link.PropositionEvidenceLinkId;
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_PropositionEvidenceLink
                (PropositionEvidenceLinkId, DecisionMatterId, RetrievedPropositionId, EvidenceAnchorId,
                 EvidenceRelationshipCode, MediaProcessingRunId, ReviewStatusCode, TenantId, CreatedByUserId)
            VALUES
                (@PropositionEvidenceLinkId, @DecisionMatterId, @RetrievedPropositionId, @EvidenceAnchorId,
                 @EvidenceRelationshipCode, @MediaProcessingRunId, @ReviewStatusCode, @TenantId, @ActorUserId);
            """,
            new
            {
                PropositionEvidenceLinkId = id,
                link.DecisionMatterId,
                link.RetrievedPropositionId,
                link.EvidenceAnchorId,
                EvidenceRelationshipCode = ToEvidenceKindCode(link.EvidenceKind),
                link.MediaProcessingRunId,
                ReviewStatusCode = link.ReviewStatus.ToString(),
                link.TenantId,
                ActorUserId = actorUserId
            },
            cancellationToken: cancellationToken));
        return id;
    }

    public async Task<IReadOnlyList<PropositionEvidenceLink>> GetEvidenceLinksAsync(
        Guid tenantId, Guid retrievedPropositionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LinkRow>(new CommandDefinition(
            """
            SELECT PropositionEvidenceLinkId, DecisionMatterId, RetrievedPropositionId, EvidenceAnchorId,
                   EvidenceRelationshipCode, MediaProcessingRunId, ReviewStatusCode
            FROM POLOXI.Legal_PropositionEvidenceLink
            WHERE TenantId = @TenantId AND RetrievedPropositionId = @RetrievedPropositionId AND IsDeleted = 0
            ORDER BY CreatedDateUtc ASC;
            """,
            new { TenantId = tenantId, RetrievedPropositionId = retrievedPropositionId }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToModel(tenantId)).ToList();
    }

    public async Task SetEvidenceLinkReviewAsync(
        Guid tenantId, Guid actorUserId, Guid propositionEvidenceLinkId, EvidenceLinkReviewStatus status,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_PropositionEvidenceLink
            SET ReviewStatusCode = @ReviewStatusCode,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @ActorUserId
            WHERE TenantId = @TenantId AND PropositionEvidenceLinkId = @PropositionEvidenceLinkId AND IsDeleted = 0;
            """,
            new
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                PropositionEvidenceLinkId = propositionEvidenceLinkId,
                ReviewStatusCode = status.ToString()
            },
            cancellationToken: cancellationToken));
    }

    // ── Enum ⇄ code mapping (stable persisted codes) ──────────────────────────────────────────────
    private static string ToAssetTypeCode(MediaAssetType type) => type switch
    {
        MediaAssetType.Photo => "PHOTO",
        MediaAssetType.Video => "VIDEO",
        MediaAssetType.Audio => "AUDIO",
        MediaAssetType.Gps => "GPS",
        MediaAssetType.Telematics => "TELEMATICS",
        MediaAssetType.DeviceLog => "DEVICE_LOG",
        MediaAssetType.SystemLog => "SYSTEM_LOG",
        _ => "PHOTO"
    };

    private static MediaAssetType FromAssetTypeCode(string code) => code switch
    {
        "PHOTO" => MediaAssetType.Photo,
        "VIDEO" => MediaAssetType.Video,
        "AUDIO" => MediaAssetType.Audio,
        "GPS" => MediaAssetType.Gps,
        "TELEMATICS" => MediaAssetType.Telematics,
        "DEVICE_LOG" => MediaAssetType.DeviceLog,
        "SYSTEM_LOG" => MediaAssetType.SystemLog,
        _ => MediaAssetType.Photo
    };

    private static string ToAnchorTypeCode(EvidenceAnchorType type) => type switch
    {
        EvidenceAnchorType.ImageRegion => "IMAGE_REGION",
        EvidenceAnchorType.VideoInterval => "VIDEO_INTERVAL",
        EvidenceAnchorType.AudioInterval => "AUDIO_INTERVAL",
        EvidenceAnchorType.StructuredRecord => "STRUCTURED_RECORD",
        _ => "IMAGE_REGION"
    };

    private static EvidenceAnchorType FromAnchorTypeCode(string code) => code switch
    {
        "IMAGE_REGION" => EvidenceAnchorType.ImageRegion,
        "VIDEO_INTERVAL" => EvidenceAnchorType.VideoInterval,
        "AUDIO_INTERVAL" => EvidenceAnchorType.AudioInterval,
        "STRUCTURED_RECORD" => EvidenceAnchorType.StructuredRecord,
        _ => EvidenceAnchorType.ImageRegion
    };

    private static string ToDerivativeTypeCode(MediaDerivativeType type) => type switch
    {
        MediaDerivativeType.Thumbnail => "THUMBNAIL",
        MediaDerivativeType.Preview => "PREVIEW",
        MediaDerivativeType.Transcript => "TRANSCRIPT",
        MediaDerivativeType.Frame => "FRAME",
        MediaDerivativeType.ParsedLog => "PARSED_LOG",
        _ => "PREVIEW"
    };

    private static MediaDerivativeType FromDerivativeTypeCode(string code) => code switch
    {
        "THUMBNAIL" => MediaDerivativeType.Thumbnail,
        "PREVIEW" => MediaDerivativeType.Preview,
        "TRANSCRIPT" => MediaDerivativeType.Transcript,
        "FRAME" => MediaDerivativeType.Frame,
        "PARSED_LOG" => MediaDerivativeType.ParsedLog,
        _ => MediaDerivativeType.Preview
    };

    private static string ToEvidenceKindCode(MediaEvidenceKind kind) => kind switch
    {
        MediaEvidenceKind.Observation => "OBSERVATION",
        MediaEvidenceKind.AttributedAssertion => "ATTRIBUTED_ASSERTION",
        MediaEvidenceKind.ProposedInterpretation => "PROPOSED_INTERPRETATION",
        _ => "OBSERVATION"
    };

    private static MediaEvidenceKind FromEvidenceKindCode(string code) => code switch
    {
        "OBSERVATION" => MediaEvidenceKind.Observation,
        "ATTRIBUTED_ASSERTION" => MediaEvidenceKind.AttributedAssertion,
        "PROPOSED_INTERPRETATION" => MediaEvidenceKind.ProposedInterpretation,
        _ => MediaEvidenceKind.Observation
    };

    private static MediaProcessingStatus FromStatusCode(string code) =>
        Enum.TryParse<MediaProcessingStatus>(code, ignoreCase: true, out var s) ? s : MediaProcessingStatus.Pending;

    private static EvidenceLinkReviewStatus FromReviewCode(string code) =>
        Enum.TryParse<EvidenceLinkReviewStatus>(code, ignoreCase: true, out var s) ? s : EvidenceLinkReviewStatus.Pending;

    // ── Row DTOs (Dapper maps column names 1:1) ───────────────────────────────────────────────────
    private sealed record AssetRow(
        Guid MediaAssetId, Guid DecisionMatterId, Guid? MatterResourceId, string AssetTypeCode,
        string OriginalFileName, string MimeType, string? SourceDescription, Guid? UploadedByUserId,
        DateTime CreatedDateUtc)
    {
        public MediaAsset ToModel(Guid tenantId) => new(
            MediaAssetId, tenantId, DecisionMatterId, MatterResourceId, FromAssetTypeCode(AssetTypeCode),
            OriginalFileName, MimeType, SourceDescription, UploadedByUserId,
            new DateTimeOffset(DateTime.SpecifyKind(CreatedDateUtc, DateTimeKind.Utc)));
    }

    private sealed record VersionRow(
        Guid MediaAssetVersionId, Guid MediaAssetId, int VersionNumber, string StorageKey, string ContentHash,
        long ByteLength, string? MetadataJson, DateTime? CaptureAssertedAtUtc, DateTime IngestedAtUtc,
        long? DurationMs, int? WidthPx, int? HeightPx)
    {
        public MediaAssetVersion ToModel() => new(
            MediaAssetVersionId, MediaAssetId, VersionNumber, StorageKey, ContentHash, ByteLength,
            MetadataJson,
            CaptureAssertedAtUtc is { } c ? new DateTimeOffset(DateTime.SpecifyKind(c, DateTimeKind.Utc)) : null,
            new DateTimeOffset(DateTime.SpecifyKind(IngestedAtUtc, DateTimeKind.Utc)),
            DurationMs, WidthPx, HeightPx);
    }

    private sealed record DerivativeRow(
        Guid MediaDerivativeId, Guid MediaAssetVersionId, Guid? MediaProcessingRunId, string DerivativeTypeCode,
        string? StorageKey, string? MimeType, string? PayloadJson)
    {
        public MediaDerivative ToModel() => new(
            MediaDerivativeId, MediaAssetVersionId, MediaProcessingRunId, FromDerivativeTypeCode(DerivativeTypeCode),
            StorageKey, MimeType, PayloadJson);
    }

    private sealed record RunRow(
        Guid MediaProcessingRunId, Guid DecisionMatterId, Guid MediaAssetVersionId, string ProcessorCode,
        string? ProcessorVersion, string StatusCode, string? CoverageJson, string? ErrorCode, string? ErrorMessage,
        string IdempotencyKey, DateTime? StartedAtUtc, DateTime? CompletedAtUtc)
    {
        public MediaProcessingRun ToModel(Guid tenantId) => new(
            MediaProcessingRunId, tenantId, DecisionMatterId, MediaAssetVersionId, ProcessorCode, ProcessorVersion,
            FromStatusCode(StatusCode), CoverageJson, ErrorCode, ErrorMessage, IdempotencyKey,
            StartedAtUtc is { } s ? new DateTimeOffset(DateTime.SpecifyKind(s, DateTimeKind.Utc)) : null,
            CompletedAtUtc is { } c ? new DateTimeOffset(DateTime.SpecifyKind(c, DateTimeKind.Utc)) : null);
    }

    private sealed record AnchorRow(
        Guid EvidenceAnchorId, Guid DecisionMatterId, Guid MediaAssetVersionId, string AnchorTypeCode,
        decimal? NormX, decimal? NormY, decimal? NormWidth, decimal? NormHeight,
        long? StartMs, long? EndMs, long? FrameNumber, string? SpeakerLabel,
        string? TranscriptSegmentRef, string? RecordReferenceJson)
    {
        public EvidenceAnchor ToModel(Guid tenantId) => new(
            EvidenceAnchorId, tenantId, DecisionMatterId, MediaAssetVersionId, FromAnchorTypeCode(AnchorTypeCode),
            NormX, NormY, NormWidth, NormHeight, StartMs, EndMs, FrameNumber, SpeakerLabel,
            TranscriptSegmentRef, RecordReferenceJson);
    }

    private sealed record LinkRow(
        Guid PropositionEvidenceLinkId, Guid DecisionMatterId, Guid RetrievedPropositionId, Guid EvidenceAnchorId,
        string EvidenceRelationshipCode, Guid? MediaProcessingRunId, string ReviewStatusCode)
    {
        public PropositionEvidenceLink ToModel(Guid tenantId) => new(
            PropositionEvidenceLinkId, tenantId, DecisionMatterId, RetrievedPropositionId, EvidenceAnchorId,
            FromEvidenceKindCode(EvidenceRelationshipCode), MediaProcessingRunId, FromReviewCode(ReviewStatusCode));
    }
}
