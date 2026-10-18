using Dapper;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.RunManagement;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper repository for the Run & Assessment Management execution ledger. All reads are tenant-scoped
// and restricted to non-deleted rows (DB is the source of truth; no synthesized data).
public sealed class AnalysisRunLedgerRepository(ISqlConnectionFactory connectionFactory) : IAnalysisRunLedgerRepository
{
    public async Task<IReadOnlyList<AnalysisRunDto>> GetRunsForMatterAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var runRows = (await connection.QueryAsync<RunRow>(new CommandDefinition(
            """
            SELECT AnalysisRunId, RunLabel, Title, RunTypeCode, TriggerTypeCode, TriggerText,
                   ExecutionStatusCode, PublicationStatusCode, PackLabel, InputSnapshotLabel,
                   RequestedUtc, StartedUtc, CompletedUtc, DurationSeconds, ExecutionBanner, FooterNote
            FROM POLOXI.Legal_AnalysisRun
            WHERE TenantId = @TenantId AND MatterId = @MatterId AND IsDeleted = 0
            ORDER BY RequestedUtc DESC, CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, MatterId = matterId },
            cancellationToken: cancellationToken))).ToList();

        if (runRows.Count == 0)
            return [];

        var runIds = runRows.Select(r => r.AnalysisRunId).ToArray();

        var stepRows = (await connection.QueryAsync<StepRow>(new CommandDefinition(
            """
            SELECT AnalysisRunStepId, AnalysisRunId, StepCode, DisplayName, Description, Sequence,
                   StatusCode, StatusLabel
            FROM POLOXI.Legal_AnalysisRunStep
            WHERE TenantId = @TenantId AND AnalysisRunId IN @RunIds AND IsDeleted = 0
            ORDER BY AnalysisRunId, Sequence;
            """,
            new { TenantId = tenantId, RunIds = runIds },
            cancellationToken: cancellationToken))).ToList();

        var stepIds = stepRows.Select(s => s.AnalysisRunStepId).ToArray();

        var attemptRows = stepIds.Length == 0
            ? new List<AttemptRow>()
            : (await connection.QueryAsync<AttemptRow>(new CommandDefinition(
                """
                SELECT AnalysisRunStepId, AttemptNumber, StatusCode, Summary, StartedUtc, CompletedUtc
                FROM POLOXI.Legal_AnalysisRunStepAttempt
                WHERE TenantId = @TenantId AND AnalysisRunStepId IN @StepIds AND IsDeleted = 0
                ORDER BY AnalysisRunStepId, AttemptNumber;
                """,
                new { TenantId = tenantId, StepIds = stepIds },
                cancellationToken: cancellationToken))).ToList();

        var attemptsByStep = attemptRows
            .GroupBy(a => a.AnalysisRunStepId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AnalysisRunAttemptDto>)g
                    .Select(a => new AnalysisRunAttemptDto(a.AttemptNumber, a.StatusCode, a.Summary, a.StartedUtc, a.CompletedUtc))
                    .ToList());

        var stepsByRun = stepRows
            .GroupBy(s => s.AnalysisRunId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<AnalysisRunStepDto>)g
                    .Select(s => new AnalysisRunStepDto(
                        s.AnalysisRunStepId, s.StepCode, s.DisplayName, s.Description, s.Sequence,
                        s.StatusCode, s.StatusLabel,
                        attemptsByStep.TryGetValue(s.AnalysisRunStepId, out var at) ? at : []))
                    .ToList());

        var snapshotRows = (await connection.QueryAsync<SnapshotRow>(new CommandDefinition(
            """
            SELECT InputSnapshotId, AnalysisRunId, SnapshotLabel, Query, ContextCode, ModeCode, ModelCode,
                   DomainPackCode, DiscoveryModeCode, Jurisdiction, GoverningLaw, CourtOrForum, Posture,
                   MotionTarget, CounterfactualAssumption, ClarificationAnswer, UsedDependencyGraph,
                   ContractJson, CapturedUtc
            FROM POLOXI.Legal_AnalysisRunInputSnapshot
            WHERE TenantId = @TenantId AND AnalysisRunId IN @RunIds AND IsDeleted = 0;
            """,
            new { TenantId = tenantId, RunIds = runIds },
            cancellationToken: cancellationToken))).ToList();

        var snapshotByRun = snapshotRows.ToDictionary(
            s => s.AnalysisRunId,
            s => new AnalysisRunInputSnapshotDto(
                s.InputSnapshotId, s.AnalysisRunId, s.SnapshotLabel, s.Query, s.ContextCode, s.ModeCode,
                s.ModelCode, s.DomainPackCode, s.DiscoveryModeCode, s.Jurisdiction, s.GoverningLaw,
                s.CourtOrForum, s.Posture, s.MotionTarget, s.CounterfactualAssumption, s.ClarificationAnswer,
                s.UsedDependencyGraph, s.ContractJson, s.CapturedUtc));

        return runRows
            .Select(r => new AnalysisRunDto(
                r.AnalysisRunId, r.RunLabel, r.Title, r.RunTypeCode, r.TriggerTypeCode, r.TriggerText,
                r.ExecutionStatusCode, r.PublicationStatusCode, r.PackLabel, r.InputSnapshotLabel,
                r.RequestedUtc, r.StartedUtc, r.CompletedUtc, r.DurationSeconds, r.ExecutionBanner, r.FooterNote,
                stepsByRun.TryGetValue(r.AnalysisRunId, out var steps) ? steps : [])
            {
                InputSnapshot = snapshotByRun.TryGetValue(r.AnalysisRunId, out var snap) ? snap : null,
            })
            .ToList();
    }

    public async Task<CurrentAssessmentDto?> GetCurrentAssessmentAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var row = await connection.QueryFirstOrDefaultAsync<CurrentRow>(new CommandDefinition(
            """
            SELECT TOP(1) ca.CurrentAssessmentId, ca.AssessmentLabel, ca.AnalysisRunId,
                   run.RunLabel, ca.ReviewStateText, ca.FreshnessStatusCode, ca.FreshnessNote, ca.PublishedUtc
            FROM POLOXI.Legal_AnalysisRunCurrentAssessment ca
            INNER JOIN POLOXI.Legal_AnalysisRun run ON run.AnalysisRunId = ca.AnalysisRunId AND run.IsDeleted = 0
            WHERE ca.TenantId = @TenantId AND ca.MatterId = @MatterId AND ca.IsDeleted = 0
            ORDER BY ca.PublishedUtc DESC;
            """,
            new { TenantId = tenantId, MatterId = matterId },
            cancellationToken: cancellationToken));

        return row is null
            ? null
            : new CurrentAssessmentDto(
                row.CurrentAssessmentId, row.AssessmentLabel, row.AnalysisRunId, row.RunLabel,
                row.ReviewStateText, row.FreshnessStatusCode, row.FreshnessNote, row.PublishedUtc);
    }

    public async Task<IReadOnlyDictionary<Guid, RunQualitySignals>> GetRunQualitySignalsAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Per-run execution quality metrics plus the aggregated candidate landscape. Candidate counts and
        // composite-score aggregates come first from the ledger's own lossless capture of the delivered
        // response (Legal_WideRunCandidate, keyed by WideRunResultId) - this is what the run actually
        // returned. Only when that capture is empty do we fall back to the pipeline-side Legal_WideCandidate
        // rows via the WideExecutionId bridge. All reads are tenant-scoped and restricted to non-deleted
        // rows; runs without a decision execution simply return no row.
        var rows = (await connection.QueryAsync<QualityRow>(new CommandDefinition(
            """
            SELECT r.AnalysisRunId,
                   r.StatusCode,
                   r.AnswerVerificationCode,
                   COALESCE(r.FinalConfidence, CAST(0 AS DECIMAL(9,4))) AS FinalConfidence,
                   COALESCE(r.DecisionConfidence, CAST(0 AS DECIMAL(9,4))) AS DecisionConfidence,
                   COALESCE(r.EvidenceCoverage, CAST(0 AS DECIMAL(9,4))) AS EvidenceCoverage,
                   COALESCE(r.DecisionEvidenceCoverage, CAST(0 AS DECIMAL(9,4))) AS DecisionEvidenceCoverage,
                   COALESCE(r.WinnerStability, CAST(0 AS DECIMAL(9,4))) AS WinnerStability,
                   COALESCE(r.TopKStability, CAST(0 AS DECIMAL(9,4))) AS TopKStability,
                   COALESCE(CASE WHEN ledger.CandidateCount > 0 THEN ledger.CandidateCount ELSE cand.CandidateCount END, 0) AS CandidateCount,
                   COALESCE(CASE WHEN ledger.CandidateCount > 0 THEN ledger.ViableCandidateCount ELSE cand.ViableCandidateCount END, 0) AS ViableCandidateCount,
                   COALESCE(CASE WHEN ledger.CandidateCount > 0 THEN ledger.TopCompositeScore ELSE cand.TopCompositeScore END, CAST(0 AS DECIMAL(9,4))) AS TopCompositeScore,
                   COALESCE(CASE WHEN ledger.CandidateCount > 0 THEN ledger.MeanViableCompositeScore ELSE cand.MeanViableCompositeScore END, CAST(0 AS DECIMAL(9,4))) AS MeanViableCompositeScore
            FROM POLOXI.Legal_WideRunResult r
            OUTER APPLY (
                SELECT COUNT(*) AS CandidateCount,
                       SUM(CASE WHEN rc.IsConstraintViolation = 0 THEN 1 ELSE 0 END) AS ViableCandidateCount,
                       MAX(rc.CompositeScore) AS TopCompositeScore,
                       AVG(CASE WHEN rc.IsConstraintViolation = 0 THEN rc.CompositeScore END) AS MeanViableCompositeScore
                FROM POLOXI.Legal_WideRunCandidate rc
                WHERE rc.WideRunResultId = r.WideRunResultId AND rc.TenantId = @TenantId AND rc.IsDeleted = 0
            ) ledger
            OUTER APPLY (
                SELECT COUNT(*) AS CandidateCount,
                       SUM(CASE WHEN c.IsConstraintViolation = 0 THEN 1 ELSE 0 END) AS ViableCandidateCount,
                       MAX(c.CompositeScore) AS TopCompositeScore,
                       AVG(CASE WHEN c.IsConstraintViolation = 0 THEN c.CompositeScore END) AS MeanViableCompositeScore
                FROM POLOXI.Legal_WideCandidate c
                WHERE c.WideExecutionId = r.WideExecutionId AND c.TenantId = @TenantId AND c.IsDeleted = 0
            ) cand
            WHERE r.TenantId = @TenantId AND r.MatterId = @MatterId AND r.IsDeleted = 0;
            """,
            new { TenantId = tenantId, MatterId = matterId },
            cancellationToken: cancellationToken))).ToList();

        return rows
            .GroupBy(r => r.AnalysisRunId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var r = g.First();
                    return new RunQualitySignals(
                        r.AnalysisRunId, r.StatusCode, r.AnswerVerificationCode,
                        r.FinalConfidence, r.DecisionConfidence, r.EvidenceCoverage,
                        r.DecisionEvidenceCoverage, r.WinnerStability, r.TopKStability,
                        r.CandidateCount, r.ViableCandidateCount, r.TopCompositeScore,
                        r.MeanViableCompositeScore);
                });
    }

    public async Task<int> SoftDeleteRunAsync(
        Guid tenantId, Guid analysisRunId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Tenant-scoped soft delete across the run and all dependent history. DB remains the source of
        // truth; rows are marked IsDeleted = 1 (write-once semantics) rather than physically removed.
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_AnalysisRunStepAttempt
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE TenantId = @TenantId AND IsDeleted = 0
               AND AnalysisRunStepId IN (
                   SELECT AnalysisRunStepId FROM POLOXI.Legal_AnalysisRunStep
                    WHERE TenantId = @TenantId AND AnalysisRunId = @AnalysisRunId);

            UPDATE POLOXI.Legal_AnalysisRunStep
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE TenantId = @TenantId AND AnalysisRunId = @AnalysisRunId AND IsDeleted = 0;

            UPDATE POLOXI.Legal_AnalysisRunInputSnapshot
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE TenantId = @TenantId AND AnalysisRunId = @AnalysisRunId AND IsDeleted = 0;

            UPDATE POLOXI.Legal_AnalysisRunCurrentAssessment
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE TenantId = @TenantId AND AnalysisRunId = @AnalysisRunId AND IsDeleted = 0;

            UPDATE POLOXI.Legal_WideRunResult
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE TenantId = @TenantId AND AnalysisRunId = @AnalysisRunId AND IsDeleted = 0;

            UPDATE POLOXI.Legal_AnalysisRun
               SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
             WHERE TenantId = @TenantId AND AnalysisRunId = @AnalysisRunId AND IsDeleted = 0;

            SELECT @@ROWCOUNT;
            """,
            new { TenantId = tenantId, AnalysisRunId = analysisRunId, ModifiedByUserId = modifiedByUserId },
            cancellationToken: cancellationToken));

        return affected;
    }

