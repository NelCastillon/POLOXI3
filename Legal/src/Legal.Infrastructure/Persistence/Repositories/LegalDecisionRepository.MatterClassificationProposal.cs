using Dapper;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// 0448: matter classification proposals (POLOXI.Legal_DecisionMatterClassificationProposal) and routing codes
// (POLOXI.Legal_MatterEvidenceRoutingCode). The authoritative matter record is never written here.
public sealed partial class LegalDecisionRepository
{
    private const string MatterClassificationProposalColumns = """
        p.DecisionMatterClassificationProposalId, p.DecisionMatterId, p.FieldCode, p.CurrentValue, p.ProposedValue, p.Confidence,
        p.StructuredSignals, p.NarrativeSignals, p.Rationale, p.StatusCode, p.DecidedByUserId, p.DecidedDateUtc, p.DecisionNote,
        p.LastProposedExecutionId, p.CreatedDateUtc, p.ModifiedDateUtc
        """;

    public async Task<IReadOnlyCollection<MatterClassificationProposalDto>> GetMatterClassificationProposalsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterClassificationProposalDto>(new CommandDefinition(
            $"""
            SELECT {MatterClassificationProposalColumns}
            FROM POLOXI.Legal_DecisionMatterClassificationProposal p
            WHERE p.TenantId = @TenantId AND p.DecisionMatterId = @DecisionMatterId AND p.IsDeleted = 0
            ORDER BY CASE p.StatusCode WHEN N'PROPOSED' THEN 0 ELSE 1 END, p.FieldCode, p.CreatedDateUtc DESC;
            """,
            new { TenantId = tenantId, DecisionMatterId = decisionMatterId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    // Upserts the single OPEN (PROPOSED) row per matter/field. ACCEPTED/REJECTED rows are history and untouched;
    // a field the attorney already decided is not re-proposed while that decision stands.
    public async Task UpsertMatterClassificationProposalsAsync(Guid tenantId, Guid userId, Guid decisionMatterId, IReadOnlyCollection<MatterClassificationProposalEvaluation> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) return;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        foreach (var row in rows)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                IF EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatterClassificationProposal
                           WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND FieldCode = @FieldCode
                             AND IsDeleted = 0 AND StatusCode IN (N'ACCEPTED', N'REJECTED') AND CurrentValue = @CurrentValue)
                    RETURN;
                UPDATE POLOXI.Legal_DecisionMatterClassificationProposal
                   SET CurrentValue = @CurrentValue, ProposedValue = @ProposedValue, Confidence = @Confidence,
                       StructuredSignals = @StructuredSignals, NarrativeSignals = @NarrativeSignals, Rationale = @Rationale,
                       LastProposedExecutionId = @LastProposedExecutionId, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
                 WHERE TenantId = @TenantId AND DecisionMatterId = @DecisionMatterId AND FieldCode = @FieldCode AND IsDeleted = 0 AND StatusCode = N'PROPOSED';
                IF @@ROWCOUNT = 0
                    INSERT POLOXI.Legal_DecisionMatterClassificationProposal
                        (DecisionMatterClassificationProposalId, TenantId, DecisionMatterId, FieldCode, CurrentValue, ProposedValue, Confidence,
                         StructuredSignals, NarrativeSignals, Rationale, StatusCode, LastProposedExecutionId, CreatedDateUtc, CreatedByUserId, IsDeleted)
                    VALUES (NEWID(), @TenantId, @DecisionMatterId, @FieldCode, @CurrentValue, @ProposedValue, @Confidence,
                            @StructuredSignals, @NarrativeSignals, @Rationale, N'PROPOSED', @LastProposedExecutionId, SYSUTCDATETIME(), @UserId, 0);
                """,
                new
                {
                    TenantId = tenantId, UserId = userId, DecisionMatterId = decisionMatterId, row.FieldCode, row.CurrentValue, row.ProposedValue,
                    row.Confidence, row.StructuredSignals, row.NarrativeSignals, row.Rationale, row.LastProposedExecutionId
                }, cancellationToken: cancellationToken));
        }
    }

    public async Task<bool> DecideMatterClassificationProposalAsync(Guid tenantId, Guid userId, Guid proposalId, string statusCode, string? decisionNote, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE POLOXI.Legal_DecisionMatterClassificationProposal
               SET StatusCode = @StatusCode, DecidedByUserId = @UserId, DecidedDateUtc = SYSUTCDATETIME(), DecisionNote = @DecisionNote,
                   ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @UserId
             WHERE TenantId = @TenantId AND DecisionMatterClassificationProposalId = @ProposalId AND IsDeleted = 0 AND StatusCode = N'PROPOSED'
               AND EXISTS (SELECT 1 FROM POLOXI.Legal_MatterEvidenceRoutingCode c
                           WHERE c.CodeKind = N'CLASSIFICATION_PROPOSAL_STATUS' AND c.Code = @StatusCode AND c.IsDeleted = 0 AND c.IsActive = 1
                             AND (c.TenantId IS NULL OR c.TenantId = @TenantId));
            """,
            new { TenantId = tenantId, UserId = userId, ProposalId = proposalId, StatusCode = statusCode, DecisionNote = decisionNote }, cancellationToken: cancellationToken));
        return affected > 0;
    }

    public async Task<IReadOnlyCollection<MatterEvidenceRoutingCodeDto>> GetMatterEvidenceRoutingCodesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MatterEvidenceRoutingCodeDto>(new CommandDefinition(
            """
            SELECT CodeKind, Code, Name, Description, RunsL3, SortOrder
            FROM POLOXI.Legal_MatterEvidenceRoutingCode
            WHERE IsDeleted = 0 AND IsActive = 1 AND (TenantId IS NULL OR TenantId = @TenantId)
            ORDER BY CodeKind, SortOrder, Name;
            """,
            new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }
}
