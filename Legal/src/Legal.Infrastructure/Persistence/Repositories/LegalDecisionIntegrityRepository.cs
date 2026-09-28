using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper repository for Continuous Decision Integrity (Phase 1). All state lives in the POLOXI.Legal_*
// integrity tables (migration 0340). Snapshots are append-only; only reliance status is mutated.
public sealed class LegalDecisionIntegrityRepository(ISqlConnectionFactory connectionFactory) : IDecisionIntegrityRepository
{
    // ── Snapshots ──────────────────────────────────────────────────────────────────────────────

    public async Task<Guid> CreateSnapshotAsync(DecisionSnapshotPersistence snapshot, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionSnapshot
                (DecisionSnapshotId, DecisionMatterId, DecisionSessionId, SnapshotNumber, Title, PropositionStatement,
                 ReadinessStatusCode, RelianceStatusCode, RelianceReason, IsAttorneyApproved, ApprovedByUserId, ApprovedDateUtc,
                 SupersededBySnapshotId, EvidenceSummaryJson, EvaluatedDateUtc, TenantId, CreatedByUserId)
            VALUES
                (@DecisionSnapshotId, @DecisionMatterId, @DecisionSessionId, @SnapshotNumber, @Title, @PropositionStatement,
                 @ReadinessStatusCode, @RelianceStatusCode, @RelianceReason, @IsAttorneyApproved, @ApprovedByUserId, @ApprovedDateUtc,
                 @SupersededBySnapshotId, @EvidenceSummaryJson, @EvaluatedDateUtc, @TenantId, @ActorUserId);
            """,
            new
            {
                snapshot.DecisionSnapshotId, snapshot.DecisionMatterId, snapshot.DecisionSessionId, snapshot.SnapshotNumber,
                snapshot.Title, snapshot.PropositionStatement, snapshot.ReadinessStatusCode, snapshot.RelianceStatusCode,
                snapshot.RelianceReason, snapshot.IsAttorneyApproved, snapshot.ApprovedByUserId, snapshot.ApprovedDateUtc,
                snapshot.SupersededBySnapshotId, snapshot.EvidenceSummaryJson, snapshot.EvaluatedDateUtc, snapshot.TenantId, snapshot.ActorUserId
            },
            cancellationToken: cancellationToken));
        return snapshot.DecisionSnapshotId;
    }

    private const string SnapshotColumns =
        "DecisionSnapshotId, DecisionMatterId, DecisionSessionId, SnapshotNumber, Title, PropositionStatement, " +
        "ReadinessStatusCode, RelianceStatusCode, RelianceReason, IsAttorneyApproved, SupersededBySnapshotId, EvaluatedDateUtc, CreatedDateUtc";

    public async Task<DecisionSnapshotDto?> GetSnapshotAsync(Guid tenantId, Guid decisionSnapshotId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DecisionSnapshotDto>(new CommandDefinition(
            $"SELECT {SnapshotColumns} FROM POLOXI.Legal_DecisionSnapshot WHERE TenantId=@tenantId AND DecisionSnapshotId=@decisionSnapshotId AND IsDeleted=0;",
            new { tenantId, decisionSnapshotId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<DecisionSnapshotDto>> GetMatterSnapshotsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionSnapshotDto>(new CommandDefinition(
            $"SELECT {SnapshotColumns} FROM POLOXI.Legal_DecisionSnapshot WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId AND IsDeleted=0 ORDER BY SnapshotNumber DESC, CreatedDateUtc DESC;",
            new { tenantId, decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<DecisionSnapshotDto?> GetLatestMatterSnapshotAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<DecisionSnapshotDto>(new CommandDefinition(
            $"SELECT TOP 1 {SnapshotColumns} FROM POLOXI.Legal_DecisionSnapshot WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId AND IsDeleted=0 ORDER BY SnapshotNumber DESC, CreatedDateUtc DESC;",
            new { tenantId, decisionMatterId }, cancellationToken: cancellationToken));
    }

    public async Task<int> GetNextSnapshotNumberAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var current = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT MAX(SnapshotNumber) FROM POLOXI.Legal_DecisionSnapshot WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId AND IsDeleted=0;",
            new { tenantId, decisionMatterId }, cancellationToken: cancellationToken));
        return (current ?? 0) + 1;
    }

    public async Task<bool> UpdateSnapshotRelianceAsync(Guid tenantId, Guid userId, Guid decisionSnapshotId, string relianceStatusCode, string? relianceReason, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE POLOXI.Legal_DecisionSnapshot
               SET RelianceStatusCode=@relianceStatusCode, RelianceReason=@relianceReason,
                   ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@userId
             WHERE TenantId=@tenantId AND DecisionSnapshotId=@decisionSnapshotId AND IsDeleted=0;
            """,
            new { tenantId, userId, decisionSnapshotId, relianceStatusCode, relianceReason }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    // ── Proposition ↔ evidence links + dependencies ─────────────────────────────────────────────

    public async Task SavePropositionEvidenceLinksAsync(IReadOnlyCollection<PropositionEvidenceLinkPersistence> links, CancellationToken cancellationToken = default)
    {
        if (links.Count == 0) return;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_PropositionEvidenceLink
                (PropositionEvidenceLinkId, DecisionMatterId, DecisionSnapshotId, PropositionKey, PropositionStatement,
                 LegalDocumentVersionId, LegalDocumentPassageId, LinkKindCode, SupportWeight, StatusCode, Notes, TenantId, CreatedByUserId)
            VALUES
                (@PropositionEvidenceLinkId, @DecisionMatterId, @DecisionSnapshotId, @PropositionKey, @PropositionStatement,
                 @LegalDocumentVersionId, @LegalDocumentPassageId, @LinkKindCode, @SupportWeight, @StatusCode, @Notes, @TenantId, @ActorUserId);
            """, links, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<PropositionEvidenceLinkPersistence>> GetPropositionEvidenceLinksAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PropositionEvidenceLinkPersistence>(new CommandDefinition("""
            SELECT PropositionEvidenceLinkId, DecisionMatterId, DecisionSnapshotId, PropositionKey, PropositionStatement,
                   LegalDocumentVersionId, LegalDocumentPassageId, LinkKindCode, SupportWeight, StatusCode, Notes,
                   TenantId, CreatedByUserId AS ActorUserId
              FROM POLOXI.Legal_PropositionEvidenceLink
             WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId AND IsDeleted=0;
            """, new { tenantId, decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task SaveDependenciesAsync(IReadOnlyCollection<MatterDependencyPersistence> dependencies, CancellationToken cancellationToken = default)
    {
        if (dependencies.Count == 0) return;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_MatterDependency
                (DecisionDependencyId, DecisionMatterId, DecisionSnapshotId, DependentKindCode, DependentKey,
                 DependsOnKindCode, DependsOnKey, RelationCode, IsEssential, SupportWeight, StatusCode, TenantId, CreatedByUserId)
            VALUES
                (@DecisionDependencyId, @DecisionMatterId, @DecisionSnapshotId, @DependentKindCode, @DependentKey,
                 @DependsOnKindCode, @DependsOnKey, @RelationCode, @IsEssential, @SupportWeight, @StatusCode, @TenantId, @ActorUserId);
            """, dependencies, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<string>> GetDependentKeysForPropositionAsync(Guid tenantId, Guid decisionMatterId, string dependsOnKindCode, string dependsOnKey, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<string>(new CommandDefinition("""
            SELECT DISTINCT DependentKey
              FROM POLOXI.Legal_MatterDependency
             WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId
               AND DependsOnKindCode=@dependsOnKindCode AND DependsOnKey=@dependsOnKey AND IsDeleted=0;
            """, new { tenantId, decisionMatterId, dependsOnKindCode, dependsOnKey }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    // ── Change events (idempotent) ──────────────────────────────────────────────────────────────

    public async Task<(Guid EventId, bool AlreadyExisted)> CreateChangeEventAsync(MatterChangeEventPersistence changeEvent, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var existing = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT TOP 1 MatterChangeEventId FROM POLOXI.Legal_MatterChangeEvent WHERE DecisionMatterId=@DecisionMatterId AND IdempotencyKey=@IdempotencyKey AND IsDeleted=0;",
            new { changeEvent.DecisionMatterId, changeEvent.IdempotencyKey }, cancellationToken: cancellationToken));
        if (existing is { } existingId)
            return (existingId, true);

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_MatterChangeEvent
                (MatterChangeEventId, DecisionMatterId, ChangeSourceCode, LegalDocumentId, LegalDocumentVersionId, SourceHash,
                 SourceLabel, DocumentDateUtc, IdempotencyKey, ClassificationCode, Summary, CandidateFactsJson,
                 ProcessingStatusCode, ProcessingError, AffectedPropositionCount, AffectedCandidateCount, ProcessedDateUtc, TenantId, CreatedByUserId)
            VALUES
                (@MatterChangeEventId, @DecisionMatterId, @ChangeSourceCode, @LegalDocumentId, @LegalDocumentVersionId, @SourceHash,
                 @SourceLabel, @DocumentDateUtc, @IdempotencyKey, @ClassificationCode, @Summary, @CandidateFactsJson,
                 @ProcessingStatusCode, @ProcessingError, @AffectedPropositionCount, @AffectedCandidateCount, @ProcessedDateUtc, @TenantId, @ActorUserId);
            """,
            new
            {
                changeEvent.MatterChangeEventId, changeEvent.DecisionMatterId, changeEvent.ChangeSourceCode, changeEvent.LegalDocumentId,
                changeEvent.LegalDocumentVersionId, changeEvent.SourceHash, changeEvent.SourceLabel, changeEvent.DocumentDateUtc,
                changeEvent.IdempotencyKey, changeEvent.ClassificationCode, changeEvent.Summary, changeEvent.CandidateFactsJson,
                changeEvent.ProcessingStatusCode, changeEvent.ProcessingError, changeEvent.AffectedPropositionCount,
                changeEvent.AffectedCandidateCount, changeEvent.ProcessedDateUtc, changeEvent.TenantId, changeEvent.ActorUserId
            },
            cancellationToken: cancellationToken));
        return (changeEvent.MatterChangeEventId, false);
    }

    public async Task UpdateChangeEventOutcomeAsync(Guid tenantId, Guid userId, Guid matterChangeEventId, string classificationCode, string processingStatusCode, string? processingError, string? summary, string? candidateFactsJson, int affectedPropositionCount, int affectedCandidateCount, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE POLOXI.Legal_MatterChangeEvent
               SET ClassificationCode=@classificationCode, ProcessingStatusCode=@processingStatusCode, ProcessingError=@processingError,
                   Summary=@summary, CandidateFactsJson=@candidateFactsJson, AffectedPropositionCount=@affectedPropositionCount,
                   AffectedCandidateCount=@affectedCandidateCount, ProcessedDateUtc=SYSUTCDATETIME(),
                   ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@userId
             WHERE TenantId=@tenantId AND MatterChangeEventId=@matterChangeEventId AND IsDeleted=0;
            """,
            new { tenantId, userId, matterChangeEventId, classificationCode, processingStatusCode, processingError, summary, candidateFactsJson, affectedPropositionCount, affectedCandidateCount },
            cancellationToken: cancellationToken));
    }

    private const string ChangeEventColumns =
        "MatterChangeEventId, DecisionMatterId, ChangeSourceCode, LegalDocumentId, LegalDocumentVersionId, SourceLabel, " +
        "DocumentDateUtc, ClassificationCode, Summary, ProcessingStatusCode, ProcessingError, AffectedPropositionCount, " +
        "AffectedCandidateCount, ProcessedDateUtc, CreatedDateUtc";

    public async Task<MatterChangeEventDto?> GetChangeEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<MatterChangeEventDto>(new CommandDefinition(
            $"SELECT {ChangeEventColumns} FROM POLOXI.Legal_MatterChangeEvent WHERE TenantId=@tenantId AND MatterChangeEventId=@matterChangeEventId AND IsDeleted=0;",
            new { tenantId, matterChangeEventId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<MatterChangeEventDto>> GetMatterChangeEventsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterChangeEventDto>(new CommandDefinition(
            $"SELECT {ChangeEventColumns} FROM POLOXI.Legal_MatterChangeEvent WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;",
            new { tenantId, decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    // ── Impacts ────────────────────────────────────────────────────────────────────────────────

    public async Task SaveImpactsAsync(IReadOnlyCollection<DecisionImpactPersistence> impacts, CancellationToken cancellationToken = default)
    {
        if (impacts.Count == 0) return;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionImpact
                (DecisionImpactId, MatterChangeEventId, DecisionMatterId, DecisionSnapshotId, AffectedKindCode, AffectedKey,
                 AffectedLabel, PreviousStateCode, CurrentStateCode, ImpactSeverityCode, Rationale, TenantId, CreatedByUserId)
            VALUES
                (@DecisionImpactId, @MatterChangeEventId, @DecisionMatterId, @DecisionSnapshotId, @AffectedKindCode, @AffectedKey,
                 @AffectedLabel, @PreviousStateCode, @CurrentStateCode, @ImpactSeverityCode, @Rationale, @TenantId, @ActorUserId);
            """, impacts, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<DecisionImpactDto>> GetImpactsForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionImpactDto>(new CommandDefinition("""
            SELECT DecisionImpactId, MatterChangeEventId, AffectedKindCode, AffectedKey, AffectedLabel,
                   PreviousStateCode, CurrentStateCode, ImpactSeverityCode, Rationale
              FROM POLOXI.Legal_DecisionImpact
             WHERE TenantId=@tenantId AND MatterChangeEventId=@matterChangeEventId AND IsDeleted=0
             ORDER BY CreatedDateUtc;
            """, new { tenantId, matterChangeEventId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    // ── Review tasks ───────────────────────────────────────────────────────────────────────────

    public async Task<Guid> CreateReviewTaskAsync(DecisionReviewTaskPersistence reviewTask, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionReviewTask
                (DecisionReviewTaskId, DecisionMatterId, MatterChangeEventId, DecisionSnapshotId, TaskKindCode, Title, Detail,
                 RequiredAction, PriorityCode, StatusCode, AssignedToUserId, ResolvedByUserId, ResolvedDateUtc, ResolutionNotes, TenantId, CreatedByUserId)
            VALUES
                (@DecisionReviewTaskId, @DecisionMatterId, @MatterChangeEventId, @DecisionSnapshotId, @TaskKindCode, @Title, @Detail,
                 @RequiredAction, @PriorityCode, @StatusCode, @AssignedToUserId, @ResolvedByUserId, @ResolvedDateUtc, @ResolutionNotes, @TenantId, @ActorUserId);
            """,
            new
            {
                reviewTask.DecisionReviewTaskId, reviewTask.DecisionMatterId, reviewTask.MatterChangeEventId, reviewTask.DecisionSnapshotId,
                reviewTask.TaskKindCode, reviewTask.Title, reviewTask.Detail, reviewTask.RequiredAction, reviewTask.PriorityCode,
                reviewTask.StatusCode, reviewTask.AssignedToUserId, reviewTask.ResolvedByUserId, reviewTask.ResolvedDateUtc,
                reviewTask.ResolutionNotes, reviewTask.TenantId, reviewTask.ActorUserId
            },
            cancellationToken: cancellationToken));
        return reviewTask.DecisionReviewTaskId;
    }

    private const string ReviewTaskColumns =
        "DecisionReviewTaskId, DecisionMatterId, MatterChangeEventId, DecisionSnapshotId, TaskKindCode, Title, Detail, " +
        "RequiredAction, PriorityCode, StatusCode, CreatedDateUtc";

    public async Task<IReadOnlyCollection<DecisionReviewTaskDto>> GetReviewTasksForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionReviewTaskDto>(new CommandDefinition(
            $"SELECT {ReviewTaskColumns} FROM POLOXI.Legal_DecisionReviewTask WHERE TenantId=@tenantId AND MatterChangeEventId=@matterChangeEventId AND IsDeleted=0 ORDER BY CreatedDateUtc;",
            new { tenantId, matterChangeEventId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<DecisionReviewTaskDto>> GetOpenReviewTasksAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionReviewTaskDto>(new CommandDefinition(
            $"SELECT {ReviewTaskColumns} FROM POLOXI.Legal_DecisionReviewTask WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId AND StatusCode IN ('OPEN','IN_PROGRESS') AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;",
            new { tenantId, decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<bool> UpdateReviewTaskStatusAsync(Guid tenantId, Guid userId, Guid decisionReviewTaskId, string statusCode, string? resolutionNotes, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var isTerminal = statusCode is DecisionReviewTaskStatus.Resolved or DecisionReviewTaskStatus.Dismissed;
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE POLOXI.Legal_DecisionReviewTask
               SET StatusCode=@statusCode, ResolutionNotes=@resolutionNotes,
                   ResolvedByUserId = CASE WHEN @isTerminal = 1 THEN @userId ELSE ResolvedByUserId END,
                   ResolvedDateUtc  = CASE WHEN @isTerminal = 1 THEN SYSUTCDATETIME() ELSE ResolvedDateUtc END,
                   ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@userId
             WHERE TenantId=@tenantId AND DecisionReviewTaskId=@decisionReviewTaskId AND IsDeleted=0;
            """,
            new { tenantId, userId, decisionReviewTaskId, statusCode, resolutionNotes, isTerminal },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    // ── Workspace summaries ────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<MatterChangeReviewSummaryDto>> GetChangeReviewSummariesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterChangeReviewSummaryDto>(new CommandDefinition("""
            SELECT m.DecisionMatterId,
                   m.Title AS MatterTitle,
                   (SELECT COUNT(1) FROM POLOXI.Legal_DecisionReviewTask t
                     WHERE t.DecisionMatterId=m.DecisionMatterId AND t.StatusCode IN ('OPEN','IN_PROGRESS') AND t.IsDeleted=0) AS OpenReviewTaskCount,
                   (SELECT COUNT(1) FROM POLOXI.Legal_MatterChangeEvent e
                     WHERE e.DecisionMatterId=m.DecisionMatterId AND e.ProcessingStatusCode='PENDING' AND e.IsDeleted=0) AS PendingChangeCount,
                   (SELECT TOP 1 s.RelianceStatusCode FROM POLOXI.Legal_DecisionSnapshot s
                     WHERE s.DecisionMatterId=m.DecisionMatterId AND s.IsDeleted=0
                     ORDER BY s.SnapshotNumber DESC, s.CreatedDateUtc DESC) AS LatestRelianceStatusCode,
                   (SELECT MAX(e.CreatedDateUtc) FROM POLOXI.Legal_MatterChangeEvent e
                     WHERE e.DecisionMatterId=m.DecisionMatterId AND e.IsDeleted=0) AS LatestChangeDateUtc
              FROM POLOXI.Legal_DecisionMatter m
             WHERE m.TenantId=@tenantId AND m.IsDeleted=0
               AND EXISTS (SELECT 1 FROM POLOXI.Legal_MatterChangeEvent e WHERE e.DecisionMatterId=m.DecisionMatterId AND e.IsDeleted=0)
             ORDER BY LatestChangeDateUtc DESC;
            """, new { tenantId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    // ── Decision Change Intelligence — first-class DecisionDelta (migration 0344; append-only) ────────

    public async Task<Guid> CreateDecisionDeltaAsync(DecisionDeltaPersistence delta, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionDelta
                (DecisionDeltaId, DecisionMatterId, MatterChangeEventId, FromSnapshotId, ToSnapshotId, DeltaKindCode,
                 ClassificationCode, Summary, ChangeSourceLabel, WinnerChanged, PreviousWinnerId, CurrentWinnerId,
                 PreviousWinnerLabel, CurrentWinnerLabel, PreviousMargin, CurrentMargin, PreviousEntropy, CurrentEntropy,
                 AffectedPropositionCount, AffectedCandidateCount, AffectedEvidenceCount, InformationValueDelta, FrontierChanged,
                 PreviousReadinessCode, CurrentReadinessCode, AttorneyReviewRequired, RequiredAction, DetailJson,
                 OccurredDateUtc, TenantId, CreatedByUserId)
            VALUES
                (@DecisionDeltaId, @DecisionMatterId, @MatterChangeEventId, @FromSnapshotId, @ToSnapshotId, @DeltaKindCode,
                 @ClassificationCode, @Summary, @ChangeSourceLabel, @WinnerChanged, @PreviousWinnerId, @CurrentWinnerId,
                 @PreviousWinnerLabel, @CurrentWinnerLabel, @PreviousMargin, @CurrentMargin, @PreviousEntropy, @CurrentEntropy,
                 @AffectedPropositionCount, @AffectedCandidateCount, @AffectedEvidenceCount, @InformationValueDelta, @FrontierChanged,
                 @PreviousReadinessCode, @CurrentReadinessCode, @AttorneyReviewRequired, @RequiredAction, @DetailJson,
                 @OccurredDateUtc, @TenantId, @ActorUserId);
            """,
            new
            {
                delta.DecisionDeltaId, delta.DecisionMatterId, delta.MatterChangeEventId, delta.FromSnapshotId, delta.ToSnapshotId,
                delta.DeltaKindCode, delta.ClassificationCode, delta.Summary, delta.ChangeSourceLabel, delta.WinnerChanged,
                delta.PreviousWinnerId, delta.CurrentWinnerId, delta.PreviousWinnerLabel, delta.CurrentWinnerLabel,
                delta.PreviousMargin, delta.CurrentMargin, delta.PreviousEntropy, delta.CurrentEntropy,
                delta.AffectedPropositionCount, delta.AffectedCandidateCount, delta.AffectedEvidenceCount,
                delta.InformationValueDelta, delta.FrontierChanged, delta.PreviousReadinessCode, delta.CurrentReadinessCode,
                delta.AttorneyReviewRequired, delta.RequiredAction, delta.DetailJson, delta.OccurredDateUtc, delta.TenantId, delta.ActorUserId
            },
            cancellationToken: cancellationToken));
        return delta.DecisionDeltaId;
    }

    private const string DeltaColumns =
        "DecisionDeltaId, DecisionMatterId, MatterChangeEventId, FromSnapshotId, ToSnapshotId, DeltaKindCode, " +
        "ClassificationCode, Summary, ChangeSourceLabel, WinnerChanged, PreviousWinnerId, CurrentWinnerId, " +
        "PreviousWinnerLabel, CurrentWinnerLabel, PreviousMargin, CurrentMargin, PreviousEntropy, CurrentEntropy, " +
        "AffectedPropositionCount, AffectedCandidateCount, AffectedEvidenceCount, InformationValueDelta, FrontierChanged, " +
        "PreviousReadinessCode, CurrentReadinessCode, AttorneyReviewRequired, RequiredAction, OccurredDateUtc, CreatedDateUtc";

    public async Task<IReadOnlyCollection<DecisionDeltaDto>> GetMatterDecisionDeltasAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionDeltaDto>(new CommandDefinition(
            $"SELECT {DeltaColumns} FROM POLOXI.Legal_DecisionDelta WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId AND IsDeleted=0 ORDER BY OccurredDateUtc DESC, CreatedDateUtc DESC;",
            new { tenantId, decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<DecisionDeltaDto?> GetDecisionDeltaForEventAsync(Guid tenantId, Guid matterChangeEventId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<DecisionDeltaDto>(new CommandDefinition(
            $"SELECT TOP 1 {DeltaColumns} FROM POLOXI.Legal_DecisionDelta WHERE TenantId=@tenantId AND MatterChangeEventId=@matterChangeEventId AND IsDeleted=0 ORDER BY OccurredDateUtc DESC, CreatedDateUtc DESC;",
            new { tenantId, matterChangeEventId }, cancellationToken: cancellationToken));
    }
}
