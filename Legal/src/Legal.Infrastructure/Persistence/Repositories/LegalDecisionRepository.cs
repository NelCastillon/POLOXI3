using System.Data;
using System.Globalization;
using System.Text.Json;
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

        bool B(string key, bool fallback) => map.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

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
            I("Decision.MaxCandidates", 8),
            B("Decision.ProposalIntegrity.EnableRecovery", false))
        {
            Verification = new DecisionVerificationSettings
            {
                Enabled = B("Decision.Verification.Enabled", true),
                ShadowMode = B("Decision.Verification.ShadowMode", true),
                MechanicalVerificationEnabled = B("Decision.Verification.Mechanical.Enabled", true),
                SemanticVerificationEnabled = B("Decision.Verification.Semantic.Enabled", true),
                MaxInputTokensPerEvidence = I("Decision.Verification.Semantic.MaxInputTokens", 2500),
                MaxOutputTokensPerEvidence = I("Decision.Verification.Semantic.MaxOutputTokens", 700),
                AllowSchemaRepair = B("Decision.Verification.Semantic.AllowSchemaRepair", true),
                MaxSchemaRepairAttempts = I("Decision.Verification.Semantic.MaxSchemaRepairAttempts", 1),
                PoloxiDeepeningEnabled = B("Decision.Verification.PoloxiDeepening.Enabled", false),
                PoloxiDecisionMaterialOnly = B("Decision.Verification.PoloxiDeepening.DecisionMaterialOnly", true),
                PoloxiMaxRounds = I("Decision.Verification.PoloxiDeepening.MaxRounds", 1),
                EnforceVerifiedEvidenceOnly = B("Decision.Verification.Authorization.EnforceVerifiedOnly", false),
                CacheEnabled = B("Decision.Verification.Cache.Enabled", true),
            },
        };
    }

    public async Task PersistOutputClaimProvenanceAsync(
        IReadOnlyCollection<DecisionOutputClaimProvenancePersistence> provenance,
        CancellationToken cancellationToken = default)
    {
        if (provenance.Count == 0)
            return;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionOutputClaimProvenance
                (OutputClaimProvenanceId, DecisionSessionId, ClaimId, SourceBranchId, SourceCandidateId,
                 DecisionEvidenceId, DecisionEvidenceAttachmentId, DecisionEvidenceVerificationId,
                 SourceSnapshotId, PassageRef, ClaimText, IsMaterial, MappingStateCode,
                 SourcePropositionId, MappingReasonCode, DispositionCode, TenantId, CreatedByUserId)
            VALUES
                (@OutputClaimProvenanceId, @DecisionSessionId, @ClaimId, @SourceBranchId, @SourceCandidateId,
                 @DecisionEvidenceId, @DecisionEvidenceAttachmentId, @DecisionEvidenceVerificationId,
                 @SourceSnapshotId, @PassageRef, @ClaimText, @IsMaterial, @MappingStateCode,
                 @SourcePropositionId, @MappingReasonCode, @DispositionCode, @TenantId, @ActorUserId);
            """, provenance, cancellationToken: cancellationToken));
    }

    public async Task PersistEvidenceVerificationsAsync(
        IReadOnlyCollection<DecisionEvidenceVerificationPersistence> verifications,
        CancellationToken cancellationToken = default)
    {
        if (verifications.Count == 0)
            return;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        foreach (var verification in verifications)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                IF @SourceSnapshotId IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM POLOXI.Legal_EvidenceSourceSnapshot WHERE SourceSnapshotId = @SourceSnapshotId)
                BEGIN
                    INSERT INTO POLOXI.Legal_EvidenceSourceSnapshot
                        (SourceSnapshotId, DecisionEvidenceId, SourceProvider, SourceVersion, SourceRef,
                         ContentHash, PassageHash, PassageRef, ExtractionVersion, RetrievedDateUtc, TenantId)
                    VALUES
                        (@SourceSnapshotId, @DecisionEvidenceId, @SourceProvider, @SourceVersion,
                         @SourceRef,
                         @SourceContentHash, @PassageHash, @PassageRef, @ExtractionVersion, @EvaluatedDateUtc, @TenantId);
                END;

                DECLARE @NextVerificationVersion INT =
                    ISNULL((SELECT MAX(VerificationVersion)
                            FROM POLOXI.Legal_DecisionEvidenceVerification WITH (UPDLOCK, HOLDLOCK)
                            WHERE DecisionEvidenceId = @DecisionEvidenceId), 0) + 1;

                UPDATE POLOXI.Legal_DecisionEvidenceVerification
                   SET IsDeleted = 1
                 WHERE DecisionEvidenceId = @DecisionEvidenceId AND TenantId = @TenantId AND IsDeleted = 0;

                INSERT INTO POLOXI.Legal_DecisionEvidenceVerification
                    (DecisionEvidenceVerificationId, DecisionEvidenceId, DecisionSessionId, DecisionBranchId,
                     SourceTypeCode, ProfileCode, DispositionCode, IsVerified, IsDecisionAuthorized,
                     BlockingReasonsJson, MatterId, TenantId, EvaluatedDateUtc, CreatedByUserId,
                     MechanicalVerificationCount, SemanticVerificationCount, PoloxiDeepeningCount,
                     CacheHitCount, InputTokenCount, OutputTokenCount, LatencyMilliseconds,
                     SourceSnapshotId, ProfileVersion, VerificationVersion, RetrievedCount, PreScreenRejectedCount)
                VALUES
                    (@DecisionEvidenceVerificationId, @DecisionEvidenceId, @DecisionSessionId, @DecisionBranchId,
                     @SourceTypeCode, @ProfileCode, @DispositionCode, @IsVerified, @IsDecisionAuthorized,
                     @BlockingReasonsJson, @MatterId, @TenantId, @EvaluatedDateUtc, @ActorUserId,
                     @MechanicalVerificationCount, @SemanticVerificationCount, @PoloxiDeepeningCount,
                     @CacheHitCount, @InputTokenCount, @OutputTokenCount, @LatencyMilliseconds,
                     @SourceSnapshotId, @ProfileVersion, @NextVerificationVersion, @RetrievedCount, @PreScreenRejectedCount);
                """, verification, transaction, cancellationToken: cancellationToken));

            if (verification.Factors.Count > 0)
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO POLOXI.Legal_DecisionEvidenceVerificationFactor
                        (DecisionEvidenceVerificationFactorId, DecisionEvidenceVerificationId, FactorCode, StateCode,
                         ReasonCode, Reason, VerifiedValue, SourceRef, SupportingPassage, VerificationMethod,
                         SupportedComponentsJson, UnsupportedComponentsJson, EvaluatedDateUtc, TenantId, CreatedByUserId,
                         PassageRef, VerifierId, VerifierVersion)
                    VALUES
                        (@DecisionEvidenceVerificationFactorId, @DecisionEvidenceVerificationId, @FactorCode, @StateCode,
                         @ReasonCode, @Reason, @VerifiedValue, @SourceRef, @SupportingPassage, @VerificationMethod,
                         @SupportedComponentsJson, @UnsupportedComponentsJson, @EvaluatedDateUtc, @TenantId, @ActorUserId,
                         @PassageRef, @VerifierId, @VerifierVersion);
                    """, verification.Factors.Select(f => new
                    {
                        f.DecisionEvidenceVerificationFactorId,
                        verification.DecisionEvidenceVerificationId,
                        f.FactorCode,
                        f.StateCode,
                        f.ReasonCode,
                        f.Reason,
                        f.VerifiedValue,
                        f.SourceRef,
                        f.SupportingPassage,
                        f.VerificationMethod,
                        f.SupportedComponentsJson,
                        f.UnsupportedComponentsJson,
                        f.EvaluatedDateUtc,
                        f.PassageRef,
                        f.VerifierId,
                        f.VerifierVersion,
                        verification.TenantId,
                        verification.ActorUserId
                    }), transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyCollection<DecisionEvidenceVerificationPersistence>> GetEvidenceVerificationsAsync(
        Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var runs = (await connection.QueryAsync<DecisionEvidenceVerificationRow>(new CommandDefinition("""
            SELECT DecisionEvidenceVerificationId, DecisionEvidenceId, DecisionSessionId, DecisionBranchId,
                   SourceTypeCode, ProfileCode, DispositionCode, IsVerified, IsDecisionAuthorized,
                   BlockingReasonsJson, MatterId, TenantId, CreatedByUserId AS ActorUserId, EvaluatedDateUtc,
                   MechanicalVerificationCount, SemanticVerificationCount, PoloxiDeepeningCount,
                   CacheHitCount, InputTokenCount, OutputTokenCount, LatencyMilliseconds,
                   SourceSnapshotId, ProfileVersion, VerificationVersion
                   , RetrievedCount, PreScreenRejectedCount
            FROM POLOXI.Legal_DecisionEvidenceVerification
            WHERE TenantId = @TenantId AND DecisionSessionId = @DecisionSessionId AND IsDeleted = 0;
            """, new { TenantId = tenantId, DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken))).ToArray();
        if (runs.Length == 0)
            return [];

        var factors = (await connection.QueryAsync<DecisionEvidenceVerificationFactorRow>(new CommandDefinition("""
            SELECT f.DecisionEvidenceVerificationFactorId, f.DecisionEvidenceVerificationId, f.FactorCode,
                   f.StateCode, f.ReasonCode, f.Reason, f.VerifiedValue, f.SourceRef, f.SupportingPassage,
                    f.VerificationMethod, f.SupportedComponentsJson, f.UnsupportedComponentsJson, f.EvaluatedDateUtc,
                    f.PassageRef, f.VerifierId, f.VerifierVersion
            FROM POLOXI.Legal_DecisionEvidenceVerificationFactor f
            INNER JOIN POLOXI.Legal_DecisionEvidenceVerification v
                ON v.DecisionEvidenceVerificationId = f.DecisionEvidenceVerificationId
            WHERE v.TenantId = @TenantId AND v.DecisionSessionId = @DecisionSessionId
              AND v.IsDeleted = 0 AND f.IsDeleted = 0;
            """, new { TenantId = tenantId, DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken))).ToArray();
        var byRun = factors.GroupBy(f => f.DecisionEvidenceVerificationId).ToDictionary(g => g.Key, g => g.ToArray());
        return runs.Select(r => new DecisionEvidenceVerificationPersistence(
            r.DecisionEvidenceVerificationId, r.DecisionEvidenceId, r.DecisionSessionId, r.DecisionBranchId,
            r.SourceTypeCode, r.ProfileCode, r.DispositionCode, r.IsVerified, r.IsDecisionAuthorized,
            r.BlockingReasonsJson, r.MatterId, r.TenantId, r.ActorUserId, r.EvaluatedDateUtc,
            byRun.GetValueOrDefault(r.DecisionEvidenceVerificationId, []).Select(f =>
                new DecisionEvidenceVerificationFactorPersistence(
                    f.DecisionEvidenceVerificationFactorId, f.FactorCode, f.StateCode, f.ReasonCode,
                    f.Reason, f.VerifiedValue, f.SourceRef, f.SupportingPassage, f.VerificationMethod,
                    f.SupportedComponentsJson, f.UnsupportedComponentsJson, f.EvaluatedDateUtc)
                {
                    PassageRef = f.PassageRef,
                    VerifierId = f.VerifierId,
                    VerifierVersion = f.VerifierVersion,
                }).ToArray())
            {
                SourceSnapshotId = r.SourceSnapshotId,
                ProfileVersion = r.ProfileVersion,
                VerificationVersion = r.VerificationVersion,
                RetrievedCount = r.RetrievedCount,
                PreScreenRejectedCount = r.PreScreenRejectedCount,
                MechanicalVerificationCount = r.MechanicalVerificationCount,
                SemanticVerificationCount = r.SemanticVerificationCount,
                PoloxiDeepeningCount = r.PoloxiDeepeningCount,
                CacheHitCount = r.CacheHitCount,
                InputTokenCount = r.InputTokenCount,
                OutputTokenCount = r.OutputTokenCount,
                LatencyMilliseconds = r.LatencyMilliseconds,
            }).ToArray();
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
            B("Decision.V2.UseDependencyGraph.Default", true),
            D("Decision.V2.Materiality.Threshold", 0.50),
            D("Decision.V2.Readiness.MinAuthorityVerified", 1.00),
            D("Decision.V2.Readiness.LosingSideMargin", 0.05),
            I("Decision.V2.Propagation.MaxDepth", 6),
            I("Decision.V2.Readiness.MaxHighImpactFrontier", 0),
            B("Decision.V2.ApplyVerifiedSignalsToRanking", false));
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

    public async Task<IReadOnlyCollection<DecisionPromptConfigurationDto>> GetPromptConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DecisionPromptConfigurationDto>(new CommandDefinition(
            """
            SELECT PromptCode, StageCode, SystemPrompt, UserPromptTemplate, OutputSchemaJson,
                   IsActive, CreatedDateUtc, ModifiedDateUtc
            FROM POLOXI.Legal_DecisionPrompt
            WHERE IsDeleted = 0
            ORDER BY StageCode, PromptCode;
            """,
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task SavePromptConfigurationAsync(
        Guid actorUserId,
        SaveDecisionPromptConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionPrompt
            SET StageCode = @StageCode,
                SystemPrompt = @SystemPrompt,
                UserPromptTemplate = @UserPromptTemplate,
                OutputSchemaJson = @OutputSchemaJson,
                IsActive = @IsActive,
                ModifiedDateUtc = SYSUTCDATETIME(),
                ModifiedByUserId = @ActorUserId
            WHERE PromptCode = @PromptCode AND IsDeleted = 0;
            """,
            new
            {
                PromptCode = request.PromptCode.Trim(),
                StageCode = request.StageCode.Trim(),
                SystemPrompt = request.SystemPrompt.Trim(),
                UserPromptTemplate = request.UserPromptTemplate.Trim(),
                OutputSchemaJson = string.IsNullOrWhiteSpace(request.OutputSchemaJson) ? null : request.OutputSchemaJson.Trim(),
                request.IsActive,
                ActorUserId = actorUserId,
            },
            cancellationToken: cancellationToken));
        if (affected == 0)
            throw new InvalidOperationException($"Decision prompt '{request.PromptCode}' was not found.");
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
                 ResearchStatusCode, ResearchFailureDetail,
                 TenantId, CreatedByUserId)
            VALUES
                (@DecisionSessionId, @QueryText, @ContextCode, @ModelCode, @UsePoloxiEngine, @StatusCode, @TerminalStateCode,
                 @TerminationReason, @WinnerCandidateId, @ContractCompleteness, @CandidateEntropy, @DecisionMargin, @DepthReached,
                 @LlmCallCount, @DurationMs, @FinalAnswer, @ClarificationQuestion, @ClarificationTarget, @CorrelationId,
                 @MatterId, @NextBestActionText, @NextBestActionImpactCode, @NextBestActionRationale,
                 @ResearchStatusCode, @ResearchFailureDetail,
                 @TenantId, @ActorUserId);
            """,
            new
            {
                session.DecisionSessionId, session.QueryText, session.ContextCode, session.ModelCode, session.UsePoloxiEngine,
                session.StatusCode, session.TerminalStateCode, session.TerminationReason, session.WinnerCandidateId,
                session.ContractCompleteness, session.CandidateEntropy, session.DecisionMargin, session.DepthReached,
                session.LlmCallCount, session.DurationMs, session.FinalAnswer, session.ClarificationQuestion,
                session.ClarificationTarget, session.CorrelationId, session.MatterId, session.NextBestActionText,
                session.NextBestActionImpactCode, session.NextBestActionRationale,
                session.ResearchStatusCode, session.ResearchFailureDetail, session.TenantId, session.ActorUserId
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
                     IsOnFrontier, StopReason, SortOrder, GenerationOriginCode, DecisionDomainConceptId, DomainConceptCode,
                     GuardrailMatchScore, GuardrailActionCode, GuardrailVersion, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionBranchId, @DecisionSessionId, @ParentDecisionBranchId, @LevelNumber, @BranchCode, @DisplayName, @Interpretation,
                     @BranchStateCode, @InformationValue, @DecisionRelevance, @FlipPotential, @EvidenceAvailability, @AdvScore, @Cost,
                     @IsOnFrontier, @StopReason, @SortOrder, @GenerationOriginCode, @DecisionDomainConceptId, @DomainConceptCode,
                     @GuardrailMatchScore, @GuardrailActionCode, @GuardrailVersion, @TenantId, @ActorUserId);
                """,
                session.Branches.Select(b => new
                {
                    b.DecisionBranchId, session.DecisionSessionId, b.ParentDecisionBranchId, b.LevelNumber, b.BranchCode, b.DisplayName,
                    b.Interpretation, b.BranchStateCode, b.InformationValue, b.DecisionRelevance, b.FlipPotential, b.EvidenceAvailability,
                    b.AdvScore, b.Cost, b.IsOnFrontier, b.StopReason, b.SortOrder, b.GenerationOriginCode, b.DecisionDomainConceptId,
                    b.DomainConceptCode, b.GuardrailMatchScore, b.GuardrailActionCode, b.GuardrailVersion,
                    session.TenantId, session.ActorUserId
                }),
                transaction, cancellationToken: cancellationToken));

        if (session.Evidence.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionEvidence
                    (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle, Snippet, IdentityFactor,
                     CitationFactor, HoldingFactor, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, LifecycleState,
                     SupportedObjective, SupportingPassage, TenantId, CreatedByUserId)
                VALUES
                    (@DecisionEvidenceId, @DecisionSessionId, @DecisionBranchId, @SourceRef, @SourceTitle, @Snippet, @IdentityFactor,
                     @CitationFactor, @HoldingFactor, @WeightFactor, @PropositionFit, @VerificationValue, @VerificationStatus, @LifecycleState,
                     @SupportedObjective, @SupportingPassage, @TenantId, @ActorUserId);
                """,
                session.Evidence.Select(e => new
                {
                    e.DecisionEvidenceId, session.DecisionSessionId, e.DecisionBranchId, e.SourceRef, e.SourceTitle, e.Snippet,
                    e.IdentityFactor, e.CitationFactor, e.HoldingFactor, e.WeightFactor, e.PropositionFit, e.VerificationValue,
                    e.VerificationStatus, e.LifecycleState, e.SupportedObjective, e.SupportingPassage, session.TenantId, session.ActorUserId
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

    public async Task AppendSessionEventsAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionEventPersistence> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
            return;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Continue the sequence from the last persisted event for this session so the B3 solver's
        // post-persistence stages sort after the original run in the timeline.
        var baseSequence = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT MAX(SequenceNumber) FROM POLOXI.Legal_DecisionEvent WHERE DecisionSessionId = @DecisionSessionId;",
            new { DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken)) ?? -1;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionEvent
                (DecisionEventId, DecisionSessionId, ParentDecisionEventId, SequenceNumber, EventType, StageCode, PayloadJson, ProvenanceJson, TenantId, CreatedByUserId)
            VALUES
                (@DecisionEventId, @DecisionSessionId, @ParentDecisionEventId, @SequenceNumber, @EventType, @StageCode, @PayloadJson, @ProvenanceJson, @TenantId, @ActorUserId);
            """,
            events.Select((ev, i) => new
            {
                DecisionEventId = ev.DecisionEventId,
                DecisionSessionId = decisionSessionId,
                ev.ParentDecisionEventId,
                SequenceNumber = baseSequence + 1 + i,
                ev.EventType,
                ev.StageCode,
                ev.PayloadJson,
                ev.ProvenanceJson,
                TenantId = tenantId,
                ActorUserId = userId
            }),
            cancellationToken: cancellationToken));
    }

    public async Task<DecisionSessionPersistence?> GetSessionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var session = await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
            """
            SELECT DecisionSessionId, TenantId, CreatedByUserId AS ActorUserId, QueryText, ContextCode, ModelCode, UsePoloxiEngine,
                   StatusCode, TerminalStateCode, TerminationReason, WinnerCandidateId, ContractCompleteness, CandidateEntropy,
                   DecisionMargin, DepthReached, LlmCallCount, DurationMs, FinalAnswer, ClarificationQuestion, ClarificationTarget, CorrelationId,
                   MatterId, NextBestActionText, NextBestActionImpactCode, NextBestActionRationale,
                   ResearchStatusCode, ResearchFailureDetail
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
                   InformationValue, DecisionRelevance, FlipPotential, EvidenceAvailability, AdvScore, Cost, IsOnFrontier, StopReason, SortOrder,
                   COALESCE(GenerationOriginCode, N'DYNAMIC_LLM') AS GenerationOriginCode, DecisionDomainConceptId, DomainConceptCode,
                   GuardrailMatchScore, GuardrailActionCode, GuardrailVersion
            FROM POLOXI.Legal_DecisionBranch WHERE IsDeleted = 0 AND DecisionSessionId = @DecisionSessionId ORDER BY LevelNumber, SortOrder;
            """,
            new { DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken))).ToArray();

        var evidenceRows = await connection.QueryAsync<DecisionEvidenceRow>(new CommandDefinition(
            """
            SELECT DecisionEvidenceId, DecisionBranchId, SourceRef, SourceTitle, Snippet, IdentityFactor, CitationFactor,
                   HoldingFactor, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, LifecycleState, SupportedObjective, SupportingPassage
            FROM POLOXI.Legal_DecisionEvidence WHERE IsDeleted = 0 AND DecisionSessionId = @DecisionSessionId;
            """,
            new { DecisionSessionId = decisionSessionId }, cancellationToken: cancellationToken));
        var evidence = evidenceRows.Select(Map).ToArray();

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
            NextBestActionRationale = session.NextBestActionRationale,
            ResearchStatusCode = session.ResearchStatusCode,
            ResearchFailureDetail = session.ResearchFailureDetail
        };
    }

    public async Task PersistResearchEvidenceAsync(
        Guid tenantId, Guid userId, Guid decisionSessionId,
        IReadOnlyCollection<DecisionEvidencePersistence> evidence,
        CancellationToken cancellationToken = default)
    {
        if (evidence.Count == 0)
            return;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionEvidence
                (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle, Snippet, IdentityFactor,
                 CitationFactor, HoldingFactor, WeightFactor, PropositionFit, VerificationValue, VerificationStatus, LifecycleState,
                 SupportedObjective, SupportingPassage, TenantId, CreatedByUserId)
            VALUES
                (@DecisionEvidenceId, @DecisionSessionId, @DecisionBranchId, @SourceRef, @SourceTitle, @Snippet, @IdentityFactor,
                 @CitationFactor, @HoldingFactor, @WeightFactor, @PropositionFit, @VerificationValue, @VerificationStatus, @LifecycleState,
                 @SupportedObjective, @SupportingPassage, @TenantId, @ActorUserId);
            """,
            evidence.Select(e => new
            {
                e.DecisionEvidenceId,
                DecisionSessionId = decisionSessionId,
                e.DecisionBranchId,
                e.SourceRef,
                e.SourceTitle,
                e.Snippet,
                e.IdentityFactor,
                e.CitationFactor,
                e.HoldingFactor,
                e.WeightFactor,
                e.PropositionFit,
                e.VerificationValue,
                e.VerificationStatus,
                e.LifecycleState,
                e.SupportedObjective,
                e.SupportingPassage,
                TenantId = tenantId,
                ActorUserId = userId
            }), cancellationToken: cancellationToken));
    }

    public async Task UpdateResearchEvidenceAsync(
        Guid tenantId, Guid userId, Guid decisionSessionId,
        IReadOnlyCollection<DecisionEvidencePersistence> evidence,
        CancellationToken cancellationToken = default)
    {
        if (evidence.Count == 0)
            return;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE POLOXI.Legal_DecisionEvidence
               SET IdentityFactor = @IdentityFactor,
                   CitationFactor = @CitationFactor,
                   HoldingFactor = @HoldingFactor,
                   WeightFactor = @WeightFactor,
                   PropositionFit = @PropositionFit,
                   VerificationValue = @VerificationValue,
                   VerificationStatus = @VerificationStatus,
                    LifecycleState = @LifecycleState,
                   SupportedObjective = @SupportedObjective,
                   SupportingPassage = @SupportingPassage,
                   ModifiedDateUtc = SYSUTCDATETIME(),
                   ModifiedByUserId = @ActorUserId
             WHERE DecisionEvidenceId = @DecisionEvidenceId
               AND DecisionSessionId = @DecisionSessionId
               AND TenantId = @TenantId
               AND IsDeleted = 0;
            """, evidence.Select(e => new
            {
                e.DecisionEvidenceId,
                DecisionSessionId = decisionSessionId,
                TenantId = tenantId,
                ActorUserId = userId,
                e.IdentityFactor,
                e.CitationFactor,
                e.HoldingFactor,
                e.WeightFactor,
                e.PropositionFit,
                e.VerificationValue,
                e.VerificationStatus,
                e.LifecycleState,
                e.SupportedObjective,
                e.SupportingPassage
            }), cancellationToken: cancellationToken));
    }

    // ── Matter aggregate + cockpit support ──────────────────────────────────────────────────────
    public async Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterRow>(new CommandDefinition(
            """
            SELECT
                m.DecisionMatterId, m.Title, m.MatterTypeCode, m.Jurisdiction, m.Posture, m.Description, m.StatusCode,
                m.PracticeAreaCode, m.ClaimTypeCode, m.DomainPackCode,
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
                m.PracticeAreaCode, m.ClaimTypeCode, m.DomainPackCode,
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
                 PracticeAreaCode, ClaimTypeCode, DomainPackCode,
                 Subtype, CourtSystem, State, CourtLevel, County, GoverningLaw, MovingParty, RespondingParty, MotionTarget, RequestedDisposition)
            VALUES
                (@DecisionMatterId, @Title, @MatterTypeCode, @Jurisdiction, @Posture, @Description, N'OPEN', @TenantId, @UserId,
                 @PracticeAreaCode, @ClaimTypeCode, @DomainPackCode,
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
                request.PracticeAreaCode,
                request.ClaimTypeCode,
                request.DomainPackCode,
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

    // ── Domain Pack (practice-area domain semantics) ─────────────────────────────────────────────
    // Loads a database-backed Domain Pack (global default rows use TenantId NULL; tenant rows override)
    // with its dimensions, evidence types, verification profiles, and matter-type taxonomy. Advisory
    // configuration only — POLOXI Core decision reasoning is unchanged.
    public async Task<DecisionDomainPackDto?> GetDomainPackAsync(Guid tenantId, string packCode, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var pack = await connection.QuerySingleOrDefaultAsync<DomainPackRow>(new CommandDefinition(
            """
            SELECT TOP 1 DecisionDomainPackId, PackCode, PracticeAreaCode, Name, Description
            FROM POLOXI.Legal_DecisionDomainPack
            WHERE IsDeleted = 0 AND IsActive = 1 AND PackCode = @PackCode
              AND (TenantId = @TenantId OR TenantId IS NULL)
            ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END, SortOrder;
            """,
            new { TenantId = tenantId, PackCode = packCode },
            cancellationToken: cancellationToken));
        if (pack is null)
            return null;

        var dimensions = (await connection.QueryAsync<DecisionDomainPackDimensionDto>(new CommandDefinition(
            """
            SELECT DimensionCode, Name, Description FROM POLOXI.Legal_DecisionDomainPackDimension
            WHERE IsDeleted = 0 AND IsActive = 1 AND DecisionDomainPackId = @PackId ORDER BY SortOrder, Name;
            """, new { PackId = pack.DecisionDomainPackId }, cancellationToken: cancellationToken))).ToArray();

        var evidenceTypes = (await connection.QueryAsync<DecisionDomainPackEvidenceTypeDto>(new CommandDefinition(
            """
            SELECT EvidenceTypeCode, Name, DimensionCode, Description FROM POLOXI.Legal_DecisionDomainPackEvidenceType
            WHERE IsDeleted = 0 AND IsActive = 1 AND DecisionDomainPackId = @PackId ORDER BY SortOrder, Name;
            """, new { PackId = pack.DecisionDomainPackId }, cancellationToken: cancellationToken))).ToArray();

        var profiles = (await connection.QueryAsync<DecisionDomainPackVerificationProfileDto>(new CommandDefinition(
            """
            SELECT ProfileCode, Name, EvidenceTypeCode, Description FROM POLOXI.Legal_DecisionDomainPackVerificationProfile
            WHERE IsDeleted = 0 AND IsActive = 1 AND DecisionDomainPackId = @PackId ORDER BY SortOrder, Name;
            """, new { PackId = pack.DecisionDomainPackId }, cancellationToken: cancellationToken))).ToArray();

        var matterTypes = (await connection.QueryAsync<DecisionDomainPackMatterTypeDto>(new CommandDefinition(
            """
            SELECT MatterTypeCode, Name, Description FROM POLOXI.Legal_DecisionDomainPackMatterType
            WHERE IsDeleted = 0 AND IsActive = 1 AND DecisionDomainPackId = @PackId ORDER BY SortOrder, Name;
            """, new { PackId = pack.DecisionDomainPackId }, cancellationToken: cancellationToken))).ToArray();

        var concepts = (await connection.QueryAsync<DecisionDomainConceptDto>(new CommandDefinition(
            """
            WITH RankedConcept AS
            (
                SELECT DecisionDomainConceptId, ConceptCode, DimensionCode, Name, Description, ConceptKindCode,
                       SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode,
                       IsRequiredCoverage, IsFallbackEligible, SortOrder, VersionNumber,
                       ROW_NUMBER() OVER
                       (
                           PARTITION BY ConceptCode
                           ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END, VersionNumber DESC, SortOrder
                       ) AS ScopeRank
                FROM POLOXI.Legal_DecisionDomainConcept
                WHERE IsDeleted = 0 AND IsActive = 1 AND DecisionDomainPackId = @PackId
                  AND (TenantId = @TenantId OR TenantId IS NULL)
            )
            SELECT DecisionDomainConceptId, ConceptCode, DimensionCode, Name, Description, ConceptKindCode,
                   SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode,
                   IsRequiredCoverage, IsFallbackEligible, SortOrder, VersionNumber
            FROM RankedConcept WHERE ScopeRank = 1 ORDER BY SortOrder, Name;
            """, new { PackId = pack.DecisionDomainPackId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToArray();

        var conceptRelations = (await connection.QueryAsync<DecisionDomainConceptRelationDto>(new CommandDefinition(
            """
            WITH RankedRelation AS
            (
                SELECT DecisionDomainConceptRelationId, SourceConceptCode, TargetConceptCode, RelationTypeCode,
                       ConstraintCode, Description, JurisdictionCode, MatterTypeCode, IsHardConstraint, SortOrder,
                       ROW_NUMBER() OVER
                       (
                           PARTITION BY SourceConceptCode, TargetConceptCode, RelationTypeCode
                           ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END, SortOrder
                       ) AS ScopeRank
                FROM POLOXI.Legal_DecisionDomainConceptRelation
                WHERE IsDeleted = 0 AND IsActive = 1 AND DecisionDomainPackId = @PackId
                  AND (TenantId = @TenantId OR TenantId IS NULL)
            )
            SELECT DecisionDomainConceptRelationId, SourceConceptCode, TargetConceptCode, RelationTypeCode,
                   ConstraintCode, Description, JurisdictionCode, MatterTypeCode, IsHardConstraint, SortOrder
            FROM RankedRelation WHERE ScopeRank = 1 ORDER BY SortOrder;
            """, new { PackId = pack.DecisionDomainPackId, TenantId = tenantId }, cancellationToken: cancellationToken))).ToArray();

        return new DecisionDomainPackDto(
            pack.DecisionDomainPackId, pack.PackCode, pack.PracticeAreaCode, pack.Name, pack.Description,
            dimensions, evidenceTypes, profiles, matterTypes)
        {
            Concepts = concepts,
            ConceptRelations = conceptRelations,
        };
    }

    private sealed record DomainPackRow(Guid DecisionDomainPackId, string PackCode, string PracticeAreaCode, string Name, string? Description);

    // ── Personal Injury (Domain Pack: PERSONAL_INJURY) profile + child aggregates + options ────────
    // Selectable values are DB-backed (POLOXI.Legal_DecisionMatterOption FieldCode buckets and the
    // dedicated PI config tables). No PI options are hardcoded in code.
    public async Task<PersonalInjuryOptionsDto> GetPersonalInjuryOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<(string FieldCode, string Value)>(new CommandDefinition(
            """
            SELECT FieldCode, Value
            FROM POLOXI.Legal_DecisionMatterOption
            WHERE IsDeleted = 0 AND IsActive = 1 AND FieldCode LIKE N'PI\_%' ESCAPE N'\'
            ORDER BY FieldCode, SortOrder, Value;
            """,
            cancellationToken: cancellationToken))).ToArray();

        string[] For(string field) => rows.Where(r => string.Equals(r.FieldCode, field, StringComparison.OrdinalIgnoreCase)).Select(r => r.Value).ToArray();

        return new PersonalInjuryOptionsDto
        {
            IncidentTypes = For("PI_INCIDENT_TYPE"),
            BodyAreas = For("PI_BODY_AREA"),
            InjurySeverities = For("PI_INJURY_SEVERITY"),
            CoverageTypes = For("PI_COVERAGE_TYPE"),
            CoverageStatuses = For("PI_COVERAGE_STATUS"),
            TreatmentStatuses = For("PI_TREATMENT_STATUS"),
            DamageTypes = For("PI_DAMAGE_TYPE"),
            LienTypes = For("PI_LIEN_TYPE"),
            LienStatuses = For("PI_LIEN_STATUS"),
            DemandStatuses = For("PI_DEMAND_STATUS"),
            SettlementStatuses = For("PI_SETTLEMENT_STATUS"),
            LitigationStatuses = For("PI_LITIGATION_STATUS"),
            MatterStages = For("PI_MATTER_STAGE"),
            DeadlineRuleSources = For("PI_DEADLINE_RULE_SOURCE"),
            DeadlineVerificationStates = For("PI_DEADLINE_VERIFICATION"),
            VehicleRoles = For("PI_VEHICLE_ROLE"),
            DraftSourceTypes = For("PI_DRAFT_SOURCE_TYPE"),
            DraftVerificationStates = For("PI_DRAFT_VERIFICATION_STATE")
        };
    }

    public async Task<IReadOnlyCollection<PersonalInjuryDecisionTypeDto>> GetPersonalInjuryDecisionTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PersonalInjuryDecisionTypeDto>(new CommandDefinition(
            """
            SELECT DecisionTypeCode, Name, Description
            FROM POLOXI.Legal_DecisionPIDecisionType
            WHERE IsDeleted = 0 AND IsActive = 1 AND (TenantId = @TenantId OR TenantId IS NULL)
            ORDER BY SortOrder, Name;
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<PersonalInjuryStageDecisionDto>> GetPersonalInjuryStageDecisionMapAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PersonalInjuryStageDecisionDto>(new CommandDefinition(
            """
            SELECT StageCode, DefaultDecisionTypeCode
            FROM POLOXI.Legal_DecisionPIStageDecisionMap
            WHERE IsDeleted = 0 AND IsActive = 1 AND (TenantId = @TenantId OR TenantId IS NULL)
            ORDER BY SortOrder, StageCode;
            """,
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<PersonalInjuryProfileDto?> GetPersonalInjuryProfileAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT TOP 1 DecisionMatterId, IncidentTypeCode, IncidentDate, IncidentTime, IncidentLocation, IncidentCity,
                   IncidentCounty, IncidentState, IncidentSummary, LiabilitySummary, InjurySummary, TreatmentSummary,
                   DamagesSummary, CurrentStageCode, LitigationStatusCode, DemandStatusCode, SettlementStatusCode
            FROM POLOXI.Legal_DecisionPIMatterProfile
            WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId AND (TenantId = @TenantId OR TenantId IS NULL);

            SELECT DecisionPIInsurancePolicyId AS Id, Carrier, Insured, Adjuster, ClaimNumber, PolicyNumber, CoverageTypeCode,
                   BodilyInjuryLimitPerPerson, BodilyInjuryLimitPerOccur, CoverageStatusCode, LimitsSource, LimitsVerified, Notes
            FROM POLOXI.Legal_DecisionPIInsurancePolicy WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPIInjuryId AS Id, BodyAreaCode, InitialSymptoms, Diagnosis, IsPreexisting, ClaimedPermanency,
                   SurgeryRecommended, SurgeryPerformed, SeverityCode, Notes
            FROM POLOXI.Legal_DecisionPIInjury WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPITreatmentId AS Id, Provider, Specialty, FirstTreatmentDate, LastTreatmentDate, StatusCode,
                   VisitCount, RecordRequestStatus, BillRequestStatus, TreatmentGapDays, Notes
            FROM POLOXI.Legal_DecisionPITreatment WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPIMedicalBillId AS Id, Provider, AmountBilled, Adjustments, AmountPaid, OutstandingBalance, Payer, LienStatusCode, Notes
            FROM POLOXI.Legal_DecisionPIMedicalBill WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPIDamageId AS Id, DamageTypeCode, Description, ClaimedAmount, IsEconomic, Notes
            FROM POLOXI.Legal_DecisionPIDamage WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPILienId AS Id, Lienholder, LienTypeCode, AssertedAmount, VerifiedAmount, NegotiatedAmount, FinalPayoffAmount, StatusCode, Notes
            FROM POLOXI.Legal_DecisionPILien WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPIDemandId AS Id, DemandDate, DemandAmount, Recipient, ResponseDeadline, StatusCode, Notes
            FROM POLOXI.Legal_DecisionPIDemand WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPINegotiationId AS Id, EventDate, Amount, Source, IsOffer, Conditions, ExpirationDate, Notes
            FROM POLOXI.Legal_DecisionPINegotiation WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPISettlementId AS Id, GrossRecovery, Fees, Costs, Liens, NetToClient, StatusCode, SettlementDate, Notes
            FROM POLOXI.Legal_DecisionPISettlement WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPIDeadlineId AS Id, DeadlineTypeCode, CandidateDate, RuleSourceCode, VerificationState, Notes
            FROM POLOXI.Legal_DecisionPIDeadline WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPIIncidentVehicleId AS Id, RoleCode, Description, Owner, Driver, ImpactType, Citation, Notes
            FROM POLOXI.Legal_DecisionPIIncidentVehicle WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;

            SELECT DecisionPIWitnessId AS Id, Name, ContactInfo, StatementSummary, SupportsClient, Notes
            FROM POLOXI.Legal_DecisionPIWitness WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId ORDER BY CreatedDateUtc;
            """,
            new { TenantId = tenantId, MatterId = decisionMatterId },
            cancellationToken: cancellationToken));

        var profile = await multi.ReadSingleOrDefaultAsync<PersonalInjuryProfileRow>();
        var insurance = (await multi.ReadAsync<PersonalInjuryInsurancePolicyDto>()).ToArray();
        var injuries = (await multi.ReadAsync<PersonalInjuryInjuryDto>()).ToArray();
        var treatments = (await multi.ReadAsync<PersonalInjuryTreatmentDto>()).ToArray();
        var bills = (await multi.ReadAsync<PersonalInjuryMedicalBillDto>()).ToArray();
        var damages = (await multi.ReadAsync<PersonalInjuryDamageDto>()).ToArray();
        var liens = (await multi.ReadAsync<PersonalInjuryLienDto>()).ToArray();
        var demands = (await multi.ReadAsync<PersonalInjuryDemandDto>()).ToArray();
        var negotiations = (await multi.ReadAsync<PersonalInjuryNegotiationDto>()).ToArray();
        var settlements = (await multi.ReadAsync<PersonalInjurySettlementDto>()).ToArray();
        var deadlines = (await multi.ReadAsync<PersonalInjuryDeadlineDto>()).ToArray();
        var vehicles = (await multi.ReadAsync<PersonalInjuryIncidentVehicleDto>()).ToArray();
        var witnesses = (await multi.ReadAsync<PersonalInjuryWitnessDto>()).ToArray();

        if (profile is null &&
            insurance.Length == 0 && injuries.Length == 0 && treatments.Length == 0 && bills.Length == 0 &&
            damages.Length == 0 && liens.Length == 0 && demands.Length == 0 && negotiations.Length == 0 &&
            settlements.Length == 0 && deadlines.Length == 0 && vehicles.Length == 0 && witnesses.Length == 0)
            return null;

        return new PersonalInjuryProfileDto(decisionMatterId)
        {
            IncidentTypeCode = profile?.IncidentTypeCode,
            IncidentDate = profile?.IncidentDate is { } dt ? DateOnly.FromDateTime(dt) : null,
            IncidentTime = profile?.IncidentTime is { } ts ? TimeOnly.FromTimeSpan(ts) : null,
            IncidentLocation = profile?.IncidentLocation,
            IncidentCity = profile?.IncidentCity,
            IncidentCounty = profile?.IncidentCounty,
            IncidentState = profile?.IncidentState,
            IncidentSummary = profile?.IncidentSummary,
            LiabilitySummary = profile?.LiabilitySummary,
            InjurySummary = profile?.InjurySummary,
            TreatmentSummary = profile?.TreatmentSummary,
            DamagesSummary = profile?.DamagesSummary,
            CurrentStageCode = profile?.CurrentStageCode,
            LitigationStatusCode = profile?.LitigationStatusCode,
            DemandStatusCode = profile?.DemandStatusCode,
            SettlementStatusCode = profile?.SettlementStatusCode,
            InsurancePolicies = insurance,
            Injuries = injuries,
            Treatments = treatments,
            MedicalBills = bills,
            Damages = damages,
            Liens = liens,
            Demands = demands,
            Negotiations = negotiations,
            Settlements = settlements,
            Deadlines = deadlines,
            IncidentVehicles = vehicles,
            Witnesses = witnesses
        };
    }

    // Full replace-on-save of the PI profile + child aggregates (soft-delete existing rows, re-insert).
    public async Task SavePersonalInjuryProfileAsync(Guid tenantId, Guid userId, Guid decisionMatterId, PersonalInjuryProfileSaveRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        var actor = userId == Guid.Empty ? (Guid?)null : userId;

        // Upsert the 1:1 profile.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionPIMatterProfile
            SET IncidentTypeCode = @IncidentTypeCode, IncidentDate = @IncidentDate, IncidentTime = @IncidentTime,
                IncidentLocation = @IncidentLocation, IncidentCity = @IncidentCity, IncidentCounty = @IncidentCounty,
                IncidentState = @IncidentState, IncidentSummary = @IncidentSummary, LiabilitySummary = @LiabilitySummary,
                InjurySummary = @InjurySummary, TreatmentSummary = @TreatmentSummary, DamagesSummary = @DamagesSummary,
                CurrentStageCode = @CurrentStageCode, LitigationStatusCode = @LitigationStatusCode,
                DemandStatusCode = @DemandStatusCode, SettlementStatusCode = @SettlementStatusCode,
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
            WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;

            IF @@ROWCOUNT = 0
            INSERT INTO POLOXI.Legal_DecisionPIMatterProfile
                (DecisionMatterId, IncidentTypeCode, IncidentDate, IncidentTime, IncidentLocation, IncidentCity, IncidentCounty,
                 IncidentState, IncidentSummary, LiabilitySummary, InjurySummary, TreatmentSummary, DamagesSummary,
                 CurrentStageCode, LitigationStatusCode, DemandStatusCode, SettlementStatusCode, TenantId, CreatedByUserId)
            VALUES
                (@MatterId, @IncidentTypeCode, @IncidentDate, @IncidentTime, @IncidentLocation, @IncidentCity, @IncidentCounty,
                 @IncidentState, @IncidentSummary, @LiabilitySummary, @InjurySummary, @TreatmentSummary, @DamagesSummary,
                 @CurrentStageCode, @LitigationStatusCode, @DemandStatusCode, @SettlementStatusCode, @TenantId, @Actor);
            """,
            new
            {
                MatterId = decisionMatterId,
                request.IncidentTypeCode,
                IncidentDate = request.IncidentDate.HasValue ? request.IncidentDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                IncidentTime = request.IncidentTime.HasValue ? request.IncidentTime.Value.ToTimeSpan() : (TimeSpan?)null,
                request.IncidentLocation,
                request.IncidentCity,
                request.IncidentCounty,
                request.IncidentState,
                request.IncidentSummary,
                request.LiabilitySummary,
                request.InjurySummary,
                request.TreatmentSummary,
                request.DamagesSummary,
                request.CurrentStageCode,
                request.LitigationStatusCode,
                request.DemandStatusCode,
                request.SettlementStatusCode,
                TenantId = tenantId,
                Actor = actor
            },
            transaction: tx, cancellationToken: cancellationToken));

        // Soft-delete then re-insert all child aggregates.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionPIInsurancePolicy SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPIInjury          SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPITreatment       SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPIMedicalBill     SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPIDamage          SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPILien            SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPIDemand          SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPINegotiation     SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPISettlement      SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPIDeadline        SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPIIncidentVehicle SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            UPDATE POLOXI.Legal_DecisionPIWitness         SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor WHERE IsDeleted = 0 AND DecisionMatterId = @MatterId;
            """,
            new { MatterId = decisionMatterId, Actor = actor },
            transaction: tx, cancellationToken: cancellationToken));

        foreach (var p in request.InsurancePolicies)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIInsurancePolicy
                    (DecisionMatterId, Carrier, Insured, Adjuster, ClaimNumber, PolicyNumber, CoverageTypeCode,
                     BodilyInjuryLimitPerPerson, BodilyInjuryLimitPerOccur, CoverageStatusCode, LimitsSource, LimitsVerified, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @Carrier, @Insured, @Adjuster, @ClaimNumber, @PolicyNumber, @CoverageTypeCode,
                     @BodilyInjuryLimitPerPerson, @BodilyInjuryLimitPerOccur, @CoverageStatusCode, @LimitsSource, @LimitsVerified, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, p.Carrier, p.Insured, p.Adjuster, p.ClaimNumber, p.PolicyNumber, p.CoverageTypeCode,
                      p.BodilyInjuryLimitPerPerson, p.BodilyInjuryLimitPerOccur, p.CoverageStatusCode, p.LimitsSource, p.LimitsVerified, p.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var i in request.Injuries)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIInjury
                    (DecisionMatterId, BodyAreaCode, InitialSymptoms, Diagnosis, IsPreexisting, ClaimedPermanency, SurgeryRecommended, SurgeryPerformed, SeverityCode, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @BodyAreaCode, @InitialSymptoms, @Diagnosis, @IsPreexisting, @ClaimedPermanency, @SurgeryRecommended, @SurgeryPerformed, @SeverityCode, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, i.BodyAreaCode, i.InitialSymptoms, i.Diagnosis, i.IsPreexisting, i.ClaimedPermanency, i.SurgeryRecommended, i.SurgeryPerformed, i.SeverityCode, i.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var t in request.Treatments)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPITreatment
                    (DecisionMatterId, Provider, Specialty, FirstTreatmentDate, LastTreatmentDate, StatusCode, VisitCount, RecordRequestStatus, BillRequestStatus, TreatmentGapDays, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @Provider, @Specialty, @FirstTreatmentDate, @LastTreatmentDate, @StatusCode, @VisitCount, @RecordRequestStatus, @BillRequestStatus, @TreatmentGapDays, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, t.Provider, t.Specialty,
                      FirstTreatmentDate = t.FirstTreatmentDate.HasValue ? t.FirstTreatmentDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      LastTreatmentDate = t.LastTreatmentDate.HasValue ? t.LastTreatmentDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      t.StatusCode, t.VisitCount, t.RecordRequestStatus, t.BillRequestStatus, t.TreatmentGapDays, t.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var b in request.MedicalBills)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIMedicalBill
                    (DecisionMatterId, Provider, AmountBilled, Adjustments, AmountPaid, OutstandingBalance, Payer, LienStatusCode, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @Provider, @AmountBilled, @Adjustments, @AmountPaid, @OutstandingBalance, @Payer, @LienStatusCode, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, b.Provider, b.AmountBilled, b.Adjustments, b.AmountPaid, b.OutstandingBalance, b.Payer, b.LienStatusCode, b.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var d in request.Damages)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIDamage
                    (DecisionMatterId, DamageTypeCode, Description, ClaimedAmount, IsEconomic, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @DamageTypeCode, @Description, @ClaimedAmount, @IsEconomic, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, d.DamageTypeCode, d.Description, d.ClaimedAmount, d.IsEconomic, d.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var l in request.Liens)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPILien
                    (DecisionMatterId, Lienholder, LienTypeCode, AssertedAmount, VerifiedAmount, NegotiatedAmount, FinalPayoffAmount, StatusCode, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @Lienholder, @LienTypeCode, @AssertedAmount, @VerifiedAmount, @NegotiatedAmount, @FinalPayoffAmount, @StatusCode, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, l.Lienholder, l.LienTypeCode, l.AssertedAmount, l.VerifiedAmount, l.NegotiatedAmount, l.FinalPayoffAmount, l.StatusCode, l.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var d in request.Demands)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIDemand
                    (DecisionMatterId, DemandDate, DemandAmount, Recipient, ResponseDeadline, StatusCode, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @DemandDate, @DemandAmount, @Recipient, @ResponseDeadline, @StatusCode, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId,
                      DemandDate = d.DemandDate.HasValue ? d.DemandDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      d.DemandAmount, d.Recipient,
                      ResponseDeadline = d.ResponseDeadline.HasValue ? d.ResponseDeadline.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      d.StatusCode, d.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var n in request.Negotiations)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPINegotiation
                    (DecisionMatterId, EventDate, Amount, Source, IsOffer, Conditions, ExpirationDate, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @EventDate, @Amount, @Source, @IsOffer, @Conditions, @ExpirationDate, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId,
                      EventDate = n.EventDate.HasValue ? n.EventDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      n.Amount, n.Source, n.IsOffer, n.Conditions,
                      ExpirationDate = n.ExpirationDate.HasValue ? n.ExpirationDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      n.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var s in request.Settlements)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPISettlement
                    (DecisionMatterId, GrossRecovery, Fees, Costs, Liens, NetToClient, StatusCode, SettlementDate, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @GrossRecovery, @Fees, @Costs, @Liens, @NetToClient, @StatusCode, @SettlementDate, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, s.GrossRecovery, s.Fees, s.Costs, s.Liens, s.NetToClient, s.StatusCode,
                      SettlementDate = s.SettlementDate.HasValue ? s.SettlementDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      s.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var dl in request.Deadlines)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIDeadline
                    (DecisionMatterId, DeadlineTypeCode, CandidateDate, RuleSourceCode, VerificationState, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @DeadlineTypeCode, @CandidateDate, @RuleSourceCode, @VerificationState, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, dl.DeadlineTypeCode,
                      CandidateDate = dl.CandidateDate.HasValue ? dl.CandidateDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                      dl.RuleSourceCode, dl.VerificationState, dl.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var v in request.IncidentVehicles)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIIncidentVehicle
                    (DecisionMatterId, RoleCode, Description, Owner, Driver, ImpactType, Citation, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @RoleCode, @Description, @Owner, @Driver, @ImpactType, @Citation, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, v.RoleCode, v.Description, v.Owner, v.Driver, v.ImpactType, v.Citation, v.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        foreach (var w in request.Witnesses)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIWitness
                    (DecisionMatterId, Name, ContactInfo, StatementSummary, SupportsClient, Notes, TenantId, CreatedByUserId)
                VALUES (@MatterId, @Name, @ContactInfo, @StatementSummary, @SupportsClient, @Notes, @TenantId, @Actor);
                """,
                new { MatterId = decisionMatterId, w.Name, w.ContactInfo, w.StatementSummary, w.SupportsClient, w.Notes, TenantId = tenantId, Actor = actor },
                transaction: tx, cancellationToken: cancellationToken));

        tx.Commit();
    }

    // ── Generate-New-Matter draft / provenance persistence (extraction wired later) ──
    public async Task<Guid> CreatePersonalInjuryDraftAsync(Guid tenantId, Guid userId, PersonalInjuryMatterDraftCreateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = connection.BeginTransaction();
        var draftId = Guid.NewGuid();
        var actor = userId == Guid.Empty ? (Guid?)null : userId;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_DecisionPIMatterDraft
                (DecisionPIMatterDraftId, StatusCode, Prompt, SourceDocumentIdsJson, TenantId, CreatedByUserId)
            VALUES (@DraftId, @StatusCode, @Prompt, @SourceDocumentIdsJson, @TenantId, @Actor);
            """,
            new
            {
                DraftId = draftId,
                StatusCode = PersonalInjuryMatterDraftStatusCodes.PendingReview,
                request.Prompt,
                SourceDocumentIdsJson = request.SourceDocumentIds.Count > 0 ? JsonSerializer.Serialize(request.SourceDocumentIds) : null,
                TenantId = tenantId,
                Actor = actor
            },
            transaction: tx, cancellationToken: cancellationToken));

        var order = 0;
        foreach (var f in request.Fields)
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_DecisionPIMatterDraftField
                    (DecisionPIMatterDraftId, FieldCode, ProposedValue, SourceType, SourceDocumentId, SourcePassage,
                     VerificationState, ConflictReason, ConflictsJson, SortOrder, TenantId, CreatedByUserId)
                VALUES (@DraftId, @FieldCode, @ProposedValue, @SourceType, @SourceDocumentId, @SourcePassage,
                     @VerificationState, @ConflictReason, @ConflictsJson, @SortOrder, @TenantId, @Actor);
                """,
                new
                {
                    DraftId = draftId,
                    f.FieldCode,
                    f.ProposedValue,
                    f.SourceType,
                    f.SourceDocumentId,
                    f.SourcePassage,
                    f.VerificationState,
                    f.ConflictReason,
                    ConflictsJson = f.Conflicts.Count > 0 ? JsonSerializer.Serialize(f.Conflicts) : null,
                    SortOrder = order++,
                    TenantId = tenantId,
                    Actor = actor
                },
                transaction: tx, cancellationToken: cancellationToken));

        tx.Commit();
        return draftId;
    }

    public async Task<PersonalInjuryMatterDraftDto?> GetPersonalInjuryDraftAsync(Guid tenantId, Guid decisionPIMatterDraftId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT TOP 1 DecisionPIMatterDraftId, StatusCode, Prompt, CreatedDateUtc
            FROM POLOXI.Legal_DecisionPIMatterDraft
            WHERE IsDeleted = 0 AND DecisionPIMatterDraftId = @DraftId AND (TenantId = @TenantId OR TenantId IS NULL);

            SELECT FieldCode, ProposedValue, SourceType, SourceDocumentId, SourcePassage, VerificationState, ConflictReason, ConflictsJson
            FROM POLOXI.Legal_DecisionPIMatterDraftField
            WHERE IsDeleted = 0 AND DecisionPIMatterDraftId = @DraftId ORDER BY SortOrder;
            """,
            new { TenantId = tenantId, DraftId = decisionPIMatterDraftId },
            cancellationToken: cancellationToken));

        var draft = await multi.ReadSingleOrDefaultAsync<PersonalInjuryDraftRow>();
        if (draft is null)
            return null;
        var fieldRows = (await multi.ReadAsync<PersonalInjuryDraftFieldRow>()).ToArray();

        var fields = fieldRows.Select(r => new GeneratedMatterFieldDto(
            r.FieldCode, r.ProposedValue,
            r.SourceType ?? PersonalInjuryDraftSourceTypes.Missing,
            r.VerificationState ?? PersonalInjuryDraftVerificationStates.Missing)
        {
            SourceDocumentId = r.SourceDocumentId,
            SourcePassage = r.SourcePassage,
            ConflictReason = r.ConflictReason,
            Conflicts = string.IsNullOrWhiteSpace(r.ConflictsJson)
                ? []
                : JsonSerializer.Deserialize<List<GeneratedMatterFieldCandidateDto>>(r.ConflictsJson) ?? []
        }).ToArray();

        return new PersonalInjuryMatterDraftDto(draft.DecisionPIMatterDraftId, draft.StatusCode, draft.Prompt, draft.CreatedDateUtc)
        {
            Fields = fields
        };
    }

    public async Task<bool> MarkPersonalInjuryDraftConfirmedAsync(Guid tenantId, Guid userId, Guid decisionPIMatterDraftId, Guid confirmedMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionPIMatterDraft
            SET StatusCode = @StatusCode, ConfirmedMatterId = @ConfirmedMatterId,
                ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @Actor
            WHERE IsDeleted = 0 AND DecisionPIMatterDraftId = @DraftId AND (TenantId = @TenantId OR TenantId IS NULL);
            """,
            new
            {
                StatusCode = PersonalInjuryMatterDraftStatusCodes.Confirmed,
                ConfirmedMatterId = confirmedMatterId,
                DraftId = decisionPIMatterDraftId,
                TenantId = tenantId,
                Actor = userId == Guid.Empty ? (Guid?)null : userId
            },
            cancellationToken: cancellationToken));
        return affected > 0;
    }

    private sealed record PersonalInjuryProfileRow(
        Guid DecisionMatterId, string? IncidentTypeCode, DateTime? IncidentDate, TimeSpan? IncidentTime, string? IncidentLocation,
        string? IncidentCity, string? IncidentCounty, string? IncidentState, string? IncidentSummary, string? LiabilitySummary,
        string? InjurySummary, string? TreatmentSummary, string? DamagesSummary, string? CurrentStageCode, string? LitigationStatusCode,
        string? DemandStatusCode, string? SettlementStatusCode);

    private sealed record PersonalInjuryDraftRow(Guid DecisionPIMatterDraftId, string StatusCode, string? Prompt, DateTime CreatedDateUtc);

    private sealed record PersonalInjuryDraftFieldRow(
        string FieldCode, string? ProposedValue, string? SourceType, Guid? SourceDocumentId, string? SourcePassage,
        string? VerificationState, string? ConflictReason, string? ConflictsJson);

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
                PracticeAreaCode = @PracticeAreaCode,
                ClaimTypeCode = @ClaimTypeCode,
                DomainPackCode = @DomainPackCode,
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
                request.PracticeAreaCode,
                request.ClaimTypeCode,
                request.DomainPackCode,
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
            SELECT o.Value FROM POLOXI.Legal_DecisionMatterOption o
                WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'PRACTICE_AREA'
                AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                GROUP BY o.Value, o.SortOrder ORDER BY o.SortOrder, o.Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'PI_MATTER_TYPE'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.MatterTypeCode)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND m.PracticeAreaCode = N'PERSONAL_INJURY'
                    AND NULLIF(LTRIM(RTRIM(m.MatterTypeCode)), N'') IS NOT NULL
            ) t GROUP BY Value ORDER BY MIN(Ord), Value;
            SELECT Value FROM (
                SELECT o.Value, o.SortOrder AS Ord FROM POLOXI.Legal_DecisionMatterOption o
                    WHERE o.IsDeleted = 0 AND o.IsActive = 1 AND o.FieldCode = N'PI_CLAIM_TYPE'
                    AND (o.TenantId IS NULL OR o.TenantId = @TenantId)
                UNION
                SELECT LTRIM(RTRIM(m.ClaimTypeCode)), 1000000 FROM POLOXI.Legal_DecisionMatter m
                    WHERE m.IsDeleted = 0 AND m.TenantId = @TenantId AND m.PracticeAreaCode = N'PERSONAL_INJURY'
                    AND NULLIF(LTRIM(RTRIM(m.ClaimTypeCode)), N'') IS NOT NULL
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
        var practiceAreas = (await multi.ReadAsync<string>()).ToArray();
        var piMatterTypes = (await multi.ReadAsync<string>()).ToArray();
        var piClaimTypes = (await multi.ReadAsync<string>()).ToArray();
        return new DecisionMatterFacetsDto(matterTypes, jurisdictions, postures)
        {
            Subtypes = subtypes,
            CourtSystems = courtSystems,
            States = states,
            CourtLevels = courtLevels,
            GoverningLaws = governingLaws,
            PracticeAreas = practiceAreas,
            PiMatterTypes = piMatterTypes,
            PiClaimTypes = piClaimTypes
        };
    }

    private static DecisionMatterDto MapMatter(MatterRow r) => new(
        r.DecisionMatterId, r.Title, r.MatterTypeCode, r.Jurisdiction, r.Posture, r.Description, r.StatusCode,
        r.CreatedDateUtc, r.ModifiedDateUtc)
    {
        PracticeAreaCode = r.PracticeAreaCode,
        ClaimTypeCode = r.ClaimTypeCode,
        DomainPackCode = r.DomainPackCode,
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
                        (DecisionFactPropositionId, DecisionSessionId, NodeCode, Statement, Support, VerificationStatus, SortOrder,
                         SourceBranchId, SourceCandidateId, SourceEvidenceId, SourceAuthorityId, MatterId, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @NodeCode, @Statement, @Support, @VerificationStatus, @SortOrder,
                         @SourceBranchId, @SourceCandidateId, @SourceEvidenceId, @SourceAuthorityId, @MatterId, @TenantId, @ActorUserId);
                    """,
                DecisionGraphNodeKinds.Proposition => """
                    INSERT INTO POLOXI.Legal_DecisionLegalProposition
                        (DecisionLegalPropositionId, DecisionSessionId, NodeCode, Statement, AuthorityRef, Support, VerificationStatus, SortOrder,
                         SourceBranchId, SourceCandidateId, SourceEvidenceId, SourceAuthorityId, MatterId, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @NodeCode, @Statement, @AuthorityRef, @Support, @VerificationStatus, @SortOrder,
                         @SourceBranchId, @SourceCandidateId, @SourceEvidenceId, @SourceAuthorityId, @MatterId, @TenantId, @ActorUserId);
                    """,
                DecisionGraphNodeKinds.Element => """
                    INSERT INTO POLOXI.Legal_DecisionLegalElement
                        (DecisionLegalElementId, DecisionSessionId, NodeCode, DisplayName, Statement, IsEssential, IsSatisfied, Support, VerificationStatus, SortOrder,
                         SourceBranchId, SourceCandidateId, SourceEvidenceId, SourceAuthorityId, MatterId, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @NodeCode, @DisplayName, @Statement, @IsEssential, @IsSatisfied, @Support, @VerificationStatus, @SortOrder,
                         @SourceBranchId, @SourceCandidateId, @SourceEvidenceId, @SourceAuthorityId, @MatterId, @TenantId, @ActorUserId);
                    """,
                DecisionGraphNodeKinds.Strategy => """
                    INSERT INTO POLOXI.Legal_DecisionReasoningStrategy
                        (DecisionReasoningStrategyId, DecisionSessionId, DecisionCandidateId, NodeCode, DisplayName, Rationale, Support, VerificationStatus, SortOrder,
                         SourceBranchId, SourceCandidateId, SourceEvidenceId, SourceAuthorityId, MatterId, TenantId, CreatedByUserId)
                    VALUES (@NodeId, @SessionId, @CandidateId, @NodeCode, @DisplayName, @Statement, @Support, @VerificationStatus, @SortOrder,
                         @SourceBranchId, @SourceCandidateId, @SourceEvidenceId, @SourceAuthorityId, @MatterId, @TenantId, @ActorUserId);
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

            // Persistence-boundary backstop for bounded local failure: a Burden rule requires a
            // BurdenedParty (NOT NULL). If a malformed node slipped past the proposal validator,
            // skip just this node instead of throwing and aborting the whole graph transaction.
            if (n.NodeKind == DecisionGraphNodeKinds.Burden && string.IsNullOrWhiteSpace(n.BurdenedParty))
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
                n.SourceBranchId,
                n.SourceCandidateId,
                n.SourceEvidenceId,
                n.SourceAuthorityId,
                n.MatterId,
                graph.TenantId,
                graph.ActorUserId
            }, transaction, cancellationToken: cancellationToken));
        }

        foreach (var e in graph.Edges)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO POLOXI.Legal_DecisionGraphEdge
                    (DecisionGraphEdgeId, DecisionSessionId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId,
                     SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, VerificationNotes, PropagatedStateCode,
                     SourceBranchId, SourceCandidateId, SourceEvidenceId, SourceAuthorityId, MatterId, AlternativePathAllowed,
                     PropagationPolicy, RowVersionNo, TenantId, CreatedByUserId)
                VALUES (@EdgeId, @SessionId, @RelationCode, @SourceNodeKind, @SourceNodeId, @TargetNodeKind, @TargetNodeId,
                     @SupportWeight, @Materiality, @IsEssential, @IsDispositive, @VerificationStatus, @VerificationNotes, @PropagatedStateCode,
                     @SourceBranchId, @SourceCandidateId, @SourceEvidenceId, @SourceAuthorityId, @MatterId, @AlternativePathAllowed,
                     @PropagationPolicy, @RowVersionNo, @TenantId, @ActorUserId);
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
                e.SourceBranchId,
                e.SourceCandidateId,
                e.SourceEvidenceId,
                e.SourceAuthorityId,
                e.MatterId,
                e.AlternativePathAllowed,
                e.PropagationPolicy,
                e.RowVersionNo,
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

        var edgeRows = await connection.QueryAsync<GraphEdgeRow>(new CommandDefinition("""
            SELECT DecisionGraphEdgeId AS EdgeId, RelationCode, SourceNodeKind, SourceNodeId, TargetNodeKind, TargetNodeId,
                   SupportWeight, Materiality, IsEssential, IsDispositive, VerificationStatus, VerificationNotes, PropagatedStateCode,
                   SourceBranchId, SourceCandidateId, SourceEvidenceId, SourceAuthorityId, MatterId,
                   AlternativePathAllowed, PropagationPolicy, RowVersionNo
            FROM POLOXI.Legal_DecisionGraphEdge WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
        var edges = edgeRows.Select(Map).ToArray();

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

    // ── POLOXI Legal V2.1 — closed-loop persistence ─────────────────────────────────────────────

    public async Task<DecisionV21Settings> GetV21SettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string SettingKey, string SettingValue)>(new CommandDefinition(
            "SELECT SettingKey, SettingValue FROM POLOXI.Legal_DecisionSetting WHERE IsDeleted = 0;",
            cancellationToken: cancellationToken));
        var map = rows.ToDictionary(r => r.SettingKey, r => r.SettingValue, StringComparer.OrdinalIgnoreCase);

        bool B(string key, bool fallback) => map.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;
        int I(string key, int fallback) => map.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : fallback;
        double D(string key, double fallback) => map.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : fallback;

        return new DecisionV21Settings(
            B("Decision.V2.UseDependencyPropagation", true),
            B("Decision.V2.UseGraphDrivenRecompetition", true),
            B("Decision.V2.UseGraphFrontierSignals", true),
            I("Decision.V2.Loop.MaxReopensPerBranch", 3),
            I("Decision.V2.Loop.MaxResearchActions", 8),
            D("Decision.V2.Loop.NoInformationGainEpsilon", 0.01),
            B("Decision.V2.Benchmark.Enabled", true));
    }

    public async Task<DecisionResearchLoopSettings> GetResearchLoopSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string SettingKey, string SettingValue)>(new CommandDefinition(
            "SELECT SettingKey, SettingValue FROM POLOXI.Legal_DecisionSetting WHERE IsDeleted = 0;",
            cancellationToken: cancellationToken));
        var map = rows.ToDictionary(r => r.SettingKey, r => r.SettingValue, StringComparer.OrdinalIgnoreCase);

        bool B(string key, bool fallback) => map.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;
        int I(string key, int fallback) => map.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var i) ? i : fallback;
        double D(string key, double fallback) => map.TryGetValue(key, out var v) && double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : fallback;

        // Default OFF: the bounded research loop stays disabled until validated end to end, preserving
        // the shadow baseline. Budgets are conservative so an accidental enable can never run away.
        return new DecisionResearchLoopSettings(
            B("Decision.ResearchLoop.Enabled", false),
            I("Decision.ResearchLoop.MaxRounds", 4),
            I("Decision.ResearchLoop.MaxRetrievals", 12),
            D("Decision.ResearchLoop.MinFrontierInformationValue", 0.15),
            D("Decision.ResearchLoop.NoStateChangeEpsilon", 0.01),
            B("Decision.ResearchLoop.UseSeedRetriever", false));
    }

    public async Task<DecisionDependencyEventPersistence?> GetDependencyEventAsync(Guid tenantId, Guid decisionSessionId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DecisionDependencyEventPersistence>(new CommandDefinition("""
            SELECT DecisionDependencyEventId, DecisionSessionId, TenantId, CreatedByUserId AS ActorUserId, MatterId,
                   DecisionGraphEdgeId, IdempotencyKey, PreviousStatus, NewStatus, ImpactJson,
                   AffectedBranchCount, AffectedCandidateCount, RecompetitionTriggered
            FROM POLOXI.Legal_DecisionDependencyEvent
            WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IdempotencyKey = @IdempotencyKey AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId, IdempotencyKey = idempotencyKey }, cancellationToken: cancellationToken));
    }

    public async Task PersistDependencyEventAsync(DecisionDependencyEventPersistence dependencyEvent, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionDependencyEvent
                (DecisionDependencyEventId, DecisionSessionId, MatterId, DecisionGraphEdgeId, IdempotencyKey, PreviousStatus,
                 NewStatus, ImpactJson, AffectedBranchCount, AffectedCandidateCount, RecompetitionTriggered, TenantId, CreatedByUserId)
            VALUES
                (@DecisionDependencyEventId, @DecisionSessionId, @MatterId, @DecisionGraphEdgeId, @IdempotencyKey, @PreviousStatus,
                 @NewStatus, @ImpactJson, @AffectedBranchCount, @AffectedCandidateCount, @RecompetitionTriggered, @TenantId, @ActorUserId);
            """, dependencyEvent, cancellationToken: cancellationToken));
    }

    public async Task PersistRecompetitionAsync(DecisionRecompetitionPersistence recompetition, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionRecompetition
                (DecisionRecompetitionId, DecisionSessionId, DecisionDependencyEventId, PreviousWinnerCandidateId, CurrentWinnerCandidateId,
                 WinnerChanged, PreviousEntropy, CurrentEntropy, PreviousMargin, CurrentMargin, ReopenedBranchCount,
                 PreviousRankingJson, CurrentRankingJson, ReasonCode, TenantId, CreatedByUserId)
            VALUES
                (@DecisionRecompetitionId, @DecisionSessionId, @DecisionDependencyEventId, @PreviousWinnerCandidateId, @CurrentWinnerCandidateId,
                 @WinnerChanged, @PreviousEntropy, @CurrentEntropy, @PreviousMargin, @CurrentMargin, @ReopenedBranchCount,
                 @PreviousRankingJson, @CurrentRankingJson, @ReasonCode, @TenantId, @ActorUserId);
            """, recompetition, cancellationToken: cancellationToken));
    }

    public async Task PersistResearchNeedAsync(DecisionResearchNeedPersistence researchNeed, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionResearchNeed
                (DecisionResearchNeedId, DecisionSessionId, MatterId, DecisionBranchId, DecisionDependencyEventId, IssueLabel,
                 PropositionToResolve, ResearchNeedTypeCode, AuthorityKind, RequiredEvidenceKind, WhyDecisionRelevant, ExpectedDiscrimination,
                 CurrentUncertainty, InformationValue, FalsificationCondition, StatusCode, TenantId, CreatedByUserId,
                  SourceClassCode, IsResearchable, ResearchKey, ResearchQuestion, SearchQuery, SearchConceptsJson,
                  AuthorityKindsJson, ApplicationDeferred, ParentResearchKey, RequiredResearchKeysJson,
                 CandidateDiscriminationJson, SemanticProposalStatusCode, SemanticProposalReasonCode)
            VALUES
                (@DecisionResearchNeedId, @DecisionSessionId, @MatterId, @DecisionBranchId, @DecisionDependencyEventId, @IssueLabel,
                 @PropositionToResolve, @ResearchNeedTypeCode, @AuthorityKind, @RequiredEvidenceKind, @WhyDecisionRelevant, @ExpectedDiscrimination,
                 @CurrentUncertainty, @InformationValue, @FalsificationCondition, @StatusCode, @TenantId, @ActorUserId,
                  @SourceClassCode, @IsResearchable, @ResearchKey, @ResearchQuestion, @SearchQuery, @SearchConceptsJson,
                  @AuthorityKindsJson, @ApplicationDeferred, @ParentResearchKey, @RequiredResearchKeysJson,
                 @CandidateDiscriminationJson, @SemanticProposalStatusCode, @SemanticProposalReasonCode);
            """, researchNeed, cancellationToken: cancellationToken));
    }

    public async Task PersistEvidenceAttachmentsAsync(
        IReadOnlyCollection<DecisionEvidenceAttachmentPersistence> attachments,
        CancellationToken cancellationToken = default)
    {
        if (attachments.Count == 0)
            return;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionEvidenceAttachment
                (DecisionEvidenceAttachmentId, DecisionSessionId, DecisionResearchNeedId, DecisionBranchId,
                 DecisionEvidenceId, PropositionToResolve, SupportStateCode, AffectedGraphEdgeId,
                 IsAuthoritative, AssessmentReason, MatterId, TenantId, CreatedByUserId,
                 DecisionEvidenceVerificationId, SourceSnapshotId, PassageRef)
            VALUES
                (@DecisionEvidenceAttachmentId, @DecisionSessionId, @DecisionResearchNeedId, @DecisionBranchId,
                 @DecisionEvidenceId, @PropositionToResolve, @SupportStateCode, @AffectedGraphEdgeId,
                 @IsAuthoritative, @AssessmentReason, @MatterId, @TenantId, @ActorUserId,
                 @DecisionEvidenceVerificationId, @SourceSnapshotId, @PassageRef);
            """, attachments, cancellationToken: cancellationToken));
    }

    public async Task UpdateEvidenceAttachmentsAsync(
        IReadOnlyCollection<DecisionEvidenceAttachmentPersistence> attachments,
        CancellationToken cancellationToken = default)
    {
        if (attachments.Count == 0)
            return;

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE POLOXI.Legal_DecisionEvidenceAttachment
               SET SupportStateCode = @SupportStateCode,
                   AffectedGraphEdgeId = @AffectedGraphEdgeId,
                   IsAuthoritative = @IsAuthoritative,
                   AssessmentReason = @AssessmentReason,
                   ModifiedDateUtc = SYSUTCDATETIME(),
                   ModifiedByUserId = @ActorUserId
             WHERE DecisionEvidenceAttachmentId = @DecisionEvidenceAttachmentId
               AND DecisionSessionId = @DecisionSessionId
               AND TenantId = @TenantId
               AND IsDeleted = 0;
            """, attachments, cancellationToken: cancellationToken));
    }

    public async Task PersistFrontierSnapshotAsync(DecisionFrontierSnapshotPersistence snapshot, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_DecisionFrontierSnapshot
                (DecisionFrontierSnapshotId, DecisionSessionId, DecisionRecompetitionId, Entropy, Margin, OpenFrontierCount,
                 TopBranchId, TopBranchInformationValue, FrontierJson, TenantId, CreatedByUserId)
            VALUES
                (@DecisionFrontierSnapshotId, @DecisionSessionId, @DecisionRecompetitionId, @Entropy, @Margin, @OpenFrontierCount,
                 @TopBranchId, @TopBranchInformationValue, @FrontierJson, @TenantId, @ActorUserId);
            """, snapshot, cancellationToken: cancellationToken));
    }

    public async Task<int> CountBranchReopensAsync(Guid tenantId, Guid decisionSessionId, Guid decisionBranchId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM POLOXI.Legal_DecisionResearchNeed
            WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND DecisionBranchId = @BranchId AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId, BranchId = decisionBranchId }, cancellationToken: cancellationToken));
    }

    public async Task<int> CountResearchNeedsAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM POLOXI.Legal_DecisionResearchNeed
            WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND StatusCode = N'OPEN' AND IsDeleted = 0;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<DecisionRecompetitionPersistence?> GetLatestRecompetitionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DecisionRecompetitionPersistence>(new CommandDefinition("""
            SELECT TOP 1 DecisionRecompetitionId, DecisionSessionId, TenantId, CreatedByUserId AS ActorUserId, DecisionDependencyEventId,
                   PreviousWinnerCandidateId, CurrentWinnerCandidateId, WinnerChanged, PreviousEntropy, CurrentEntropy,
                   PreviousMargin, CurrentMargin, ReopenedBranchCount, PreviousRankingJson, CurrentRankingJson, ReasonCode
            FROM POLOXI.Legal_DecisionRecompetition
            WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND IsDeleted = 0
            ORDER BY CreatedDateUtc DESC;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<DecisionResearchNeedPersistence?> GetLatestOpenResearchNeedAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<DecisionResearchNeedRow>(new CommandDefinition("""
            SELECT TOP 1 DecisionResearchNeedId, DecisionSessionId, TenantId, CreatedByUserId AS ActorUserId, MatterId, DecisionBranchId,
                   DecisionDependencyEventId, IssueLabel, PropositionToResolve, AuthorityKind, RequiredEvidenceKind, WhyDecisionRelevant,
                   ExpectedDiscrimination, CurrentUncertainty, InformationValue, FalsificationCondition, StatusCode, ResearchNeedTypeCode,
                    SourceClassCode, IsResearchable, ResearchKey, ResearchQuestion, SearchQuery, SearchConceptsJson,
                    AuthorityKindsJson, ApplicationDeferred, ParentResearchKey, RequiredResearchKeysJson,
                   CandidateDiscriminationJson, SemanticProposalStatusCode, SemanticProposalReasonCode
            FROM POLOXI.Legal_DecisionResearchNeed
            WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId AND StatusCode = N'OPEN' AND IsDeleted = 0
            ORDER BY InformationValue DESC, CreatedDateUtc DESC;
            """, new { SessionId = decisionSessionId, TenantId = tenantId }, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new DecisionResearchNeedPersistence(
                row.DecisionResearchNeedId, row.DecisionSessionId, row.TenantId, row.ActorUserId,
                row.MatterId, row.DecisionBranchId, row.DecisionDependencyEventId, row.IssueLabel,
                row.PropositionToResolve, row.AuthorityKind, row.RequiredEvidenceKind, row.WhyDecisionRelevant,
                row.ExpectedDiscrimination, row.CurrentUncertainty, row.InformationValue,
                row.FalsificationCondition, row.StatusCode)
            {
                ResearchNeedTypeCode = row.ResearchNeedTypeCode,
                SourceClassCode = row.SourceClassCode,
                IsResearchable = row.IsResearchable,
                ResearchKey = row.ResearchKey,
                ResearchQuestion = row.ResearchQuestion,
                SearchQuery = row.SearchQuery,
                SearchConceptsJson = row.SearchConceptsJson,
                AuthorityKindsJson = row.AuthorityKindsJson,
                ApplicationDeferred = row.ApplicationDeferred,
                ParentResearchKey = row.ParentResearchKey,
                RequiredResearchKeysJson = row.RequiredResearchKeysJson,
                CandidateDiscriminationJson = row.CandidateDiscriminationJson,
                SemanticProposalStatusCode = row.SemanticProposalStatusCode,
                SemanticProposalReasonCode = row.SemanticProposalReasonCode,
            };
    }

    public async Task UpdateSessionOutcomeAsync(Guid tenantId, Guid userId, Guid decisionSessionId, string statusCode, decimal entropy, decimal margin, Guid? winnerCandidateId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE POLOXI.Legal_DecisionSession
               SET StatusCode = @StatusCode,
                   CandidateEntropy = @Entropy,
                   DecisionMargin = @Margin,
                   WinnerCandidateId = @WinnerCandidateId,
                   ModifiedDateUtc = SYSUTCDATETIME(),
                   ModifiedByUserId = @UserId
             WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId;
            """, new { StatusCode = statusCode, Entropy = entropy, Margin = margin, WinnerCandidateId = winnerCandidateId, SessionId = decisionSessionId, TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateSessionAnswerAsync(Guid tenantId, Guid userId, Guid decisionSessionId, string? finalAnswer, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE POLOXI.Legal_DecisionSession
               SET FinalAnswer = @FinalAnswer,
                   ModifiedDateUtc = SYSUTCDATETIME(),
                   ModifiedByUserId = @UserId
             WHERE DecisionSessionId = @SessionId AND TenantId = @TenantId;
            """, new { FinalAnswer = finalAnswer, SessionId = decisionSessionId, TenantId = tenantId, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task ReplaceBranchesAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionBranchPersistence> branches, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        foreach (var b in branches)
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE POLOXI.Legal_DecisionBranch
                   SET BranchStateCode = @BranchStateCode,
                       InformationValue = @InformationValue,
                       DecisionRelevance = @DecisionRelevance,
                       FlipPotential = @FlipPotential,
                       EvidenceAvailability = @EvidenceAvailability,
                       AdvScore = @AdvScore,
                       Cost = @Cost,
                       IsOnFrontier = @IsOnFrontier,
                       StopReason = @StopReason,
                       ModifiedDateUtc = SYSUTCDATETIME(),
                       ModifiedByUserId = @UserId
                 WHERE DecisionBranchId = @DecisionBranchId AND DecisionSessionId = @SessionId AND TenantId = @TenantId;
                """, new
            {
                b.BranchStateCode, b.InformationValue, b.DecisionRelevance, b.FlipPotential, b.EvidenceAvailability,
                b.AdvScore, b.Cost, b.IsOnFrontier, b.StopReason, b.DecisionBranchId,
                SessionId = decisionSessionId, TenantId = tenantId, UserId = userId
            }, cancellationToken: cancellationToken));
    }

    public async Task ReplaceCandidatesAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionCandidatePersistence> candidates, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        foreach (var c in candidates)
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE POLOXI.Legal_DecisionCandidate
                   SET EvidenceSupport = @EvidenceSupport,
                       AuthoritySupport = @AuthoritySupport,
                       VerificationScore = @Verification,
                       Uncertainty = @Uncertainty,
                       CompositeScore = @CompositeScore,
                       DecisionSupportCeiling = @DecisionSupportCeiling,
                       RankOrder = @RankOrder,
                       IsWinner = @IsWinner,
                       IsEliminated = @IsEliminated,
                       ModifiedDateUtc = SYSUTCDATETIME(),
                       ModifiedByUserId = @UserId
                 WHERE DecisionCandidateId = @DecisionCandidateId AND DecisionSessionId = @SessionId AND TenantId = @TenantId;
                """, new
            {
                c.EvidenceSupport, c.AuthoritySupport, c.Verification, c.Uncertainty, c.CompositeScore,
                c.DecisionSupportCeiling, c.RankOrder, c.IsWinner, c.IsEliminated, c.DecisionCandidateId,
                SessionId = decisionSessionId, TenantId = tenantId, UserId = userId
            }, cancellationToken: cancellationToken));
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

    private static DecisionEvidencePersistence Map(DecisionEvidenceRow r) => new(
        r.DecisionEvidenceId, r.DecisionBranchId, r.SourceRef, r.SourceTitle, r.Snippet,
        r.IdentityFactor, r.CitationFactor, r.HoldingFactor, r.WeightFactor,
        r.PropositionFit, r.VerificationValue, r.VerificationStatus)
    {
        SupportedObjective = r.SupportedObjective,
        SupportingPassage = r.SupportingPassage,
        LifecycleState = r.LifecycleState
    };

    private static DecisionGraphEdgePersistence Map(GraphEdgeRow r) => new(
        r.EdgeId, r.RelationCode, r.SourceNodeKind, r.SourceNodeId, r.TargetNodeKind, r.TargetNodeId,
        r.SupportWeight, r.Materiality, r.IsEssential, r.IsDispositive, r.VerificationStatus,
        r.VerificationNotes, r.PropagatedStateCode)
    {
        SourceBranchId = r.SourceBranchId,
        SourceCandidateId = r.SourceCandidateId,
        SourceEvidenceId = r.SourceEvidenceId,
        SourceAuthorityId = r.SourceAuthorityId,
        MatterId = r.MatterId,
        AlternativePathAllowed = r.AlternativePathAllowed,
        PropagationPolicy = r.PropagationPolicy,
        RowVersionNo = r.RowVersionNo
    };

    private sealed record GraphEdgeRow(
        Guid EdgeId, string RelationCode, string SourceNodeKind, Guid SourceNodeId,
        string TargetNodeKind, Guid TargetNodeId, decimal SupportWeight, decimal Materiality,
        bool IsEssential, bool IsDispositive, string VerificationStatus, string? VerificationNotes,
        string? PropagatedStateCode, Guid? SourceBranchId, Guid? SourceCandidateId,
        Guid? SourceEvidenceId, string? SourceAuthorityId, Guid? MatterId,
        bool AlternativePathAllowed, string PropagationPolicy, int RowVersionNo);

    private sealed record DecisionEvidenceRow(
        Guid DecisionEvidenceId,
        Guid? DecisionBranchId,
        string? SourceRef,
        string? SourceTitle,
        string? Snippet,
        decimal IdentityFactor,
        decimal CitationFactor,
        decimal HoldingFactor,
        decimal WeightFactor,
        decimal PropositionFit,
        decimal VerificationValue,
        string VerificationStatus,
        string? LifecycleState,
        string? SupportedObjective,
        string? SupportingPassage);

    private sealed record DecisionEvidenceVerificationRow(
        Guid DecisionEvidenceVerificationId, Guid DecisionEvidenceId, Guid DecisionSessionId,
        Guid? DecisionBranchId, string SourceTypeCode, string ProfileCode, string DispositionCode,
        bool IsVerified, bool IsDecisionAuthorized, string? BlockingReasonsJson, Guid? MatterId,
        Guid TenantId, Guid? ActorUserId, DateTime EvaluatedDateUtc,
        int MechanicalVerificationCount, int SemanticVerificationCount, int PoloxiDeepeningCount,
        int CacheHitCount, int InputTokenCount, int OutputTokenCount, long LatencyMilliseconds,
        Guid? SourceSnapshotId, int ProfileVersion, int VerificationVersion,
        int RetrievedCount, int PreScreenRejectedCount);

    private sealed record DecisionEvidenceVerificationFactorRow(
        Guid DecisionEvidenceVerificationFactorId, Guid DecisionEvidenceVerificationId,
        string FactorCode, string StateCode, string ReasonCode, string? Reason, string? VerifiedValue,
        string? SourceRef, string? SupportingPassage, string VerificationMethod,
        string? SupportedComponentsJson, string? UnsupportedComponentsJson, DateTime EvaluatedDateUtc,
        string? PassageRef, string? VerifierId, string? VerifierVersion);

    private sealed class DecisionResearchNeedRow
    {
        public Guid DecisionResearchNeedId { get; init; }
        public Guid DecisionSessionId { get; init; }
        public Guid TenantId { get; init; }
        public Guid? ActorUserId { get; init; }
        public Guid? MatterId { get; init; }
        public Guid? DecisionBranchId { get; init; }
        public Guid? DecisionDependencyEventId { get; init; }
        public string? IssueLabel { get; init; }
        public string? PropositionToResolve { get; init; }
        public string? AuthorityKind { get; init; }
        public string? RequiredEvidenceKind { get; init; }
        public string? WhyDecisionRelevant { get; init; }
        public decimal ExpectedDiscrimination { get; init; }
        public decimal CurrentUncertainty { get; init; }
        public decimal InformationValue { get; init; }
        public string? FalsificationCondition { get; init; }
        public string StatusCode { get; init; } = string.Empty;
        public string ResearchNeedTypeCode { get; init; } = string.Empty;
        public string SourceClassCode { get; init; } = DecisionResearchSourceClasses.LegalAuthority;
        public bool IsResearchable { get; init; } = true;
        public string? ResearchKey { get; init; }
        public string? ResearchQuestion { get; init; }
        public string? SearchQuery { get; init; }
        public string? SearchConceptsJson { get; init; }
        public string? AuthorityKindsJson { get; init; }
        public bool ApplicationDeferred { get; init; }
        public string? ParentResearchKey { get; init; }
        public string? RequiredResearchKeysJson { get; init; }
        public string? CandidateDiscriminationJson { get; init; }
        public string? SemanticProposalStatusCode { get; init; }
        public string? SemanticProposalReasonCode { get; init; }
    }

    private sealed record GraphNodeRow(
        Guid NodeId, string NodeCode, string? DisplayName, string? Statement, decimal Support, bool IsEssential,
        bool IsSatisfied, bool IsDispositive, string VerificationStatus, int SortOrder,
        string? AuthorityRef, string? BurdenedParty, string? StandardOfProof, Guid? CandidateId);

    private sealed record MatterRow(
        Guid DecisionMatterId, string Title, string? MatterTypeCode, string? Jurisdiction, string? Posture, string? Description,
        string StatusCode,
        string? PracticeAreaCode, string? ClaimTypeCode, string? DomainPackCode,
        string? Subtype, string? CourtSystem, string? State, string? CourtLevel, string? County, string? GoverningLaw,
        string? MovingParty, string? RespondingParty, string? MotionTarget, string? RequestedDisposition,
        DateTime CreatedDateUtc, DateTime? ModifiedDateUtc, Guid? LatestSessionId, string? CurrentOutcome,
        string? DecisionStatusCode, DateTime? LastDecidedUtc, int CriticalFlipPointCount);

    private sealed record SessionRow(
        Guid DecisionSessionId, Guid TenantId, Guid? ActorUserId, string QueryText, string? ContextCode, string? ModelCode,
        bool UsePoloxiEngine, string StatusCode, string? TerminalStateCode, string TerminationReason, Guid? WinnerCandidateId,
        decimal ContractCompleteness, decimal CandidateEntropy, decimal DecisionMargin, int DepthReached, int LlmCallCount,
        long DurationMs, string? FinalAnswer, string? ClarificationQuestion, string? ClarificationTarget, string? CorrelationId,
        Guid? MatterId, string? NextBestActionText, string? NextBestActionImpactCode, string? NextBestActionRationale,
        string? ResearchStatusCode, string? ResearchFailureDetail);
}
