using System.Data;
using System.Text.Json;
using Dapper;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// Write side of the Run & Assessment Management execution ledger. Persists a completed decision
// execution as an AnalysisRun plus response-derived steps, inside a single transaction. All content
// comes from the actual DecisionSearchResponse (no synthesized data); only matter-scoped decisions
// are recorded because the ledger is matter-scoped.
public sealed class AnalysisRunLedgerWriter(
    ISqlConnectionFactory connectionFactory,
    ILegalDecisionContractRepository contractRepository) : IAnalysisRunLedgerWriter
{
    public async Task<Guid?> RecordDecisionRunAsync(
        DecisionSearchRequest request,
        DecisionSearchResponse response,
        CancellationToken cancellationToken = default)
    {
        var matterId = request.MatterId ?? response.MatterId;
        if (matterId is null || matterId == Guid.Empty)
            return null; // Ad-hoc decision: not part of the matter ledger.

        // Load the full, DB-backed Decision Contract (Draft) actually in effect for this matter so the
        // immutable input snapshot preserves every Draft field — not just the request search projection.
        var contract = await contractRepository.GetCurrentContractAsync(
            request.TenantId, matterId.Value, cancellationToken);

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            // Sequence-based display labels, scoped to the matter (R-1, R-2, ... / A-1, A-2, ...).
            var runCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(*) FROM POLOXI.Legal_AnalysisRun
                WHERE TenantId = @TenantId AND MatterId = @MatterId AND IsDeleted = 0;
                """,
                new { request.TenantId, MatterId = matterId },
                transaction,
                cancellationToken: cancellationToken));

            var runLabel = $"R-{runCount + 1}";
            var snapshotLabel = $"S-{runCount + 1}";
            var runId = Guid.NewGuid();
            var requestedUtc = DateTime.UtcNow;
            var durationSeconds = (int)Math.Round(response.DurationMilliseconds / 1000.0);
            var executionStatus = MapExecutionStatus(response);
            var title = BuildTitle(request, response);
            var idempotencyKey = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? response.DecisionSessionId.ToString("N")
                : request.CorrelationId;

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_AnalysisRun
                    (AnalysisRunId, MatterId, DecisionId, RunLabel, Title, RunTypeCode, TriggerTypeCode, TriggerText,
                     ExecutionStatusCode, PublicationStatusCode, PackLabel, InputSnapshotLabel,
                     RequestedUtc, StartedUtc, CompletedUtc, DurationSeconds, ExecutionBanner, FooterNote,
                     IdempotencyScope, IdempotencyKey, TenantId, CreatedByUserId)
                VALUES
                    (@AnalysisRunId, @MatterId, @DecisionId, @RunLabel, @Title, @RunTypeCode, @TriggerTypeCode, @TriggerText,
                     @ExecutionStatusCode, @PublicationStatusCode, @PackLabel, @InputSnapshotLabel,
                     @RequestedUtc, @StartedUtc, @CompletedUtc, @DurationSeconds, @ExecutionBanner, @FooterNote,
                     @IdempotencyScope, @IdempotencyKey, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    AnalysisRunId = runId,
                    MatterId = matterId,
                    DecisionId = (Guid?)response.DecisionSessionId,
                    RunLabel = runLabel,
                    Title = title,
                    RunTypeCode = "MATTER_ANALYSIS",
                    TriggerTypeCode = request.CounterfactualAssumption is null ? "DECISION_RUN" : "COUNTERFACTUAL",
                    TriggerText = request.CounterfactualAssumption,
                    ExecutionStatusCode = executionStatus,
                    PublicationStatusCode = "NotPublished",
                    PackLabel = request.DomainPackCode is null ? "Original" : request.DomainPackCode,
                    InputSnapshotLabel = snapshotLabel,
                    RequestedUtc = requestedUtc,
                    StartedUtc = requestedUtc,
                    CompletedUtc = requestedUtc.AddMilliseconds(response.DurationMilliseconds),
                    DurationSeconds = durationSeconds,
                    ExecutionBanner = BuildBanner(executionStatus, response),
                    FooterNote = (string?)null,
                    IdempotencyScope = "DECISION",
                    IdempotencyKey = idempotencyKey,
                    request.TenantId,
                    CreatedByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId),
                },
                transaction,
                cancellationToken: cancellationToken));

            var steps = DeriveSteps(response);
            foreach (var step in steps)
            {
                var stepId = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_AnalysisRunStep
                        (AnalysisRunStepId, AnalysisRunId, StepCode, DisplayName, Description, Sequence,
                         StatusCode, StatusLabel, TenantId, CreatedByUserId)
                    VALUES
                        (@AnalysisRunStepId, @AnalysisRunId, @StepCode, @DisplayName, @Description, @Sequence,
                         @StatusCode, @StatusLabel, @TenantId, @CreatedByUserId);
                    """,
                    new
                    {
                        AnalysisRunStepId = stepId,
                        AnalysisRunId = runId,
                        step.StepCode,
                        step.DisplayName,
                        step.Description,
                        step.Sequence,
                        step.StatusCode,
                        step.StatusLabel,
                        request.TenantId,
                        CreatedByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId),
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                await InsertStepAttemptsAsync(
                    connection, transaction, request, response, step.StepCode, stepId, cancellationToken);
            }

            await InsertInputSnapshotAsync(
                connection, transaction, request, matterId.Value, runId, snapshotLabel, contract, cancellationToken);

            await UpsertCurrentAssessmentAsync(
                connection, transaction, request, response, matterId.Value, runId, runLabel, executionStatus,
                cancellationToken);

            transaction.Commit();
            return runId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // POLOXI Wide (decision cockpit) execution ledger write. Persists a completed Wide search as an
    // AnalysisRun plus response-derived steps and an immutable input snapshot, inside a single
    // transaction. All content comes from the actual WideSearchResponse (no synthesized data); only
    // matter-scoped executions are recorded because the ledger is matter-scoped.
    public async Task<Guid?> RecordWideRunAsync(
        WideSearchRequest request,
        WideSearchResponse response,
        CancellationToken cancellationToken = default)
    {
        var matterId = request.MatterId ?? request.DecisionMatterId;
        if (matterId is null || matterId == Guid.Empty)
            return null; // Ad-hoc Wide search: not part of the matter ledger.

        // Load the full, DB-backed Decision Contract (Draft) actually in effect for this matter so the
        // immutable input snapshot preserves every Draft field — not just the request search projection.
        var wideContract = await contractRepository.GetCurrentContractAsync(
            request.TenantId, matterId.Value, cancellationToken);

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        try
        {
            var runCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT COUNT(*) FROM POLOXI.Legal_AnalysisRun
                WHERE TenantId = @TenantId AND MatterId = @MatterId AND IsDeleted = 0;
                """,
                new { request.TenantId, MatterId = matterId },
                transaction,
                cancellationToken: cancellationToken));

            var runLabel = $"R-{runCount + 1}";
            var snapshotLabel = $"S-{runCount + 1}";
            var runId = Guid.NewGuid();
            var requestedUtc = DateTime.UtcNow;
            var durationSeconds = (int)Math.Round(response.DurationMilliseconds / 1000.0);
            var executionStatus = MapWideExecutionStatus(response);
            var title = BuildWideTitle(request, response);
            var idempotencyKey = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? response.WideExecutionId.ToString("N")
                : request.CorrelationId;
            var createdByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_AnalysisRun
                    (AnalysisRunId, MatterId, DecisionId, RunLabel, Title, RunTypeCode, TriggerTypeCode, TriggerText,
                     ExecutionStatusCode, PublicationStatusCode, PackLabel, InputSnapshotLabel,
                     RequestedUtc, StartedUtc, CompletedUtc, DurationSeconds, ExecutionBanner, FooterNote,
                     IdempotencyScope, IdempotencyKey, TenantId, CreatedByUserId)
                VALUES
                    (@AnalysisRunId, @MatterId, @DecisionId, @RunLabel, @Title, @RunTypeCode, @TriggerTypeCode, @TriggerText,
                     @ExecutionStatusCode, @PublicationStatusCode, @PackLabel, @InputSnapshotLabel,
                     @RequestedUtc, @StartedUtc, @CompletedUtc, @DurationSeconds, @ExecutionBanner, @FooterNote,
                     @IdempotencyScope, @IdempotencyKey, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    AnalysisRunId = runId,
                    MatterId = matterId,
                    DecisionId = (Guid?)response.WideExecutionId,
                    RunLabel = runLabel,
                    Title = title,
                    RunTypeCode = "MATTER_ANALYSIS",
                    TriggerTypeCode = string.IsNullOrWhiteSpace(request.ClarificationAnswer) ? "DECISION_RUN" : "CLARIFICATION",
                    TriggerText = request.ClarificationAnswer,
                    ExecutionStatusCode = executionStatus,
                    PublicationStatusCode = "NotPublished",
                    PackLabel = request.UseDomainPack ? "Domain Pack" : "Original",
                    InputSnapshotLabel = snapshotLabel,
                    RequestedUtc = requestedUtc,
                    StartedUtc = requestedUtc,
                    CompletedUtc = requestedUtc.AddMilliseconds(response.DurationMilliseconds),
                    DurationSeconds = durationSeconds,
                    ExecutionBanner = BuildWideBanner(executionStatus),
                    FooterNote = (string?)null,
                    IdempotencyScope = "WIDE",
                    IdempotencyKey = idempotencyKey,
                    request.TenantId,
                    CreatedByUserId = createdByUserId,
                },
                transaction,
                cancellationToken: cancellationToken));

            var steps = DeriveWideSteps(response);
            foreach (var step in steps)
            {
                var stepId = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_AnalysisRunStep
                        (AnalysisRunStepId, AnalysisRunId, StepCode, DisplayName, Description, Sequence,
                         StatusCode, StatusLabel, TenantId, CreatedByUserId)
                    VALUES
                        (@AnalysisRunStepId, @AnalysisRunId, @StepCode, @DisplayName, @Description, @Sequence,
                         @StatusCode, @StatusLabel, @TenantId, @CreatedByUserId);
                    """,
                    new
                    {
                        AnalysisRunStepId = stepId,
                        AnalysisRunId = runId,
                        step.StepCode,
                        step.DisplayName,
                        step.Description,
                        step.Sequence,
                        step.StatusCode,
                        step.StatusLabel,
                        request.TenantId,
                        CreatedByUserId = createdByUserId,
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                await InsertWideStepAttemptsAsync(
                    connection, transaction, request, response, step.StepCode, stepId, cancellationToken);
            }

            await InsertWideInputSnapshotAsync(
                connection, transaction, request, matterId.Value, runId, snapshotLabel, wideContract, cancellationToken);

            // Capture the ENTIRE Decision2 result graph so Analysis History can reopen the exact display.
            await InsertWideRunResultAsync(
                connection, transaction, request, response, matterId.Value, runId, createdByUserId, cancellationToken);

            await UpsertWideCurrentAssessmentAsync(
                connection, transaction, request, response, matterId.Value, runId, runLabel, executionStatus,
                cancellationToken);

            transaction.Commit();
            return runId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // ── Immutable input snapshot (S-###) for a Wide execution. Captures the search input contract. ──
    private static async Task InsertWideInputSnapshotAsync(
        IDbConnection connection, IDbTransaction transaction,
        WideSearchRequest request, Guid matterId, Guid runId, string snapshotLabel,
        DecisionContractDto? contract,
        CancellationToken cancellationToken)
    {
        var projection = new
        {
            request.Query,
            request.MaximumResults,
            request.CorrelationId,
            request.UsePoloxiEngine,
            request.ModelCode,
            request.ContextCode,
            request.UseDomainPack,
            request.ClarificationAnswer,
            request.ClarificationTarget,
            request.ClarificationRound,
            // Full DB-backed Decision Contract (Draft) in effect at execution time. Preserves every
            // Draft field and child collection so the run is fully reproducible. Null when no contract.
            DecisionContract = contract,
        };

        var contractJson = JsonSerializer.Serialize(projection);
        var createdByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_AnalysisRunInputSnapshot
                (InputSnapshotId, AnalysisRunId, MatterId, SnapshotLabel, Query, ContextCode, ModeCode, ModelCode,
                 DomainPackCode, DiscoveryModeCode, Jurisdiction, GoverningLaw, CourtOrForum, Posture, MotionTarget,
                 CounterfactualAssumption, ClarificationAnswer, UsedDependencyGraph, ContractJson,
                 TenantId, CreatedByUserId)
            VALUES
                (@InputSnapshotId, @AnalysisRunId, @MatterId, @SnapshotLabel, @Query, @ContextCode, @ModeCode, @ModelCode,
                 @DomainPackCode, @DiscoveryModeCode, @Jurisdiction, @GoverningLaw, @CourtOrForum, @Posture, @MotionTarget,
                 @CounterfactualAssumption, @ClarificationAnswer, @UsedDependencyGraph, @ContractJson,
                 @TenantId, @CreatedByUserId);
            """,
            new
            {
                InputSnapshotId = Guid.NewGuid(),
                AnalysisRunId = runId,
                MatterId = matterId,
                SnapshotLabel = snapshotLabel,
                request.Query,
                request.ContextCode,
                ModeCode = "WIDE",
                request.ModelCode,
                DomainPackCode = (string?)null,
                DiscoveryModeCode = request.UseDomainPack ? "DOMAIN_PACK" : "DEFAULT",
                Jurisdiction = contract?.Jurisdiction,
                GoverningLaw = contract?.GoverningLaw,
                CourtOrForum = contract?.CourtOrForum,
                Posture = contract?.ProceduralPosture,
                MotionTarget = (string?)null,
                CounterfactualAssumption = (string?)null,
                request.ClarificationAnswer,
                UsedDependencyGraph = (bool?)request.UsePoloxiEngine,
                ContractJson = contractJson,
                request.TenantId,
                CreatedByUserId = createdByUserId,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    // ── Full POLOXI Wide (Decision2) result capture. Persists the ENTIRE WideSearchResponse: a header
    // row with every top-level scalar, one normalized child row per display collection, and a lossless
    // DetailJson of the whole response so the read-only Decision2 display reproduces it verbatim. ──
    private static async Task InsertWideRunResultAsync(
        IDbConnection connection, IDbTransaction transaction,
        WideSearchRequest request, WideSearchResponse response,
        Guid matterId, Guid runId, Guid? createdByUserId,
        CancellationToken cancellationToken)
    {
        var resultId = Guid.NewGuid();
        var detailJson = JsonSerializer.Serialize(response);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_WideRunResult
                (WideRunResultId, AnalysisRunId, MatterId, WideExecutionId, Query, StatusCode,
                 TerminationReasonCode, DepthReached, LlmCallCount, FinalConfidence, AnswerVerificationCode,
                 FinalAnswer, DurationMilliseconds, AnswerKindCode, AnswerKindRoutingApplied, ProviderCodeUsed,
                 ModelCodeUsed, EvidenceCoverage, DecisionEvidenceCoverage, ExternalEvidenceCount,
                 EnterpriseEvidenceCount, InitialEntropy, FinalEntropy, InitialNormalizedEntropy,
                 FinalNormalizedEntropy, TotalActualInformationGain, EntropyBasisCode, WinnerStability,
                 TopKStability, DecisionConfidence, ClarificationQuestion, ClarificationTarget, IntentEntropy,
                 BestClarificationValue, ClarificationGain, ClarificationRound, FinalNarrowingTrend, DetailJson,
                 TenantId, CreatedByUserId)
            VALUES
                (@WideRunResultId, @AnalysisRunId, @MatterId, @WideExecutionId, @Query, @StatusCode,
                 @TerminationReasonCode, @DepthReached, @LlmCallCount, @FinalConfidence, @AnswerVerificationCode,
                 @FinalAnswer, @DurationMilliseconds, @AnswerKindCode, @AnswerKindRoutingApplied, @ProviderCodeUsed,
                 @ModelCodeUsed, @EvidenceCoverage, @DecisionEvidenceCoverage, @ExternalEvidenceCount,
                 @EnterpriseEvidenceCount, @InitialEntropy, @FinalEntropy, @InitialNormalizedEntropy,
                 @FinalNormalizedEntropy, @TotalActualInformationGain, @EntropyBasisCode, @WinnerStability,
                 @TopKStability, @DecisionConfidence, @ClarificationQuestion, @ClarificationTarget, @IntentEntropy,
                 @BestClarificationValue, @ClarificationGain, @ClarificationRound, @FinalNarrowingTrend, @DetailJson,
                 @TenantId, @CreatedByUserId);
            """,
            new
            {
                WideRunResultId = resultId,
                AnalysisRunId = runId,
                MatterId = matterId,
                WideExecutionId = response.WideExecutionId,
                response.Query,
                response.StatusCode,
                response.TerminationReasonCode,
                response.DepthReached,
                response.LlmCallCount,
                response.FinalConfidence,
                response.AnswerVerificationCode,
                response.FinalAnswer,
                response.DurationMilliseconds,
                response.AnswerKindCode,
                response.AnswerKindRoutingApplied,
                response.ProviderCodeUsed,
                response.ModelCodeUsed,
                response.EvidenceCoverage,
                response.DecisionEvidenceCoverage,
                response.ExternalEvidenceCount,
                response.EnterpriseEvidenceCount,
                response.InitialEntropy,
                response.FinalEntropy,
                response.InitialNormalizedEntropy,
                response.FinalNormalizedEntropy,
                response.TotalActualInformationGain,
                response.EntropyBasisCode,
                response.WinnerStability,
                response.TopKStability,
                response.DecisionConfidence,
                response.ClarificationQuestion,
                response.ClarificationTarget,
                response.IntentEntropy,
                response.BestClarificationValue,
                response.ClarificationGain,
                response.ClarificationRound,
                response.FinalNarrowingTrend,
                DetailJson = detailJson,
                request.TenantId,
                CreatedByUserId = createdByUserId,
            },
            transaction,
            cancellationToken: cancellationToken));

        // ── Branches ──
        var branchSort = 0;
        foreach (var b in response.Branches)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunBranch
                    (WideRunBranchId, WideRunResultId, WideBranchId, ParentWideBranchId, LevelNumber, BranchCode,
                     DisplayName, Interpretation, CapabilityCode, SearchText, GroundingStatusCode, EvidenceCount,
                     Confidence, ContinueNarrowing, StopReason, IsEliminated, EliminationReason, SortOrder,
                     BranchStateCode, SemanticTypeCode, BranchRoleCode, InterpretationPrior, EvidenceSupport,
                     PoloxiConfidence, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @WideBranchId, @ParentWideBranchId, @LevelNumber, @BranchCode,
                     @DisplayName, @Interpretation, @CapabilityCode, @SearchText, @GroundingStatusCode, @EvidenceCount,
                     @Confidence, @ContinueNarrowing, @StopReason, @IsEliminated, @EliminationReason, @SortOrder,
                     @BranchStateCode, @SemanticTypeCode, @BranchRoleCode, @InterpretationPrior, @EvidenceSupport,
                     @PoloxiConfidence, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, b.WideBranchId, b.ParentWideBranchId, b.LevelNumber,
                    b.BranchCode, b.DisplayName, b.Interpretation, b.CapabilityCode, b.SearchText, b.GroundingStatusCode,
                    b.EvidenceCount, b.Confidence, b.ContinueNarrowing, b.StopReason, b.IsEliminated, b.EliminationReason,
                    SortOrder = branchSort++, b.BranchStateCode, b.SemanticTypeCode, b.BranchRoleCode, b.InterpretationPrior,
                    b.EvidenceSupport, b.PoloxiConfidence, request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── Evidence ──
        var evidenceSort = 0;
        foreach (var e in response.Evidence)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunEvidence
                    (WideRunEvidenceId, WideRunResultId, HierarchyBranchId, SearchDocumentId, EntityTypeCode, EntityId,
                     ModuleCode, Title, Excerpt, NavigationRoute, RelevanceScore, RankNumber, MatchedBranchesJson,
                     SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @HierarchyBranchId, @SearchDocumentId, @EntityTypeCode, @EntityId,
                     @ModuleCode, @Title, @Excerpt, @NavigationRoute, @RelevanceScore, @RankNumber, @MatchedBranchesJson,
                     @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, e.HierarchyBranchId, e.SearchDocumentId, e.EntityTypeCode,
                    e.EntityId, e.ModuleCode, e.Title, e.Excerpt, e.NavigationRoute, e.RelevanceScore, e.RankNumber,
                    MatchedBranchesJson = JsonSerializer.Serialize(e.MatchedBranches), SortOrder = evidenceSort++,
                    request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── Candidates (+ branch scores) ──
        var candidateSort = 0;
        foreach (var c in response.Candidates)
        {
            var candidateRowId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunCandidate
                    (WideRunCandidateId, WideRunResultId, WideCandidateId, RankNumber, DisplayName, Detail,
                     CompositeScore, EvidenceCoverage, IsConstraintViolation, QualityScore, EvidenceConfidence,
                     AdmissionModeCode, SupportTierCode, InterpretiveSupportCount, EvidenceHostSupportCount,
                     TotalSupportCount, EstimatedResolutionLabel, EstimatedCostBand, SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @WideCandidateId, @RankNumber, @DisplayName, @Detail,
                     @CompositeScore, @EvidenceCoverage, @IsConstraintViolation, @QualityScore, @EvidenceConfidence,
                     @AdmissionModeCode, @SupportTierCode, @InterpretiveSupportCount, @EvidenceHostSupportCount,
                     @TotalSupportCount, @EstimatedResolutionLabel, @EstimatedCostBand, @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = candidateRowId, ResultId = resultId, c.WideCandidateId, c.RankNumber, c.DisplayName, c.Detail,
                    c.CompositeScore, c.EvidenceCoverage, c.IsConstraintViolation, c.QualityScore, c.EvidenceConfidence,
                    c.AdmissionModeCode, c.SupportTierCode, c.InterpretiveSupportCount, c.EvidenceHostSupportCount,
                    c.TotalSupportCount, c.EstimatedResolutionLabel, c.EstimatedCostBand, SortOrder = candidateSort++,
                    request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));

            var scoreSort = 0;
            foreach (var s in c.BranchScores)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_WideRunCandidateBranchScore
                        (WideRunCandidateBranchScoreId, WideRunCandidateId, BranchDisplayName, EvidenceScore,
                         DirectScore, ChildScoresJson, SortOrder, TenantId, CreatedByUserId)
                    VALUES
                        (@Id, @CandidateId, @BranchDisplayName, @EvidenceScore,
                         @DirectScore, @ChildScoresJson, @SortOrder, @TenantId, @CreatedByUserId);
                    """,
                    new
                    {
                        Id = Guid.NewGuid(), CandidateId = candidateRowId, s.BranchDisplayName, s.EvidenceScore,
                        s.DirectScore, ChildScoresJson = JsonSerializer.Serialize(s.ChildScores), SortOrder = scoreSort++,
                        request.TenantId, CreatedByUserId = createdByUserId,
                    },
                    transaction, cancellationToken: cancellationToken));
            }
        }

        // ── Interpretive results (+ items) ──
        var interpSort = 0;
        foreach (var ir in response.InterpretiveResults)
        {
            var interpRowId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunInterpretiveResult
                    (WideRunInterpretiveResultId, WideRunResultId, BranchDisplayName, Interpretation, Confidence,
                     DataVolatility, IsExternallyGrounded, BranchStateCode, LevelNumber, ScoreSource, SortOrder,
                     TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @BranchDisplayName, @Interpretation, @Confidence,
                     @DataVolatility, @IsExternallyGrounded, @BranchStateCode, @LevelNumber, @ScoreSource, @SortOrder,
                     @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = interpRowId, ResultId = resultId, ir.BranchDisplayName, ir.Interpretation, ir.Confidence,
                    ir.DataVolatility, ir.IsExternallyGrounded, ir.BranchStateCode, ir.LevelNumber, ir.ScoreSource,
                    SortOrder = interpSort++, request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));

            var itemSort = 0;
            foreach (var item in ir.Items)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO POLOXI.Legal_WideRunInterpretiveResultItem
                        (WideRunInterpretiveResultItemId, WideRunInterpretiveResultId, RankNumber, Name, Detail,
                         Score, SortOrder, TenantId, CreatedByUserId)
                    VALUES
                        (@Id, @ParentId, @RankNumber, @Name, @Detail, @Score, @SortOrder, @TenantId, @CreatedByUserId);
                    """,
                    new
                    {
                        Id = Guid.NewGuid(), ParentId = interpRowId, item.RankNumber, item.Name, item.Detail, item.Score,
                        SortOrder = itemSort++, request.TenantId, CreatedByUserId = createdByUserId,
                    },
                    transaction, cancellationToken: cancellationToken));
            }
        }

        // ── LLM raw items ──
        var rawSort = 0;
        foreach (var r in response.LlmRawItems)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunLlmRawItem
                    (WideRunLlmRawItemId, WideRunResultId, RankNumber, Name, Detail, Score, SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @RankNumber, @Name, @Detail, @Score, @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, r.RankNumber, r.Name, r.Detail, r.Score,
                    SortOrder = rawSort++, request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── External references ──
        var extSort = 0;
        foreach (var x in response.ExternalReferences)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunExternalReference
                    (WideRunExternalReferenceId, WideRunResultId, Title, Url, Source, Summary, BranchDisplayName,
                     SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @Title, @Url, @Source, @Summary, @BranchDisplayName, @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, x.Title, x.Url, x.Source, x.Summary, x.BranchDisplayName,
                    SortOrder = extSort++, request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── Proposed legal authorities ──
        var authSort = 0;
        foreach (var a in response.ProposedLegalAuthorities)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunProposedAuthority
                    (WideRunProposedAuthorityId, WideRunResultId, Name, Kind, Relevance, VerificationStatus,
                     SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @Name, @Kind, @Relevance, @VerificationStatus, @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, a.Name, a.Kind, a.Relevance, a.VerificationStatus,
                    SortOrder = authSort++, request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── Suggested actions ──
        var actionSort = 0;
        foreach (var sa in response.SuggestedActions)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunSuggestedAction
                    (WideRunSuggestedActionId, WideRunResultId, DisplayName, NavigationRoute, Rationale,
                     SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @DisplayName, @NavigationRoute, @Rationale, @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, sa.DisplayName, sa.NavigationRoute, sa.Rationale,
                    SortOrder = actionSort++, request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── Stage timings ──
        var timingSort = 0;
        foreach (var t in response.StageTimings)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunStageTiming
                    (WideRunStageTimingId, WideRunResultId, StageCode, DurationMilliseconds, InputTokenCount,
                     OutputTokenCount, ModelCode, SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @StageCode, @DurationMilliseconds, @InputTokenCount,
                     @OutputTokenCount, @ModelCode, @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, t.StageCode, t.DurationMilliseconds, t.InputTokenCount,
                    t.OutputTokenCount, t.ModelCode, SortOrder = timingSort++, request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── Information rounds (targets as JSON) ──
        var roundSort = 0;
        foreach (var ro in response.InformationRounds)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunInformationRound
                    (WideRunInformationRoundId, WideRunResultId, RoundNumber, EntropyBefore, NormalizedEntropyBefore,
                     EntropyAfter, NormalizedEntropyAfter, ActualInformationGain, RawEntropyDelta, TargetsJson,
                     SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @RoundNumber, @EntropyBefore, @NormalizedEntropyBefore,
                     @EntropyAfter, @NormalizedEntropyAfter, @ActualInformationGain, @RawEntropyDelta, @TargetsJson,
                     @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, ro.RoundNumber, ro.EntropyBefore, ro.NormalizedEntropyBefore,
                    ro.EntropyAfter, ro.NormalizedEntropyAfter, ro.ActualInformationGain, ro.RawEntropyDelta,
                    TargetsJson = JsonSerializer.Serialize(ro.Targets), SortOrder = roundSort++, request.TenantId,
                    CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── Narrowing iterations (transitions as JSON) ──
        var narrowSort = 0;
        foreach (var n in response.NarrowingIterations)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunNarrowingIteration
                    (WideRunNarrowingIterationId, WideRunResultId, RoundNumber, TrendCode, ActiveBranchCountBefore,
                     ActiveBranchCountAfter, CandidateCountBefore, CandidateCountAfter, NormalizedEntropyBefore,
                     NormalizedEntropyAfter, ActualInformationGain, AdmittedCandidateCount, DiscoveredNotAdmittedCount,
                     ResolvedBranchCount, ReopenedBranchCount, TransitionsJson, SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @RoundNumber, @TrendCode, @ActiveBranchCountBefore,
                     @ActiveBranchCountAfter, @CandidateCountBefore, @CandidateCountAfter, @NormalizedEntropyBefore,
                     @NormalizedEntropyAfter, @ActualInformationGain, @AdmittedCandidateCount, @DiscoveredNotAdmittedCount,
                     @ResolvedBranchCount, @ReopenedBranchCount, @TransitionsJson, @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, n.RoundNumber, n.TrendCode, n.ActiveBranchCountBefore,
                    n.ActiveBranchCountAfter, n.CandidateCountBefore, n.CandidateCountAfter, n.NormalizedEntropyBefore,
                    n.NormalizedEntropyAfter, n.ActualInformationGain, n.AdmittedCandidateCount, n.DiscoveredNotAdmittedCount,
                    n.ResolvedBranchCount, n.ReopenedBranchCount, TransitionsJson = JsonSerializer.Serialize(n.Transitions),
                    SortOrder = narrowSort++, request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }

        // ── Ambiguity groups ──
        var groupSort = 0;
        foreach (var g in response.AmbiguityGroups)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_WideRunAmbiguityGroup
                    (WideRunAmbiguityGroupId, WideRunResultId, RootWideBranchId, GroupCode, DisplayName, Interpretation,
                     Confidence, SafetyRiskCode, AnswerKindCode, CandidateKindCode, SortOrder, TenantId, CreatedByUserId)
                VALUES
                    (@Id, @ResultId, @RootWideBranchId, @GroupCode, @DisplayName, @Interpretation,
                     @Confidence, @SafetyRiskCode, @AnswerKindCode, @CandidateKindCode, @SortOrder, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    Id = Guid.NewGuid(), ResultId = resultId, g.RootWideBranchId, g.GroupCode, g.DisplayName, g.Interpretation,
                    g.Confidence, g.SafetyRiskCode, g.AnswerKindCode, g.CandidateKindCode, SortOrder = groupSort++,
                    request.TenantId, CreatedByUserId = createdByUserId,
                },
                transaction, cancellationToken: cancellationToken));
        }
    }

    // ── Matter-level current-assessment pointer for a concluded, successful Wide execution. ──
    private static async Task UpsertWideCurrentAssessmentAsync(
        IDbConnection connection, IDbTransaction transaction,
        WideSearchRequest request, WideSearchResponse response,
        Guid matterId, Guid runId, string runLabel, string executionStatus,
        CancellationToken cancellationToken)
    {
        var isConcluded = string.Equals(executionStatus, "Succeeded", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(response.FinalAnswer);
        if (!isConcluded)
            return;

        var createdByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId);
        var publishedUtc = DateTime.UtcNow;

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_AnalysisRunCurrentAssessment
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE TenantId = @TenantId AND MatterId = @MatterId AND DecisionId IS NULL AND IsDeleted = 0;
            """,
            new { request.TenantId, MatterId = matterId, ModifiedByUserId = createdByUserId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_AnalysisRunCurrentAssessment
                (CurrentAssessmentId, MatterId, DecisionId, AnalysisRunId, AssessmentLabel,
                 ReviewStateText, FreshnessStatusCode, FreshnessNote, PublishedUtc, TenantId, CreatedByUserId)
            VALUES
                (@CurrentAssessmentId, @MatterId, NULL, @AnalysisRunId, @AssessmentLabel,
                 @ReviewStateText, @FreshnessStatusCode, @FreshnessNote, @PublishedUtc, @TenantId, @CreatedByUserId);
            """,
            new
            {
                CurrentAssessmentId = Guid.NewGuid(),
                MatterId = matterId,
                AnalysisRunId = runId,
                AssessmentLabel = $"A-{runLabel.TrimStart('R', '-')}",
                ReviewStateText = (string?)null,
                FreshnessStatusCode = "Current",
                FreshnessNote = (string?)null,
                PublishedUtc = publishedUtc,
                request.TenantId,
                CreatedByUserId = createdByUserId,
            },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_AnalysisRun
               SET PublicationStatusCode = N'Published', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE AnalysisRunId = @AnalysisRunId AND TenantId = @TenantId;
            """,
            new { AnalysisRunId = runId, request.TenantId, ModifiedByUserId = createdByUserId },
            transaction,
            cancellationToken: cancellationToken));
    }

    // ── Wide response → execution steps. Each step's status AND description reflect the actual response
    // content of THIS run (branch counts per level, admitted evidence, candidates, termination reason), so
    // every ledger entry carries its own recorded facts instead of a shared generic sentence. ──
    private static IReadOnlyList<DerivedStep> DeriveWideSteps(WideSearchResponse response)
    {
        var hasBranches = response.Branches.Count > 0;
        var hasEvidence = response.Evidence.Count > 0;
        var hasAnswer = !string.IsNullOrWhiteSpace(response.FinalAnswer);
        var hasActions = response.SuggestedActions.Count > 0;

        var levelCounts = response.Branches
            .GroupBy(b => b.LevelNumber)
            .OrderBy(g => g.Key)
            .Select(g => $"L{g.Key}={g.Count()}")
            .ToArray();
        var levelSummary = levelCounts.Length == 0 ? string.Empty : $" ({string.Join(", ", levelCounts)})";

        var intakeText = Truncate(
            $"Query and decision context registered. Query: \"{(response.Query ?? string.Empty).Trim()}\".", 600);

        var discoveryText = hasBranches
            ? Truncate($"{response.Branches.Count} branch(es) proposed across {levelCounts.Length} level(s){levelSummary}; depth reached {response.DepthReached}; {response.LlmCallCount} LLM call(s).", 600)
            : "No candidate branches proposed.";

        var evidenceText = hasEvidence
            ? Truncate($"{response.Evidence.Count} evidence item(s) admitted; {response.ExternalKnowledge.Count} external snippet(s) retrieved; {response.InterpretiveResults.Count} interpretive result set(s).", 600)
            : Truncate($"No evidence admitted; {response.ExternalKnowledge.Count} external snippet(s) retrieved; {response.InterpretiveResults.Count} interpretive result set(s).", 600);

        var winner = response.Candidates
            .OrderByDescending(c => c.CompositeScore)
            .FirstOrDefault();
        var evaluationText = hasAnswer
            ? Truncate($"Terminated {response.TerminationReasonCode}; verification {response.AnswerVerificationCode}; confidence {response.FinalConfidence:P0}; {response.Candidates.Count} candidate(s)" +
                       (winner is null ? "." : $"; leading: {winner.DisplayName}."), 600)
            : Truncate($"No terminal answer reached (terminated {response.TerminationReasonCode}).", 600);

        var actionsText = hasActions
            ? Truncate($"{response.SuggestedActions.Count} suggested action(s): {string.Join("; ", response.SuggestedActions.Select(a => a.DisplayName))}.", 600)
            : "No suggested actions.";

        return
        [
            new DerivedStep("INFORMATION_INTAKE", "Information intake", intakeText, 1, Status(true)),
            new DerivedStep("DECISION_DISCOVERY", "Decision discovery", discoveryText, 2, Status(hasBranches)),
            new DerivedStep("EVIDENCE_VERIFICATION", "Evidence verification", evidenceText, 3, Status(hasEvidence)),
            new DerivedStep("POLOXI_EVALUATION", "POLOXI evaluation", evaluationText, 4, Status(hasAnswer)),
            new DerivedStep("NEXT_BEST_ACTION", "Next best action", actionsText, 5, Status(hasActions)),
        ];
    }

    // ── Wide response → per-step attempts. Narrowing iterations are recorded under DECISION_DISCOVERY and
    // information rounds under EVIDENCE_VERIFICATION so the ledger detail shows this run's actual rounds. ──
    private static async Task InsertWideStepAttemptsAsync(
        IDbConnection connection, IDbTransaction transaction,
        WideSearchRequest request, WideSearchResponse response,
        string stepCode, Guid stepId, CancellationToken cancellationToken)
    {
        var createdByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId);
        var attempts = new List<(int Number, string Status, string Summary)>();

        if (string.Equals(stepCode, "DECISION_DISCOVERY", StringComparison.Ordinal))
        {
            foreach (var iteration in response.NarrowingIterations.OrderBy(i => i.RoundNumber))
            {
                attempts.Add((
                    iteration.RoundNumber,
                    "Completed",
                    Truncate($"Narrowing round {iteration.RoundNumber} — {iteration.TrendCode}; active branches {iteration.ActiveBranchCountBefore}→{iteration.ActiveBranchCountAfter}; candidates {iteration.CandidateCountBefore}→{iteration.CandidateCountAfter}; entropy {iteration.NormalizedEntropyBefore:0.00}→{(iteration.NormalizedEntropyAfter is { } ea ? ea.ToString("0.00") : "n/a")}", 400)));
            }
        }
        else if (string.Equals(stepCode, "EVIDENCE_VERIFICATION", StringComparison.Ordinal))
        {
            foreach (var round in response.InformationRounds.OrderBy(r => r.RoundNumber))
            {
                attempts.Add((
                    round.RoundNumber,
                    "Completed",
                    Truncate($"Information round {round.RoundNumber} — {round.Targets.Count} target(s); entropy {round.NormalizedEntropyBefore:0.00}→{(round.NormalizedEntropyAfter is { } ea ? ea.ToString("0.00") : "n/a")}; gain {(round.ActualInformationGain is { } g ? g.ToString("0.000") : "n/a")}", 400)));
            }
        }

        foreach (var attempt in attempts)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_AnalysisRunStepAttempt
                    (AnalysisRunStepAttemptId, AnalysisRunStepId, AttemptNumber, StatusCode, Summary, TenantId, CreatedByUserId)
                VALUES
                    (@AnalysisRunStepAttemptId, @AnalysisRunStepId, @AttemptNumber, @StatusCode, @Summary, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    AnalysisRunStepAttemptId = Guid.NewGuid(),
                    AnalysisRunStepId = stepId,
                    AttemptNumber = attempt.Number,
                    StatusCode = attempt.Status,
                    Summary = attempt.Summary,
                    request.TenantId,
                    CreatedByUserId = createdByUserId,
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static string MapWideExecutionStatus(WideSearchResponse response)
    {
        if (string.Equals(response.StatusCode, "FAILED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.TerminationReasonCode, "ERROR", StringComparison.OrdinalIgnoreCase))
            return "Failed";

        if (string.Equals(response.StatusCode, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            return "Cancelled";

        return "Succeeded";
    }

    private static string BuildWideTitle(WideSearchRequest request, WideSearchResponse response)
    {
        var query = (response.Query ?? request.Query ?? "Matter analysis").Trim();
        return query.Length <= 120 ? query : query[..117] + "...";
    }

    private static string? BuildWideBanner(string executionStatus)
        => string.Equals(executionStatus, "Failed", StringComparison.Ordinal)
            ? "Execution did not complete. The current assessment was preserved."
            : null;
    // so opening a historical run reproduces its recorded inputs even after the matter/prompt/Domain
    // Pack/configuration later change. Write-once: one row per run, never updated in place. ──
    private static async Task InsertInputSnapshotAsync(
        IDbConnection connection, IDbTransaction transaction,
        DecisionSearchRequest request, Guid matterId, Guid runId, string snapshotLabel,
        DecisionContractDto? contract,
        CancellationToken cancellationToken)
    {
        var projection = new
        {
            request.Query,
            request.MaximumResults,
            request.CorrelationId,
            request.UsePoloxiEngine,
            Mode = request.Mode.ToString(),
            request.ModelCode,
            request.ContextCode,
            DiscoveryMode = request.DiscoveryMode.ToString(),
            request.DomainPackCode,
            request.Jurisdiction,
            request.GoverningLaw,
            request.CourtOrForum,
            request.Posture,
            request.MotionTarget,
            request.ClarificationAnswer,
            request.CounterfactualAssumption,
            // Full DB-backed Decision Contract (Draft) in effect at execution time. Preserves every
            // Draft field and child collection so the run is fully reproducible. Null when no contract.
            DecisionContract = contract,
        };

        var contractJson = JsonSerializer.Serialize(projection);
        var createdByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_AnalysisRunInputSnapshot
                (InputSnapshotId, AnalysisRunId, MatterId, SnapshotLabel, Query, ContextCode, ModeCode, ModelCode,
                 DomainPackCode, DiscoveryModeCode, Jurisdiction, GoverningLaw, CourtOrForum, Posture, MotionTarget,
                 CounterfactualAssumption, ClarificationAnswer, UsedDependencyGraph, ContractJson,
                 TenantId, CreatedByUserId)
            VALUES
                (@InputSnapshotId, @AnalysisRunId, @MatterId, @SnapshotLabel, @Query, @ContextCode, @ModeCode, @ModelCode,
                 @DomainPackCode, @DiscoveryModeCode, @Jurisdiction, @GoverningLaw, @CourtOrForum, @Posture, @MotionTarget,
                 @CounterfactualAssumption, @ClarificationAnswer, @UsedDependencyGraph, @ContractJson,
                 @TenantId, @CreatedByUserId);
            """,
            new
            {
                InputSnapshotId = Guid.NewGuid(),
                AnalysisRunId = runId,
                MatterId = matterId,
                SnapshotLabel = snapshotLabel,
                request.Query,
                request.ContextCode,
                ModeCode = request.Mode.ToString(),
                request.ModelCode,
                request.DomainPackCode,
                DiscoveryModeCode = request.DiscoveryMode.ToString(),
                request.Jurisdiction,
                request.GoverningLaw,
                request.CourtOrForum,
                request.Posture,
                request.MotionTarget,
                request.CounterfactualAssumption,
                request.ClarificationAnswer,
                UsedDependencyGraph = (bool?)request.UsePoloxiEngine,
                ContractJson = contractJson,
                request.TenantId,
                CreatedByUserId = createdByUserId,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    // ── Matter-level current-assessment pointer. Published only for a concluded, successful decision
    // (a final answer with no pending clarification). One pointer per matter (DecisionId NULL), matching
    // the filtered unique index; the newest successful run supersedes the prior pointer. ──
    private static async Task UpsertCurrentAssessmentAsync(
        IDbConnection connection, IDbTransaction transaction,
        DecisionSearchRequest request, DecisionSearchResponse response,
        Guid matterId, Guid runId, string runLabel, string executionStatus,
        CancellationToken cancellationToken)
    {
        var isConcluded = string.Equals(executionStatus, "Succeeded", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(response.FinalAnswer)
            && string.IsNullOrWhiteSpace(response.ClarificationQuestion);
        if (!isConcluded)
            return;

        var createdByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId);
        var publishedUtc = DateTime.UtcNow;

        // Supersede any existing matter-level pointer, then insert the new one. Kept inside the run
        // transaction so the pointer never references an uncommitted run.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_AnalysisRunCurrentAssessment
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE TenantId = @TenantId AND MatterId = @MatterId AND DecisionId IS NULL AND IsDeleted = 0;
            """,
            new { request.TenantId, MatterId = matterId, ModifiedByUserId = createdByUserId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO POLOXI.Legal_AnalysisRunCurrentAssessment
                (CurrentAssessmentId, MatterId, DecisionId, AnalysisRunId, AssessmentLabel,
                 ReviewStateText, FreshnessStatusCode, FreshnessNote, PublishedUtc, TenantId, CreatedByUserId)
            VALUES
                (@CurrentAssessmentId, @MatterId, NULL, @AnalysisRunId, @AssessmentLabel,
                 @ReviewStateText, @FreshnessStatusCode, @FreshnessNote, @PublishedUtc, @TenantId, @CreatedByUserId);
            """,
            new
            {
                CurrentAssessmentId = Guid.NewGuid(),
                MatterId = matterId,
                AnalysisRunId = runId,
                AssessmentLabel = $"A-{runLabel.TrimStart('R', '-')}",
                ReviewStateText = (string?)null,
                FreshnessStatusCode = "Current",
                FreshnessNote = (string?)null,
                PublishedUtc = publishedUtc,
                request.TenantId,
                CreatedByUserId = createdByUserId,
            },
            transaction,
            cancellationToken: cancellationToken));

        // Reflect that this run is now the published current assessment for the matter.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_AnalysisRun
               SET PublicationStatusCode = N'Published', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE AnalysisRunId = @AnalysisRunId AND TenantId = @TenantId;
            """,
            new { AnalysisRunId = runId, request.TenantId, ModifiedByUserId = createdByUserId },
            transaction,
            cancellationToken: cancellationToken));
    }

    // ── Step attempts from actual retries. The bounded research loop is the real retry surface of the
    // pipeline: each executed round is one verification attempt with its own outcome. We attribute these
    // to the EVIDENCE_VERIFICATION step so retries/outcomes are preserved (no synthesized attempts). ──
    private static async Task InsertStepAttemptsAsync(
        IDbConnection connection, IDbTransaction transaction,
        DecisionSearchRequest request, DecisionSearchResponse response,
        string stepCode, Guid stepId, CancellationToken cancellationToken)
    {
        if (!string.Equals(stepCode, "EVIDENCE_VERIFICATION", StringComparison.Ordinal))
            return;

        var summary = response.ResearchSummary;
        if (summary is null || summary.Rounds.Count == 0)
            return;

        var createdByUserId = (Guid?)(request.UserId == Guid.Empty ? null : request.UserId);
        var orderedRounds = summary.Rounds.OrderBy(r => r.RoundNumber).ToList();

        foreach (var round in orderedRounds)
        {
            var isFailedRound = summary.Failure is { } failure && failure.RoundNumber == round.RoundNumber;
            var statusCode = isFailedRound ? "Failed"
                : round.WinnerChanged ? "Succeeded"
                : "Completed";
            var attemptSummary = BuildAttemptSummary(round, isFailedRound ? summary.Failure : null);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO POLOXI.Legal_AnalysisRunStepAttempt
                    (AnalysisRunStepAttemptId, AnalysisRunStepId, AttemptNumber, StatusCode, Summary, TenantId, CreatedByUserId)
                VALUES
                    (@AnalysisRunStepAttemptId, @AnalysisRunStepId, @AttemptNumber, @StatusCode, @Summary, @TenantId, @CreatedByUserId);
                """,
                new
                {
                    AnalysisRunStepAttemptId = Guid.NewGuid(),
                    AnalysisRunStepId = stepId,
                    AttemptNumber = round.RoundNumber,
                    StatusCode = statusCode,
                    Summary = attemptSummary,
                    request.TenantId,
                    CreatedByUserId = createdByUserId,
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static string? BuildAttemptSummary(
        Legal.Application.Features.Intelligence.Decision.DecisionResearchRoundDto round,
        Legal.Application.Features.Intelligence.Decision.DecisionResearchFailureDto? failure)
    {
        if (failure is not null)
        {
            var reason = $"{failure.Stage}: {failure.Reason}".Trim();
            return Truncate(reason, 400);
        }

        var label = string.IsNullOrWhiteSpace(round.TargetBranchLabel) ? "frontier" : round.TargetBranchLabel;
        var text = $"{label} — {round.SourcesRetrieved} source(s), {round.EdgeVerificationStatus}" +
            (round.WinnerChanged ? ", winner changed" : string.Empty);
        return Truncate(text, 400);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..(max - 3)] + "...";

    // ── Response → execution steps. Each step's status reflects actual response content. ──
    private static IReadOnlyList<DerivedStep> DeriveSteps(DecisionSearchResponse response)
    {
        var hasContract = response.ContractCompleteness > 0;
        var hasCandidates = response.Candidates.Count > 0;
        var hasBranches = response.Branches.Count > 0;
        var hasEvidence = response.Evidence.Count > 0 || response.EvidenceVerifications.Count > 0;
        var hasIntegration = response.UsedDependencyGraph
            || response.GraphNodes.Count > 0
            || response.VerifiedSignals.Count > 0
            || response.Dependencies.Count > 0;
        var hasEvaluation = response.WinnerCandidateId is not null
            || !string.IsNullOrWhiteSpace(response.TerminalStateCode);

        return
        [
            new DerivedStep(
                "INFORMATION_INTAKE", "Information intake",
                "Query and decision contract registered.",
                1, Status(hasContract || hasCandidates)),
            new DerivedStep(
                "DECISION_DISCOVERY", "Decision discovery",
                "Candidate outcomes and hierarchy proposed.",
                2, Status(hasCandidates || hasBranches)),
            new DerivedStep(
                "EVIDENCE_VERIFICATION", "Evidence verification",
                hasEvidence ? "Sources evaluated and anchored." : "No evidence evaluated.",
                3, Status(hasEvidence)),
            new DerivedStep(
                "LPI_INTEGRATION", "LPI integration",
                hasIntegration ? "Dependency-aware signals integrated with lineage." : "Not executed.",
                4, Status(hasIntegration)),
            new DerivedStep(
                "POLOXI_EVALUATION", "POLOXI evaluation",
                hasEvaluation ? "Candidate competition evaluated to a terminal state." : "No terminal state reached.",
                5, Status(hasEvaluation)),
        ];
    }

    private static (string StatusCode, string StatusLabel) Status(bool completed)
        => completed ? ("Completed", "Completed") : ("Pending", "Not started");

    private static string MapExecutionStatus(DecisionSearchResponse response)
    {
        if (string.Equals(response.StatusCode, "FAILED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.TerminationReasonCode, "ERROR", StringComparison.OrdinalIgnoreCase))
            return "Failed";

        if (string.Equals(response.StatusCode, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            return "Cancelled";

        return "Succeeded";
    }

    private static string BuildTitle(DecisionSearchRequest request, DecisionSearchResponse response)
    {
        if (request.CounterfactualAssumption is not null)
            return "Counterfactual analysis";

        var query = (response.Query ?? request.Query ?? "Matter analysis").Trim();
        return query.Length <= 120 ? query : query[..117] + "...";
    }

    private static string? BuildBanner(string executionStatus, DecisionSearchResponse response)
        => string.Equals(executionStatus, "Failed", StringComparison.Ordinal)
            ? "Execution did not complete. The current assessment was preserved."
            : null;

    private sealed record DerivedStep(
        string StepCode, string DisplayName, string Description, int Sequence,
        (string StatusCode, string StatusLabel) Outcome)
    {
        public string StatusCode => Outcome.StatusCode;
        public string StatusLabel => Outcome.StatusLabel;
    }
}
