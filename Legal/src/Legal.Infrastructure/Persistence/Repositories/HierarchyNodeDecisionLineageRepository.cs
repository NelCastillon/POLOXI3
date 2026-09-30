using Dapper;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper reader for the durable Hierarchy Node → Decision lineage map (migration 0368;
// POLOXI.Legal_HierarchyNodeDecisionLineage). Read-only: this is the source-of-truth mapping the
// channel lineage resolver consumes to project verified contributions into typed recompetition.
public sealed class HierarchyNodeDecisionLineageRepository(ISqlConnectionFactory connectionFactory) : IHierarchyNodeDecisionLineageRepository
{
    private const string ReadColumns =
        "HierarchyNodeDecisionLineageId, DecisionMatterId, HierarchyExecutionId, HierarchyNodeId, " +
        "DecisionSessionId, DecisionBranchId, DecisionCandidateId, LineageSourceCode";

    public async Task<IReadOnlyCollection<HierarchyNodeDecisionLineageDto>> GetLineageForNodeAsync(
        Guid tenantId, Guid hierarchyExecutionId, Guid hierarchyNodeId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<HierarchyNodeDecisionLineageDto>(new CommandDefinition(
            $"SELECT {ReadColumns} FROM POLOXI.Legal_HierarchyNodeDecisionLineage " +
            "WHERE TenantId=@tenantId AND HierarchyExecutionId=@hierarchyExecutionId AND HierarchyNodeId=@hierarchyNodeId AND IsDeleted=0;",
            new { tenantId, hierarchyExecutionId, hierarchyNodeId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<HierarchyNodeDecisionLineageDto>> GetLineageForSessionAsync(
        Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<HierarchyNodeDecisionLineageDto>(new CommandDefinition(
            $"SELECT {ReadColumns} FROM POLOXI.Legal_HierarchyNodeDecisionLineage " +
            "WHERE TenantId=@tenantId AND DecisionSessionId=@decisionSessionId AND IsDeleted=0;",
            new { tenantId, decisionSessionId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }
}
