using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision.Channels;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper repository for Decision Channel Contributions (migration 0367; POLOXI.Legal_ChannelContribution).
// Qualitative source-truth only; append-only. No numeric score column exists — POLOXI Wide2 remains the
// sole decision engine and the derived node signal/state is persisted by the DecisionSupportSignal path.
public sealed class LegalChannelContributionRepository(ISqlConnectionFactory connectionFactory) : IChannelContributionRepository
{
    private const string ReadColumns =
        "ChannelContributionId, DecisionMatterId, HierarchyExecutionId, HierarchyNodeId, ChannelTypeCode, " +
        "RelationCode, VerificationStateCode, TargetSignalCode, ApplicabilityCode, DirectnessCode, " +
        "SourceTypeCode, SourceId, SourceLabel, LegalDocumentId, LegalDocumentVersionId, LegalDocumentPassageId, " +
        "ProposedByModel, PromptRunId, VerificationReason, EffectiveFromUtc, EffectiveToUtc, CreatedDateUtc";

    public async Task SaveContributionsAsync(IReadOnlyCollection<ChannelContributionPersistence> contributions, CancellationToken cancellationToken = default)
    {
        if (contributions.Count == 0) return;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO POLOXI.Legal_ChannelContribution
                (ChannelContributionId, DecisionMatterId, HierarchyExecutionId, HierarchyNodeId, ChannelTypeCode,
                 RelationCode, VerificationStateCode, TargetSignalCode, ApplicabilityCode, DirectnessCode,
                 SourceTypeCode, SourceId, SourceLabel, LegalDocumentId, LegalDocumentVersionId, LegalDocumentPassageId,
                 ProposedByModel, PromptRunId, VerificationReason, EffectiveFromUtc, EffectiveToUtc, TenantId, CreatedByUserId)
            VALUES
                (@ChannelContributionId, @DecisionMatterId, @HierarchyExecutionId, @HierarchyNodeId, @ChannelTypeCode,
                 @RelationCode, @VerificationStateCode, @TargetSignalCode, @ApplicabilityCode, @DirectnessCode,
                 @SourceTypeCode, @SourceId, @SourceLabel, @LegalDocumentId, @LegalDocumentVersionId, @LegalDocumentPassageId,
                 @ProposedByModel, @PromptRunId, @VerificationReason, @EffectiveFromUtc, @EffectiveToUtc, @TenantId, @ActorUserId);
            """,
            contributions, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForNodeAsync(Guid tenantId, Guid hierarchyNodeId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ChannelContributionDto>(new CommandDefinition(
            $"SELECT {ReadColumns} FROM POLOXI.Legal_ChannelContribution WHERE TenantId=@tenantId AND HierarchyNodeId=@hierarchyNodeId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;",
            new { tenantId, hierarchyNodeId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForExecutionAsync(Guid tenantId, Guid hierarchyExecutionId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ChannelContributionDto>(new CommandDefinition(
            $"SELECT {ReadColumns} FROM POLOXI.Legal_ChannelContribution WHERE TenantId=@tenantId AND HierarchyExecutionId=@hierarchyExecutionId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;",
            new { tenantId, hierarchyExecutionId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    public async Task<IReadOnlyCollection<ChannelContributionDto>> GetContributionsForMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ChannelContributionDto>(new CommandDefinition(
            $"SELECT {ReadColumns} FROM POLOXI.Legal_ChannelContribution WHERE TenantId=@tenantId AND DecisionMatterId=@decisionMatterId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;",
            new { tenantId, decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }
}
