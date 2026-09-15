using System.Data;
using System.Globalization;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper repository for the self-contained POLOXI Legal Decision module. All configuration and
// decision state is read from / written to POLOXI.Legal_Decision* tables (DB is the source of truth).
public sealed class LegalDecisionRepository(ISqlConnectionFactory connectionFactory) : ILegalDecisionRepository
{
    public async Task<DecisionCoreSettings> GetCoreSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string SettingKey, string SettingValue)>(new CommandDefinition(
            "SELECT SettingKey, SettingValue FROM POLOXI.Legal_DecisionSetting WHERE IsDeleted = 0;",
            cancellationToken: cancellationToken));
        var map = rows.ToDictionary(r => r.SettingKey, r => r.SettingValue, StringComparer.OrdinalIgnoreCase);

        double D(string key, double fallback) => map.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : fallback;
        int I(string key, int fallback) => map.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : fallback;

        return new DecisionCoreSettings(
            D("Decision.InformationValue.Weight.Uncertainty", 0.20),
            D("Decision.InformationValue.Weight.RankingImpact", 0.25),
            D("Decision.InformationValue.Weight.Discrimination", 0.25),
            D("Decision.InformationValue.Weight.EvidenceAvail", 0.15),
            D("Decision.InformationValue.Weight.Novelty", 0.10),
            D("Decision.InformationValue.Weight.RedundancyPenalty", 0.05),
            D("Decision.Threshold.DecisionRelevance", 0.35),
            D("Decision.Threshold.FlipPotential", 0.25),
            D("Decision.Threshold.Reopen", 0.15),
            D("Decision.Threshold.ResearchExhaustionAdv", 0.10),
            D("Decision.Threshold.TransformationRelevance", 0.30),
            D("Decision.Threshold.DeepeningFlip", 0.40),
            I("Decision.MaxDepth", 4),
            I("Decision.MaxLlmCalls", 24),
            I("Decision.MaxCandidates", 8));
    }

    public async Task<DecisionV2Settings> GetV2SettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string SettingKey, string SettingValue)>(new CommandDefinition(
            "SELECT SettingKey, SettingValue FROM POLOXI.Legal_DecisionSetting WHERE IsDeleted = 0;",
            cancellationToken: cancellationToken));
        var map = rows.ToDictionary(r => r.SettingKey, r => r.SettingValue, StringComparer.OrdinalIgnoreCase);

        bool B(string key, bool fallback) => map.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;
        double D(string key, double fallback) => map.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : fallback;
        int I(string key, int fallback) => map.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : fallback;

        return new DecisionV2Settings(
            B("Decision.V2.UseDependencyGraph.Default", false),
            D("Decision.V2.Materiality.Threshold", 0.50),
            D("Decision.V2.Readiness.MinAuthorityVerified", 1.00),
            D("Decision.V2.Readiness.LosingSideMargin", 0.05),
            I("Decision.V2.Propagation.MaxDepth", 6),
            I("Decision.V2.Readiness.MaxHighImpactFrontier", 0));
    }

    public async Task<IReadOnlyCollection<DecisionContextDto>> GetContextsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionContextDto>(new CommandDefinition(
            "SELECT ContextCode, DisplayName, Description, IsDefault FROM POLOXI.Legal_DecisionContext WHERE IsDeleted = 0 AND IsActive = 1 ORDER BY SortOrder, DisplayName;",
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<DecisionModelRouteDto>> GetModelRoutesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionModelRouteDto>(new CommandDefinition(
            """
            SELECT FeatureCode, ProviderTypeCode, ModelCode, DeploymentName, EndpointReference, CredentialReference,
                   ApiVersion, TimeoutSeconds, MaxOutputTokens, Temperature, Priority
            FROM POLOXI.Legal_DecisionModelRoute
            WHERE IsDeleted = 0 AND IsActive = 1
            ORDER BY Priority;
            """,
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<DecisionPromptDefinition?> GetPromptAsync(string promptCode, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DecisionPromptDefinition>(new CommandDefinition(
            """
            SELECT TOP 1 PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson
            FROM POLOXI.Legal_DecisionPrompt
            WHERE IsDeleted = 0 AND IsActive = 1 AND PromptCode = @PromptCode;
            """,
            new { PromptCode = promptCode },
            cancellationToken: cancellationToken));
    }

    public async Task PersistSessionAsync(DecisionSessionPersistence session, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionSession
                (DecisionSessionId, QueryText, ContextCode, ModelCode, UsePoloxiEngine, StatusCode, TerminalStateCode,
                 TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy, DecisionMargin, DepthReached,
                 LlmCallCount, DurationMs, FinalAnswer, ClarificationQuestion, ClarificationTarget, CorrelationId,
                 MatterId, NextBestActionText, NextBestActionImpactCode, NextBestActionRationale,
                 TenantId, CreatedByUserId)
            VALUES
                (@DecisionSessionId, @QueryText, @ContextCode, @ModelCode, @UsePoloxiEngine, @StatusCode, @TerminalStateCode,
                 @TerminationReason, @WinnerCandidateId, @ContractCompleteness, @CandidateEntropy, @DecisionMargin, @DepthReached,
                 @LlmCallCount, @DurationMs, @FinalAnswer, @ClarificationQuestion, @ClarificationTarget, @CorrelationId,
                 @MatterId, @NextBestActionText, @NextBestActionImpactCode, @NextBestActionRationale,
                 @TenantId, @ActorUserId);
            """,
            new
            {
                session.DecisionSessionId, session.QueryText, session.ContextCode, session.ModelCode, session.UsePoloxiEngine,
                session.StatusCode, session.TerminalStateCode, session.TerminationReason, session.WinnerCandidateId,
                session.ContractCompleteness, session.CandidateEntropy, session.DecisionMargin, session.DepthReached,
                session.LlmCallCount, session.DurationMs, session.FinalAnswer, session.ClarificationQuestion,
                session.ClarificationTarget, session.CorrelationId, session.MatterId, session.NextBestActionText,
                session.NextBestActionImpactCode, session.NextBestActionRationale, session.TenantId, session.ActorUserId
            },
            transaction, cancellationToken: cancellationToken));

        if (session.Candidates.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionCandidate
                    (DecisionCandidateId, DecisionSessionId, CandidateCode, DisplayName, Outcome, LegalSupport, FactSupport,
                     EvidenceSupport, AuthoritySupport, VerificationScore, Uncertainty, Discrimination, RankingImpact, Diversity,
                     RedundancyPenalty, CompositeScore, DecisionSupportCeiling, RankOrder, IsWinner, IsEliminated, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionCandidateId, @DecisionSessionId, @CandidateCode, @DisplayName, @Outcome, @LegalSupport, @FactSupport,
                     @EvidenceSupport, @AuthoritySupport, @Verification, @Uncertainty, @Discrimination, @RankingImpact, @Diversity,
                     @RedundancyPenalty, @CompositeScore, @DecisionSupportCeiling, @RankOrder, @IsWinner, @IsEliminated, @TenantId, @ActorUserId);
                """,
                session.Candidates.Select(c => new
                {
                    c.DecisionCandidateId, session.DecisionSessionId, c.CandidateCode, c.DisplayName, c.Outcome, c.LegalSupport,
                    c.FactSupport, c.EvidenceSupport, c.AuthoritySupport, c.Verification, c.Uncertainty, c.Discrimination,
                    c.RankingImpact, c.Diversity, c.RedundancyPenalty, c.CompositeScore, c.DecisionSupportCeiling, c.RankOrder,
                    c.IsWinner, c.IsEliminated, session.TenantId, session.ActorUserId
                }),
                transaction, cancellationToken: cancellationToken));

        if (session.Branches.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionBranch
                    (DecisionBranchId, DecisionSessionId, ParentDecisionBranchId, LevelNumber, BranchCode, DisplayName, Interpretation,
                     BranchStateCode, InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability, AdvScore, Cost,
                     IsOnFrontier, StopReason, SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionBranchId, @DecisionSessionId, @ParentDecisionBranchId, @LevelNumber, @BranchCode, @DisplayName, @Interpretation,
                     @BranchStateCode, @InformationValue, @DecisionRelevance, @FlipPotential, @EvidenceAvailability, @AdvScore, @Cost,
                     @IsOnFrontier, @StopReason, @SortOrder, @TenantId, @ActorUserId);
                """,
                session.Branches.Select(b => new
                {
                    b.DecisionBranchId, session.DecisionSessionId, b.ParentDecisionBranchId, b.LevelNumber, b.BranchCode, b.DisplayName,
                    b.Interpretation, b.BranchStateCode, b.InformationValue, b.DecisionRelevance, b.FlipPotential, b.EvidenceAvailability,
                    b.AdvScore, b.Cost, b.IsOnFrontier, b.StopReason, b.SortOrder, session.TenantId, session.ActorUserId
                }),
                transaction, cancellationToken: cancellationToken));

        if (session.Evidence.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionEvidence
                    (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle, Snippet, IdentityFactor,
                     CitationFactor, HoldingFactor, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionEvidenceId, @DecisionSessionId, @DecisionBranchId, @SourceRef, @SourceTitle, @Snippet, @IdentityFactor,
                     @CitationFactor, @HoldingFactor, @WeightFactor, @PropositionFit, @VerificationValue, @VerificationStatus, @TenantId, @ActorUserId);
                """,
                session.Evidence.Select(e => new
                {
                    e.DecisionEvidenceId, session.DecisionSessionId, e.DecisionBranchId, e.SourceRef, e.SourceTitle, e.Snippet,
                    e.IdentityFactor, e.CitationFactor, e.HoldingFactor, e.WeightFactor, e.PropositionFit, e.VerificationValue,
                    e.VerificationStatus, session.TenantId, session.ActorUserId
                }),
                transaction, cancellationToken: cancellationToken));

        if (session.FlipPoints.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionFlipPoint
                    (DecisionFlipPointId, DecisionSessionId, DecisionBranchId, Description, ChangeCost, WinnerChanges, RankDelta, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionFlipPointId, @DecisionSessionId, @DecisionBranchId, @Description, @ChangeCost, @WinnerChanges, @RankDelta, @TenantId, @ActorUserId);
                """,
                session.FlipPoints.Select(f => new
                {
                    f.DecisionFlipPointId, session.DecisionSessionId, f.DecisionBranchId, f.Description, f.ChangeCost, f.WinnerChanges,
                    f.RankDelta, session.TenantId, session.ActorUserId
                }),
                transaction, cancellationToken: cancellationToken));

        if (session.Events.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionEvent
                    (DecisionEventId, DecisionSessionId, ParentDecisionEventId, SequenceNumber, EventType, StageCode, PayloadJson, ProvenanceJson, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionEventId, @DecisionSessionId, @ParentDecisionEventId, @SequenceNumber, @EventType, @StageCode, @PayloadJson, @ProvenanceJson, @TenantId, @ActorUserId);
                """,
                session.Events.Select(ev => new
                {
                    ev.DecisionEventId, session.DecisionSessionId, ev.ParentDecisionEventId, ev.SequenceNumber, ev.EventType,
                    ev.StageCode, ev.PayloadJson, ev.ProvenanceJson, session.TenantId, session.ActorUserId
                }),
                transaction, cancellationToken: cancellationToken));

        transaction.Commit();
    }

    public async Task<DecisionSessionPersistence?> GetSessionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var session = await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
            """
            SELECT DecisionSessionId, TenantId, CreatedByUserId AS ActorUserId, QueryText, ContextCode, ModelCode, UsePoloxiEngine,
                   StatusCode, TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy,
                   DecisionMargin, DepthReached, LlmCallCount, DurationMs, FinalAnswer, ClarificationQuestion, ClarificationTarget, CorrelationId,
                   MatterId, NextBestActionText, NextBestActionImpactCode, NextBestActionRationale
            FROM POLOXI.Legal_DecisionSession
            WHERE IsDeleted = 0 AND TenantId = @TenantId AND DecisionSessionId = @DecisionSessionId;
            """,
            new { TenantId = tenantId, DecisionSessionId = decisionSessionId },
            cancellationToken: cancellationToken));
        if (session is null)
            return null;

        var candidates = (await connection.QueryAsync<DecisionCandidatePersistence>(new CommandDefinition(
            """
            SELECT DecisionCandidateId, CandidateCode, DisplayName, Outcome, LegalSupport, FactSupport, EvidenceSupport,
                   AuthoritySupport, VerificationScore AS Verification, Uncertainty, Discrimination, RankingImpact, Diversity,
                   RedundancyPenalty, CompositeScore, DecisionSupportCeiling, RankOrder, IsWinner, IsEliminated
            FROM POLOXI.Legal_DecisionCandidate WHERE IsDeleted = 0 AND DecisionSessionId = @DecisionSessionId ORDER BY RankOrder;
            """,
            new { DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken))).ToArray();

        var branches = (await connection.QueryAsync<DecisionBranchPersistence>(new CommandDefinition(
            """
            SELECT DecisionBranchId, ParentDecisionBranchId, LevelNumber, BranchCode, DisplayName, Interpretation, BranchStateCode,
                   InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability, AdvScore, Cost, IsOnFrontier, StopReason, SortOrder
            FROM POLOXI.Legal_DecisionBranch WHERE IsDeleted = 0 AND DecisionSessionId = @DecisionSessionId ORDER BY LevelNumber, SortOrder;
            """,
            new { DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken))).ToArray();

        var evidence = (await connection.QueryAsync<DecisionEvidencePersistence>(new CommandDefinition(
            """
            SELECT DecisionEvidenceId, DecisionBranchId, SourceRef, SourceTitle, Snippet, IdentityFactor, CitationFactor,
                   HoldingFactor, WeightFactor, PropositionFit, VerificationValue, VerificationStatus
            FROM POLOXI.Legal_DecisionEvidence WHERE IsDeleted = 0 AND DecisionSessionId = @DecisionSessionId;
            """,
            new { DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken))).ToArray();

        var flipPoints = (await connection.QueryAsync<DecisionFlipPointPersistence>(new CommandDefinition(
            """
            SELECT DecisionFlipPointId, DecisionBranchId, Description, ChangeCost, WinnerChanges, RankDelta
            FROM POLOXI.Legal_DecisionFlipPoint WHERE IsDeleted = 0 AND DecisionSessionId = @DecisionSessionId;
            """,
            new { DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken))).ToArray();

        return new DecisionSessionPersistence(
            session.DecisionSessionId, session.TenantId, session.ActorUserId, session.QueryText, session.ContextCode,
            session.ModelCode, session.UsePoloxiEngine, session.StatusCode, session.TerminalStateCode, session.TerminationReason,
            session.WinnerCandidateId, session.ContractCompleteness, session.CandidateEntropy, session.DecisionMargin,
            session.DepthReached, session.LlmCallCount, session.DurationMs, session.FinalAnswer, session.ClarificationQuestion,
            session.ClarificationTarget, session.CorrelationId, candidates, branches, evidence, flipPoints, [])
        {
            MatterId = session.MatterId,
            NextBestActionText = session.NextBestActionText,
            NextBestActionImpactCode = session.NextBestActionImpactCode,
            NextBestActionRationale = session.NextBestActionRationale
        };
    }

    // ── Matter aggregate + cockpit support ──────────────────────────────────────────────────────
    public async Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterRow>(new CommandDefinition(
            """
            SELECT
                m.DecisionMatterId, m.Title, m.MatterTypeCode, m.Jurisdiction, m.Posture, m.Description, m.StatusCode,
                m.Subtype, m.CourtSystem, m.State, m.CourtLevel, m.County, m.GoverningLaw,
                m.MovingParty, m.RespondingParty, m.MotionTarget, m.RequestedDisposition,
                m.CreatedDateUtc, m.ModifiedDateUtc,
                s.DecisionSessionId AS LatestSessionId,
                c.DisplayName       AS CurrentOutcome,
                s.StatusCode        AS DecisionStatusCode,
                s.CreatedDateUtc    AS LastDecidedUtc,
                (SELECT COUNT(1) FROM POLOXI.Legal_DecisionFlipPoint fp
                    WHERE fp.IsDeleted = 0 AND fp.DecisionSessionId = s.DecisionSessionId AND fp.WinnerChanges = 1) AS CriticalFlipPointCount
            FROM POLOXI.Legal_DecisionMatter m
            OUTER APPLY (
                SELECT TOP 1 ss.DecisionSessionId, ss.StatusCode, ss.WinnerCandidateId, ss.CreatedDateUtc
                FROM POLOXI.Legal_DecisionSession ss
                WHERE ss.IsDeleted = 0 AND ss.MatterId = m.DecisionMatterId
                ORDER BY ss.CreatedDateUtc DESC
            ) s
            LEFT JOIN POLOXI.Legal_DecisionCandidate c
                ON c.IsDeleted = 0 AND c.DecisionCandidateId = s.WinnerCandidateId
            WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId
            ORDER BY COALESCE(m.ModifiedDateUtc, m.CreatedDateUtc) DESC;
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return rows.Select(MapMatter).ToArray();
    }

    public async Task<DecisionMatterDto?> GetMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<MatterRow>(new CommandDefinition(
            """
            SELECT
                m.DecisionMatterId, m.Title, m.MatterTypeCode, m.Jurisdiction, m.Posture, m.Description, m.StatusCode,
                m.Subtype, m.CourtSystem, m.State, m.CourtLevel, m.County, m.GoverningLaw,
                m.MovingParty, m.RespondingParty, m.MotionTarget, m.RequestedDisposition,
                m.CreatedDateUtc, m.ModifiedDateUtc,
                s.DecisionSessionId AS LatestSessionId,
                c.DisplayName       AS CurrentOutcome,
                s.StatusCode        AS DecisionStatusCode,
                s.CreatedDateUtc    AS LastDecidedUtc,
                (SELECT COUNT(1) FROM POLOXI.Legal_DecisionFlipPoint fp
                    WHERE fp.IsDeleted = 0 AND fp.DecisionSessionId = s.DecisionSessionId AND fp.WinnerChanges = 1) AS CriticalFlipPointCount
            FROM POLOXI.Legal_DecisionMatter m
            OUTER APPLY (
                SELECT TOP 1 ss.DecisionSessionId, ss.StatusCode, ss.WinnerCandidateId, ss.CreatedDateUtc
                FROM POLOXI.Legal_DecisionSession ss
                WHERE ss.IsDeleted = 0 AND ss.MatterId = m.DecisionMatterId
                ORDER BY ss.CreatedDateUtc DESC
            ) s
            LEFT JOIN POLOXI.Legal_DecisionCandidate c
                ON c.IsDeleted = 0 AND c.DecisionCandidateId = s.WinnerCandidateId
            WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND m.DecisionMatterId = @DecisionMatterId;
            """,
            new { TenantId = tenantId, DecisionMatterId = decisionMatterId },
            cancellationToken: cancellationToken));
        return row is null ? null : MapMatter(row);
    }

    public async Task<Guid> CreateMatterAsync(Guid tenantId, Guid userId, DecisionMatterCreateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionMatter
                (DecisionMatterId, Title, MatterTypeCode, Jurisdiction, Posture, Description, StatusCode, TenantId, CreatedByUserId,
                 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw, MovingParty, RespondingParty, MotionTarget, RequestedDisposition)
            VALUES
                (@DecisionMatterId, @Title, @MatterTypeCode, @Jurisdiction, @Posture, @Description, N'OPEN', @TenantId, @UserId,
                 @Subtype, @CourtSystem, @State, @CourtLevel, @County, @GoverningLaw, @MovingParty, @RespondingParty, @MotionTarget, @RequestedDisposition);
            """,
            new
            {
                DecisionMatterId = id,
                request.Title,
                request.MatterTypeCode,
                request.Jurisdiction,
                request.Posture,
                request.Description,
                request.Subtype,
                request.CourtSystem,
                request.State,
                request.CourtLevel,
                request.County,
                request.GoverningLaw,
                request.MovingParty,
                request.RespondingParty,
                request.MotionTarget,
                request.RequestedDisposition,
                TenantId = tenantId,
                UserId = userId == Guid.Empty ? (Guid?)null : userId
            },
            cancellationToken: cancellationToken));
        return id;
    }

    public async Task<IReadOnlyCollection<DecisionTimelineEventDto>> GetSessionTimelineAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionTimelineEventDto>(new CommandDefinition(
            """
            SELECT ev.DecisionEventId, ev.SequenceNumber AS Sequence, ev.EventType, COALESCE(ev.StageCode, N'') AS StageCode, ev.PayloadJson, ev.CreatedDateUtc
            FROM POLOXI.Legal_DecisionEvent ev
            INNER JOIN POLOXI.Legal_DecisionSession s ON s.DecisionSessionId = ev.DecisionSessionId
            WHERE ev.IsDeleted = 0 AND ev.DecisionSessionId = @DecisionSessionId AND s.TenantId = @TenantId
            ORDER BY ev.SequenceNumber;
            """,
            new { TenantId = tenantId, DecisionSessionId = decisionSessionId },
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<DecisionSessionSummaryDto>> GetMatterSessionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionSessionSummaryDto>(new CommandDefinition(
            """
            SELECT
                s.DecisionSessionId, s.QueryText, s.StatusCode, s.TerminalStateCode,
                c.DisplayName AS CurrentOutcome, s.DecisionMargin, s.CandidateEntropy,
                s.DepthReached, s.LlmCallCount, s.DurationMs,
                (SELECT COUNT(1) FROM POLOXI.Legal_DecisionFlipPoint fp
                    WHERE fp.IsDeleted = 0 AND fp.DecisionSessionId = s.DecisionSessionId AND fp.WinnerChanges = 1) AS CriticalFlipPointCount,
                s.CreatedDateUtc
            FROM POLOXI.Legal_DecisionSession s
            LEFT JOIN POLOXI.Legal_DecisionCandidate c
                ON c.IsDeleted = 0 AND c.DecisionCandidateId = s.WinnerCandidateId
            WHERE s.IsDeleted = 0 AND s.TenantId = @TenantId AND s.MatterId = @DecisionMatterId
            ORDER BY s.CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, DecisionMatterId = decisionMatterId },
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<bool> UpdateMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, DecisionMatterUpdateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionMatter
            SET Title = @Title,
                MatterTypeCode = @MatterTypeCode,
                Jurisdiction = @Jurisdiction,
                Posture = @Posture,
                Description = @Description,
                Subtype = @Subtype,
                CourtSystem = @CourtSystem,
                State = @State,
                CourtLevel = @CourtLevel,
                County = @County,
                GoverningLaw = @GoverningLaw,
                MovingParty = @MovingParty,
                RespondingParty = @RespondingParty,
                MotionTarget = @MotionTarget,
                RequestedDisposition = @RequestedDisposition,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @UserId
            WHERE IsDeleted = 0 AND TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId;
            """,
            new
            {
                request.Title,
                request.MatterTypeCode,
                request.Jurisdiction,
                request.Posture,
                request.Description,
                request.Subtype,
                request.CourtSystem,
                request.State,
                request.CourtLevel,
                request.County,
                request.GoverningLaw,
                request.MovingParty,
                request.RespondingParty,
                request.MotionTarget,
                request.RequestedDisposition,
                TenantId = tenantId,
                DecisionMatterId = decisionMatterId,
                UserId = userId == Guid.Empty ? (Guid?)null : userId
            },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> UpdateMatterStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, string statusCode, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionMatter
            SET StatusCode = @StatusCode,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @UserId
            WHERE IsDeleted = 0 AND TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId;
            """,
            new
            {
                StatusCode = statusCode,
                TenantId = tenantId,
                DecisionMatterId = decisionMatterId,
                UserId = userId == Guid.Empty ? (Guid?)null : userId
            },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<bool> DeleteMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionMatter
            SET IsDeleted = 1,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @UserId
            WHERE IsDeleted = 0 AND TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId;
            """,
            new
            {
                TenantId = tenantId,
                DecisionMatterId = decisionMatterId,
                UserId = userId == Guid.Empty ? (Guid?)null : userId
            },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<DecisionMatterFacetsDto> GetMatterFacetsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'MATTER_TYPE'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.MatterTypeCode)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND NULLIF(LTRIM(RTRIM(m.MatterTypeCode)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'JURISDICTION'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.Jurisdiction)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND NULLIF(LTRIM(RTRIM(m.Jurisdiction)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'POSTURE'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.Posture)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND NULLIF(LTRIM(RTRIM(m.Posture)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'SUBTYPE'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.Subtype)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND NULLIF(LTRIM(RTRIM(m.Subtype)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'COURT_SYSTEM'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.CourtSystem)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND NULLIF(LTRIM(RTRIM(m.CourtSystem)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'STATE'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.State)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND NULLIF(LTRIM(RTRIM(m.State)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'COURT_LEVEL'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.CourtLevel)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND NULLIF(LTRIM(RTRIM(m.CourtLevel)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'GOVERNING_LAW'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.GoverningLaw)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND NULLIF(LTRIM(RTRIM(m.GoverningLaw)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        var matterTypes = (await multi.ReadAsync<string>()).ToArray();
        var jurisdictions = (await multi.ReadAsync<string>()).ToArray();
        var postures = (await multi.ReadAsync<string>()).ToArray();
        var subtypes = (await multi.ReadAsync<string>()).ToArray();
        var courtSystems = (await multi.ReadAsync<string>()).ToArray();
        var states = (await multi.ReadAsync<string>()).ToArray();
        var courtLevels = (await multi.ReadAsync<string>()).ToArray();
        var governingLaws = (await multi.ReadAsync<string>()).ToArray();
        return new DecisionMatterFacetsDto(matterTypes, jurisdictions, postures)
        {
            Subtypes = subtypes,
            CourtSystems = courtSystems,
            States = states,
            CourtLevels = courtLevels,
            GoverningLaws = governingLaws
        };
    }

    private static DecisionMatterDto MapMatter(MatterRow r) => new(
        r.DecisionMatterId, r.Title, r.MatterTypeCode, r.Jurisdiction, r.Posture, r.Description, r.StatusCode,
        r.CreatedDateUtc, r.ModifiedDateUtc)
    {
        Subtype = r.Subtype,
        CourtSystem = r.CourtSystem,
        State = r.State,
        CourtLevel = r.CourtLevel,
        County = r.County,
        GoverningLaw = r.GoverningLaw,
        MovingParty = r.MovingParty,
        RespondingParty = r.RespondingParty,
        MotionTarget = r.MotionTarget,
        RequestedDisposition = r.RequestedDisposition,
        LatestSessionId = r.LatestSessionId,
        CurrentOutcome = r.CurrentOutcome,
        DecisionStatusCode = r.DecisionStatusCode,
        CriticalFlipPointCount = r.CriticalFlipPointCount,
        LastDecidedUtc = r.LastDecidedUtc
    };

    // ── POLOXI Legal V2 — dependency-aware decision graph persistence ────────────────────────────
    public async Task PersistGraphAsync(DecisionGraphPersistence graph, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        foreach (var n in graph.Nodes)
        {
            var sql = n.NodeKind switch
            {
                DecisionGraphNodeKinds.Fact => """
                    INSERT INTO POLOXI.Legal_DecisionFactProposition
                        (DecisionFactPropositionId, DecisionSessionId, NodeCode, Statement, Support, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @NodeCode, @Statement, @Support, @VerificationStatus, @SortOrder, @TenantId, @ActorUserId);
                    """,
                DecisionGraphNodeKinds.Proposition => """
                    INSERT INTO POLOXI.Legal_DecisionLegalProposition
                        (DecisionLegalPropositionId, DecisionSessionId, NodeCode, Statement, AuthorityRef, Support, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @NodeCode, @Statement, @AuthorityRef, @Support, @VerificationStatus, @SortOrder, @TenantId, @ActorUserId);
                    """,
                DecisionGraphNodeKinds.Element => """
                    INSERT INTO POLOXI.Legal_DecisionLegalElement
                        (DecisionLegalElementId, DecisionSessionId, NodeCode, DisplayName, Statement, IsEssential, IsSatisfied, Support, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @NodeCode, @DisplayName, @Statement, @IsEssential, @IsSatisfied, @Support, @VerificationStatus, @SortOrder, @TenantId, @ActorUserId);
                    """,
                DecisionGraphNodeKinds.Strategy => """
                    INSERT INTO POLOXI.Legal_DecisionReasoningStrategy
                        (DecisionReasoningStrategyId, DecisionSessionId, DecisionCandidateId, NodeCode, DisplayName, Rationale, Support, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @CandidateId, @NodeCode, @DisplayName, @Statement, @Support, @VerificationStatus, @SortOrder, @TenantId, @ActorUserId);
                    """,
                DecisionGraphNodeKinds.Burden => """
                    INSERT INTO POLOXI.Legal_DecisionBurdenRule
                        (DecisionBurdenRuleId, DecisionSessionId, NodeCode, BurdenedParty, StandardOfProof, Statement, IsSatisfied, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @NodeCode, @BurdenedParty, @StandardOfProof, @Statement, @IsSatisfied, @VerificationStatus, @SortOrder, @TenantId, @ActorUserId);
                    """,
                DecisionGraphNodeKinds.Procedure => """
                    INSERT INTO POLOXI.Legal_DecisionProceduralConstraint
                        (DecisionProceduralConstraintId, DecisionSessionId, NodeCode, DisplayName, Statement, IsSatisfied, IsDispositive, VerificationStatus, SortOrder, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @NodeCode, @DisplayName, @Statement, @IsSatisfied, @IsDispositive, @VerificationStatus, @SortOrder, @TenantId, @ActorUserId);
                    """,
                _ => null
            };
            if (sql is null)
                continue;

            await connection.ExecuteAsync(new CommandDefinition(sql, new
            {
                NodeId = n.NodeId,
                SessionId = graph.DecisionSessionId,
                n.NodeCode,
                n.DisplayName,
                n.Statement,
                n.Support,
                n.IsEssential,
                n.IsSatisfied,
                n.IsDispositive,
                n.VerificationStatus,
                n.SortOrder,
                n.AuthorityRef,
                n.BurdenedParty,
                n.StandardOfProof,
                CandidateId = n.CandidateId,
                graph.TenantId,
                graph.ActorUserId
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var e in graph.Edges)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO POLOXI.Legal_DecisionGraphEdge
                    (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId,
                     SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, VerificationNotes, PropagatedStateCode, TenantId, CreatedByUserId)
                VALUES (@EdgeId, @SessionId, @RelationCode, @SourceNodeKind, @SourceNodeId, @TargetNodeKind, @TargetNodeId,
                     @SupportWeight, @Materiality, @IsEssential, @IsDispositive, @VerificationStatus, @VerificationNotes, @PropagatedStateCode, @TenantId, @ActorUserId);
                """, new
            {
                EdgeId = e.EdgeId,
                SessionId = graph.DecisionSessionId,
                e.RelationCode,
                e.SourceNodeKind,
                e.SourceNodeId,
                e.TargetNodeKind,
                e.TargetNodeId,
                e.SupportWeight,
                e.Materiality,
                e.IsEssential,
                e.IsDispositive,
                e.VerificationStatus,
                e.VerificationNotes,
                e.PropagatedStateCode,
                graph.TenantId,
                graph.ActorUserId
            }, transaction, cancellationToken: cancellationToken));
        }

        if (graph.LosingSideTest is { } lst)
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO POLOXI.Legal_DecisionLosingSideTest
                    (DecisionLosingSideTestId, DecisionSessionId, WinnerCandidateId, ChallengerCandidateId, StrongestCaseSummary,
                     ChallengerStrength, WinnerStrength, WinnerSurvived, TenantId, CreatedByUserId)
                VALUES (@Id, @SessionId, @WinnerCandidateId, @ChallengerCandidateId, @StrongestCaseSummary,
                     @ChallengerStrength, @WinnerStrength, @WinnerSurvived, @TenantId, @ActorUserId);
                """, new
            {
                Id = lst.DecisionLosingSideTestId,
                SessionId = graph.DecisionSessionId,
                lst.WinnerCandidateId,
                lst.ChallengerCandidateId,
                lst.StrongestCaseSummary,
                lst.ChallengerStrength,
                lst.WinnerStrength,
                lst.WinnerSurvived,
                graph.TenantId,
                graph.ActorUserId
            }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE POLOXI.Legal_DecisionSession
               SET ReadinessSatisfied = @ReadinessSatisfied,
                   ReadinessBlockersJson = @ReadinessBlockersJson,
                   ModifiedDateUtc = SYSUTCDATETIME(),
                   ModifiedByUserId = @ActorUserId
             WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId;
            """, new
        {
            SessionId = graph.DecisionSessionId,
            graph.TenantId,
            graph.ActorUserId,
            graph.ReadinessSatisfied,
            graph.ReadinessBlockersJson
        }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
    }

    public async Task<DecisionGraphPersistence?> GetGraphAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var nodes = new List<DecisionGraphNodePersistence>();

        var facts = await connection.QueryAsync<GraphNodeRow>(new CommandDefinition("""
            SELECT DecisionFactPropositionId AS NodeId, NodeCode, N'' AS DisplayName, Statement, Support, CAST(0 AS BIT) AS IsEssential,
                   CAST(0 AS BIT) AS IsSatisfied, CAST(0 AS BIT) AS IsDispositive, VerificationStatus, SortOrder, CAST(NULL AS NVARCHAR(256)) AS AuthorityRef, CAST(NULL AS NVARCHAR(256)) AS BurdenedParty, CAST(NULL AS NVARCHAR(256)) AS StandardOfProof, CAST(NULL AS UNIQUEIDENTIFIER) AS CandidateId
            FROM POLOXI.Legal_DecisionFactProposition WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        nodes.AddRange(facts.Select(r => Map(r, DecisionGraphNodeKinds.Fact)));

        var props = await connection.QueryAsync<GraphNodeRow>(new CommandDefinition("""
            SELECT DecisionLegalPropositionId AS NodeId, NodeCode, N'' AS DisplayName, Statement, Support, CAST(0 AS BIT) AS IsEssential,
                   CAST(0 AS BIT) AS IsSatisfied, CAST(0 AS BIT) AS IsDispositive, VerificationStatus, SortOrder, AuthorityRef, CAST(NULL AS NVARCHAR(256)) AS BurdenedParty, CAST(NULL AS NVARCHAR(256)) AS StandardOfProof, CAST(NULL AS UNIQUEIDENTIFIER) AS CandidateId
            FROM POLOXI.Legal_DecisionLegalProposition WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        nodes.AddRange(props.Select(r => Map(r, DecisionGraphNodeKinds.Proposition)));

        var elems = await connection.QueryAsync<GraphNodeRow>(new CommandDefinition("""
            SELECT DecisionLegalElementId AS NodeId, NodeCode, DisplayName, Statement, Support, IsEssential,
                   IsSatisfied, CAST(0 AS BIT) AS IsDispositive, VerificationStatus, SortOrder, CAST(NULL AS NVARCHAR(256)) AS AuthorityRef, CAST(NULL AS NVARCHAR(256)) AS BurdenedParty, CAST(NULL AS NVARCHAR(256)) AS StandardOfProof, CAST(NULL AS UNIQUEIDENTIFIER) AS CandidateId
            FROM POLOXI.Legal_DecisionLegalElement WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        nodes.AddRange(elems.Select(r => Map(r, DecisionGraphNodeKinds.Element)));

        var strategies = await connection.QueryAsync<GraphNodeRow>(new CommandDefinition("""
            SELECT DecisionReasoningStrategyId AS NodeId, NodeCode, DisplayName, Rationale AS Statement, Support, CAST(0 AS BIT) AS IsEssential,
                   IsViable AS IsSatisfied, CAST(0 AS BIT) AS IsDispositive, VerificationStatus, SortOrder, CAST(NULL AS NVARCHAR(256)) AS AuthorityRef, CAST(NULL AS NVARCHAR(256)) AS BurdenedParty, CAST(NULL AS NVARCHAR(256)) AS StandardOfProof, DecisionCandidateId AS CandidateId
            FROM POLOXI.Legal_DecisionReasoningStrategy WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        nodes.AddRange(strategies.Select(r => Map(r, DecisionGraphNodeKinds.Strategy)));

        var burdens = await connection.QueryAsync<GraphNodeRow>(new CommandDefinition("""
            SELECT DecisionBurdenRuleId AS NodeId, NodeCode, BurdenedParty AS DisplayName, Statement, CAST(0 AS DECIMAL(5,4)) AS Support, CAST(0 AS BIT) AS IsEssential,
                   IsSatisfied, CAST(0 AS BIT) AS IsDispositive, VerificationStatus, SortOrder, CAST(NULL AS NVARCHAR(256)) AS AuthorityRef, BurdenedParty, StandardOfProof, CAST(NULL AS UNIQUEIDENTIFIER) AS CandidateId
            FROM POLOXI.Legal_DecisionBurdenRule WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        nodes.AddRange(burdens.Select(r => Map(r, DecisionGraphNodeKinds.Burden)));

        var procs = await connection.QueryAsync<GraphNodeRow>(new CommandDefinition("""
            SELECT DecisionProceduralConstraintId AS NodeId, NodeCode, DisplayName, Statement, CAST(0 AS DECIMAL(5,4)) AS Support, CAST(0 AS BIT) AS IsEssential,
                   IsSatisfied, IsDispositive, VerificationStatus, SortOrder, CAST(NULL AS NVARCHAR(256)) AS AuthorityRef, CAST(NULL AS NVARCHAR(256)) AS BurdenedParty, CAST(NULL AS NVARCHAR(256)) AS StandardOfProof, CAST(NULL AS UNIQUEIDENTIFIER) AS CandidateId
            FROM POLOXI.Legal_DecisionProceduralConstraint WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        nodes.AddRange(procs.Select(r => Map(r, DecisionGraphNodeKinds.Procedure)));

        var edgeRows = await connection.QueryAsync<DecisionGraphEdgePersistence>(new CommandDefinition("""
            SELECT DecisionGraphEdgeId AS EdgeId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId,
                   SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, VerificationNotes, PropagatedStateCode
            FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        var edges = edgeRows.ToArray();

        var losing = await connection.QuerySingleOrDefaultAsync<DecisionLosingSideTestPersistence>(new CommandDefinition("""
            SELECT TOP 1 DecisionLosingSideTestId, WinnerCandidateId, ChallengerCandidateId, StrongestCaseSummary,
                   ChallengerStrength, WinnerStrength, WinnerSurvived
            FROM POLOXI.Legal_DecisionLosingSideTest WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0
            ORDER BY CreatedDateUtc DESC;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));

        var session = await connection.QuerySingleOrDefaultAsync<(bool? ReadinessSatisfied, string? ReadinessBlockersJson)>(new CommandDefinition("""
            SELECT ReadinessSatisfied, ReadinessBlockersJson FROM POLOXI.Legal_DecisionSession
            WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));

        if (nodes.Count == 0 && edges.Length == 0 && losing is null)
            return null;

        return new DecisionGraphPersistence(
            decisionSessionId, tenantId, null,
            session.ReadinessSatisfied ?? false, session.ReadinessBlockersJson,
            nodes, edges, losing);
    }

    public async Task UpdateEdgeVerificationAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionGraphEdgePersistence> edges, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        foreach (var e in edges)
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE POLOXI.Legal_DecisionGraphEdge
                   SET VerificationStatus = @VerificationStatus,
                       VerificationNotes = @VerificationNotes,
                       PropagatedStateCode = @PropagatedStateCode,
                       ModifiedDateUtc = SYSUTCDATETIME(),
                       ModifiedByUserId = @UserId
                 WHERE DecisionGraphEdgeId = @EdgeId AND DecisionSessionId = @SessionId AND TenantId = @TenantId;
                """, new
            {
                e.EdgeId,
                e.VerificationStatus,
                e.VerificationNotes,
                e.PropagatedStateCode,
                SessionId = decisionSessionId,
                TenantId = tenantId,
                UserId = userId
            }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
    }

    private static DecisionGraphNodePersistence Map(GraphNodeRow r, string kind) => new(
        r.NodeId, kind, r.NodeCode, r.DisplayName ?? "", r.Statement, r.Support,
        r.IsEssential, r.IsSatisfied, r.VerificationStatus, r.SortOrder)
    {
        AuthorityRef = r.AuthorityRef,
        BurdenedParty = r.BurdenedParty,
        StandardOfProof = r.StandardOfProof,
        CandidateId = r.CandidateId,
        IsDispositive = r.IsDispositive
    };

    private sealed record GraphNodeRow(
        Guid NodeId, string NodeCode, string? DisplayName, string? Statement, decimal Support, bool IsEssential,
        bool IsSatisfied, bool IsDispositive, string VerificationStatus, int SortOrder,
        string? AuthorityRef, string? BurdenedParty, string? StandardOfProof, Guid? CandidateId);

    private sealed record MatterRow(
        Guid DecisionMatterId, string Title, string? MatterTypeCode, string? Jurisdiction, string? Posture, string? Description,
        string StatusCode,
        string? Subtype, string? CourtSystem, string? State, string? CourtLevel, string? County, string? GoverningLaw,
        string? MovingParty, string? RespondingParty, string? MotionTarget, string? RequestedDisposition,
        DateTime CreatedDateUtc, DateTime? ModifiedDateUtc, Guid? LatestSessionId, string? CurrentOutcome,
        string? DecisionStatusCode, DateTime? LastDecidedUtc, int CriticalFlipPointCount);

    private sealed record SessionRow(
        Guid DecisionSessionId, Guid TenantId, Guid? ActorUserId, string QueryText, string? ContextCode, string? ModelCode,
        bool UsePoloxiEngine, string StatusCode, string? TerminalStateCode, string TerminationReason, Guid? WinnerCandidateId,
        decimal ContractCompleteness, decimal CandidateEntropy, decimal DecisionMargin, int DepthReached, int LlmCallCount,
        long DurationMs, string? FinalAnswer, string? ClarificationQuestion, string? ClarificationTarget, string? CorrelationId,
        Guid? MatterId, string? NextBestActionText, string? NextBestActionImpactCode, string? NextBestActionRationale);
}
