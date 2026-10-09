using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Channels;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper reader for a matter's latest Wide execution scoring trace (POLOXI.Legal_Wide*). Read-only.
// Surfaces the hierarchy levels (LevelNumber = L1..Ln), named branches/candidates and the exact
// persisted scoring values the Channel Scoring (LPI) workspace shows. Fail-soft: no execution =>
// MatterScoringTraceReadModel.Empty(matterId).
public sealed class MatterScoringTraceReader(ISqlConnectionFactory connectionFactory) : IMatterScoringTraceReader
{
    public async Task<MatterScoringTraceReadModel> GetLatestForMatterAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        // Latest Wide execution for the matter (tenant-scoped), preferring one that actually produced
        // candidates so the trace is meaningful.
        var execution = await connection.QuerySingleOrDefaultAsync<ExecutionRow>(new CommandDefinition(
            """
            SELECT TOP 1 e.WideExecutionId, e.QueryText,
                   COALESCE(e.DepthReached, 0) AS DepthReached,
                   COALESCE(e.CandidateCount, 0) AS CandidateCount,
                   COALESCE(e.EvidenceCoverage, CAST(0 AS DECIMAL(9,4))) AS EvidenceCoverage,
                   COALESCE(e.FinalConfidence, CAST(0 AS DECIMAL(9,4))) AS FinalConfidence,
                   e.CreatedDateUtc
            FROM POLOXI.Legal_WideExecution e
            WHERE e.MatterId = @MatterId AND e.TenantId = @TenantId AND e.IsDeleted = 0
            ORDER BY CASE WHEN e.CandidateCount > 0 THEN 0 ELSE 1 END, e.CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, MatterId = matterId }, cancellationToken: cancellationToken));

        if (execution is null)
            return MatterScoringTraceReadModel.Empty(matterId);

        var branchRows = (await connection.QueryAsync<BranchRow>(new CommandDefinition(
            """
            SELECT b.WideBranchId, COALESCE(b.LevelNumber, 0) AS LevelNumber, b.DisplayName, b.BranchRoleCode, b.GroundingStatusCode,
                   COALESCE(b.EvidenceCount, 0) AS EvidenceCount,
                   COALESCE(b.EvidenceSupport, CAST(0 AS DECIMAL(9,4))) AS EvidenceSupport,
                   COALESCE(b.PoloxiConfidence, CAST(0 AS DECIMAL(9,4))) AS PoloxiConfidence,
                   COALESCE(b.Confidence, CAST(0 AS DECIMAL(9,4))) AS Confidence,
                   COALESCE(b.IsEliminated, CAST(0 AS BIT)) AS IsEliminated, b.EliminationReason
            FROM POLOXI.Legal_WideBranch b
            WHERE b.WideExecutionId = @WideExecutionId AND b.TenantId = @TenantId AND b.IsDeleted = 0
            ORDER BY b.LevelNumber, b.SortOrder;
            """,
            new { TenantId = tenantId, execution.WideExecutionId }, cancellationToken: cancellationToken))).ToList();

        var candidateRows = (await connection.QueryAsync<CandidateRow>(new CommandDefinition(
            """
            SELECT c.WideCandidateId, COALESCE(c.RankNumber, 0) AS RankNumber, c.DisplayName,
                   COALESCE(c.CompositeScore, CAST(0 AS DECIMAL(9,4))) AS CompositeScore,
                   COALESCE(c.IsConstraintViolation, CAST(0 AS BIT)) AS IsConstraintViolation
            FROM POLOXI.Legal_WideCandidate c
            WHERE c.WideExecutionId = @WideExecutionId AND c.TenantId = @TenantId AND c.IsDeleted = 0
            ORDER BY c.RankNumber, c.CompositeScore DESC;
            """,
            new { TenantId = tenantId, execution.WideExecutionId }, cancellationToken: cancellationToken))).ToList();

        var scoreRows = (await connection.QueryAsync<CandidateBranchRow>(new CommandDefinition(
            """
            SELECT s.WideCandidateId, s.BranchDisplayName,
                   COALESCE(s.EvidenceScore, CAST(0 AS DECIMAL(9,4))) AS EvidenceScore
            FROM POLOXI.Legal_WideCandidateBranchScore s
            JOIN POLOXI.Legal_WideCandidate c ON c.WideCandidateId = s.WideCandidateId AND c.IsDeleted = 0
            WHERE c.WideExecutionId = @WideExecutionId AND s.TenantId = @TenantId AND s.IsDeleted = 0
            ORDER BY s.EvidenceScore DESC;
            """,
            new { TenantId = tenantId, execution.WideExecutionId }, cancellationToken: cancellationToken))).ToList();

        var levels = branchRows
            .GroupBy(b => b.LevelNumber)
            .OrderBy(g => g.Key)
            .Select(g => new MatterScoringLevel(
                g.Key,
                g.Select(b => new MatterScoringBranch(
                    b.WideBranchId, b.LevelNumber, b.DisplayName ?? "(unnamed)", b.BranchRoleCode,
                    b.GroundingStatusCode, b.EvidenceCount, b.EvidenceSupport, b.PoloxiConfidence,
                    b.Confidence, b.IsEliminated, b.EliminationReason)).ToArray()))
            .ToArray();

        var scoresByCandidate = scoreRows
            .GroupBy(s => s.WideCandidateId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<MatterScoringCandidateBranch>)g
                    .Select(s => new MatterScoringCandidateBranch(s.BranchDisplayName ?? "(unnamed)", s.EvidenceScore))
                    .ToArray());

        var candidates = candidateRows
            .Select(c => new MatterScoringCandidate(
                c.WideCandidateId, c.RankNumber, c.DisplayName ?? "(unnamed)", c.CompositeScore,
                c.IsConstraintViolation,
                scoresByCandidate.TryGetValue(c.WideCandidateId, out var bs) ? bs : []))
            .ToArray();

        return new MatterScoringTraceReadModel(
            matterId,
            execution.WideExecutionId,
            execution.QueryText,
            execution.DepthReached,
            execution.CandidateCount,
            execution.EvidenceCoverage,
            execution.FinalConfidence,
            execution.CreatedDateUtc,
            MatterScoringFormulaLegend.Default,
            levels,
            candidates);
    }

    private sealed record ExecutionRow(
        Guid WideExecutionId, string? QueryText, int DepthReached, int CandidateCount,
        decimal EvidenceCoverage, decimal FinalConfidence, DateTime CreatedDateUtc);

    private sealed record BranchRow(
        Guid WideBranchId, int LevelNumber, string? DisplayName, string? BranchRoleCode,
        string? GroundingStatusCode, int EvidenceCount, decimal EvidenceSupport, decimal PoloxiConfidence,
        decimal Confidence, bool IsEliminated, string? EliminationReason);

    private sealed record CandidateRow(
        Guid WideCandidateId, int RankNumber, string? DisplayName, decimal CompositeScore, bool IsConstraintViolation);

    private sealed record CandidateBranchRow(
        Guid WideCandidateId, string? BranchDisplayName, decimal EvidenceScore);
}