    private sealed record RunRow(
        Guid AnalysisRunId, string RunLabel, string Title, string RunTypeCode, string? TriggerTypeCode,
        string? TriggerText, string ExecutionStatusCode, string PublicationStatusCode, string? PackLabel,
        string? InputSnapshotLabel, DateTime RequestedUtc, DateTime? StartedUtc, DateTime? CompletedUtc,
        int? DurationSeconds, string? ExecutionBanner, string? FooterNote);

    private sealed record StepRow(
        Guid AnalysisRunStepId, Guid AnalysisRunId, string StepCode, string DisplayName, string? Description,
        int Sequence, string StatusCode, string? StatusLabel);

    private sealed record AttemptRow(
        Guid AnalysisRunStepId, int AttemptNumber, string StatusCode, string? Summary,
        DateTime? StartedUtc, DateTime? CompletedUtc);

    private sealed record CurrentRow(
        Guid CurrentAssessmentId, string AssessmentLabel, Guid AnalysisRunId, string RunLabel,
        string? ReviewStateText, string? FreshnessStatusCode, string? FreshnessNote, DateTime PublishedUtc);

    private sealed record SnapshotRow(
        Guid InputSnapshotId, Guid AnalysisRunId, string SnapshotLabel, string? Query, string? ContextCode,
        string? ModeCode, string? ModelCode, string? DomainPackCode, string? DiscoveryModeCode,
        string? Jurisdiction, string? GoverningLaw, string? CourtOrForum, string? Posture, string? MotionTarget,
        string? CounterfactualAssumption, string? ClarificationAnswer, bool? UsedDependencyGraph,
        string ContractJson, DateTime CapturedUtc);

    private sealed record QualityRow(
        Guid AnalysisRunId, string? StatusCode, string? AnswerVerificationCode,
        decimal FinalConfidence, decimal DecisionConfidence, decimal EvidenceCoverage,
        decimal DecisionEvidenceCoverage, decimal WinnerStability, decimal TopKStability,
        int CandidateCount, int ViableCandidateCount, decimal TopCompositeScore,
        decimal MeanViableCompositeScore);
}