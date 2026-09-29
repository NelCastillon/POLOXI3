using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Persistence.Repositories;

// DB-backed reads for the Attorney Decision Input (Human Intelligence) tab. Tenant-scoped on every
// query. POLOXI remains the authoritative evaluator; this repository never computes or mutates scores.
public sealed class AttorneyDecisionInputRepository(ISqlConnectionFactory connectionFactory) : IAttorneyDecisionInputRepository
{
    public async Task<MatterHumanIntelligenceDto> GetMatterHumanIntelligenceAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var enabled = await IsFeatureEnabledAsync(connection, cancellationToken);

        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            -- Nodes
            SELECT node.DecisionNodeId, node.ParentNodeId, node.CanonicalKey, node.NodeKindCode,
                   node.NodeLevel, node.NodeText, node.OriginCode, node.PlacementKey,
                   node.StructuralStateCode, node.EvidenceStateCode, node.AuthorityStateCode, node.EvaluatedValue,
                   (SELECT COUNT(1) FROM POLOXI.Legal_AttorneyDecisionChallenge challenge
                     WHERE challenge.DecisionNodeId = node.DecisionNodeId AND challenge.IsDeleted = 0
                       AND challenge.StatusCode IN (N'Open', N'UnderReview')) AS OpenChallengeCount
            FROM POLOXI.Legal_DecisionNode node
            WHERE node.TenantId = @TenantId AND node.MatterId = @MatterId AND node.IsDeleted = 0
            ORDER BY node.NodeLevel, node.PlacementKey, node.CanonicalKey;

            -- Assessments (with attorney display name / role)
            SELECT assessment.AssessmentId, assessment.DecisionNodeId, assessment.AttorneyUserId,
                   LTRIM(RTRIM(CONCAT(ISNULL(u.FirstName, N''), N' ', ISNULL(u.LastName, N'')))) AS AttorneyDisplayName,
                   role.DisplayName AS AttorneyRole,
                   assessment.ConfirmedValue, assessment.SuggestedMidpoint,
                   assessment.PreviousSiblingId, assessment.PreviousSiblingValue,
                   assessment.NextSiblingId, assessment.NextSiblingValue,
                   assessment.MethodCode, assessment.Rationale, assessment.StatusCode,
                   assessment.AssessmentVersion, assessment.CreatedDateUtc
            FROM POLOXI.Legal_AttorneyRelativeAssessment assessment
            LEFT JOIN dbo.AspNetUsers u ON u.Id = assessment.AttorneyUserId
            OUTER APPLY (
                SELECT TOP 1 r.DisplayName
                FROM SaaS.SaaS_TenantMembership m
                JOIN SaaS.SaaS_Role r ON r.RoleId = m.RoleId AND r.IsDeleted = 0
                WHERE m.UserId = assessment.AttorneyUserId AND m.TenantId = @TenantId AND m.IsDeleted = 0
            ) role
            WHERE assessment.TenantId = @TenantId AND assessment.MatterId = @MatterId AND assessment.IsDeleted = 0
            ORDER BY assessment.CreatedDateUtc;

            -- Approved assessments (single active per node)
            SELECT approval.ApprovalId, approval.DecisionNodeId, approval.AssessmentId,
                   assessment.ConfirmedValue,
                   approval.ApprovedByUserId,
                   LTRIM(RTRIM(CONCAT(ISNULL(u.FirstName, N''), N' ', ISNULL(u.LastName, N'')))) AS ApprovedByDisplayName,
                   approval.GovernancePolicyCode, approval.CreatedDateUtc AS ApprovedDateUtc
            FROM POLOXI.Legal_ApprovedMatterAssessment approval
            JOIN POLOXI.Legal_AttorneyRelativeAssessment assessment ON assessment.AssessmentId = approval.AssessmentId
            LEFT JOIN dbo.AspNetUsers u ON u.Id = approval.ApprovedByUserId
            WHERE approval.TenantId = @TenantId AND approval.MatterId = @MatterId
              AND approval.IsActive = 1 AND approval.IsDeleted = 0;
            """,
            new { TenantId = tenantId, MatterId = matterId },
            cancellationToken: cancellationToken));

        var nodeRows = (await multi.ReadAsync<NodeRow>()).ToList();
        var assessmentRows = (await multi.ReadAsync<AttorneyRelativeAssessmentDto>()).ToList();
        var approvalRows = (await multi.ReadAsync<ApprovedMatterAssessmentDto>()).ToList();

        var assessmentsByNode = assessmentRows
            .GroupBy(a => a.DecisionNodeId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<AttorneyRelativeAssessmentDto>)g.ToArray());
        var approvalByNode = approvalRows
            .GroupBy(a => a.DecisionNodeId)
            .ToDictionary(g => g.Key, g => g.First());

        var nodes = nodeRows.Select(row => new AttorneyDecisionNodeDto(
                row.DecisionNodeId, row.ParentNodeId, row.CanonicalKey, row.NodeKindCode, row.NodeLevel,
                row.NodeText, row.OriginCode, row.PlacementKey, row.StructuralStateCode, row.EvidenceStateCode,
                row.AuthorityStateCode, row.EvaluatedValue,
                assessmentsByNode.TryGetValue(row.DecisionNodeId, out var a) ? a : [],
                approvalByNode.TryGetValue(row.DecisionNodeId, out var ap) ? ap : null,
                row.OpenChallengeCount))
            .ToArray();

        return new MatterHumanIntelligenceDto(
            matterId,
            enabled,
            nodes.Length,
            assessmentRows.Select(a => a.AttorneyUserId).Distinct().Count(),
            assessmentRows.Count,
            approvalRows.Count,
            nodeRows.Sum(n => n.OpenChallengeCount),
            nodes);
    }

    private static async Task<bool> IsFeatureEnabledAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        var value = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT TOP 1 SettingValue FROM POLOXI.Legal_DecisionSetting WHERE SettingKey = N'AttorneyDecisionInput.Enabled' AND IsDeleted = 0;",
            cancellationToken: cancellationToken));
        return bool.TryParse(value, out var parsed) && parsed;
    }

    private sealed record NodeRow(
        Guid DecisionNodeId,
        Guid? ParentNodeId,
        string CanonicalKey,
        string NodeKindCode,
        int NodeLevel,
        string NodeText,
        string OriginCode,
        string? PlacementKey,
        string StructuralStateCode,
        string EvidenceStateCode,
        string AuthorityStateCode,
        decimal? EvaluatedValue,
        int OpenChallengeCount);
}
