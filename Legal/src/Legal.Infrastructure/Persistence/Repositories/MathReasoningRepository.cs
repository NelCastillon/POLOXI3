using Dapper;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper persistence for the POLOXI Mathematics V1 run audit trail. Writes the execution header and its
// obligation/candidate children in a single transaction, mirroring IntelligenceWideRepository's style.
public sealed class MathReasoningRepository(ISqlConnectionFactory connectionFactory) : IMathReasoningRepository
{
    public async Task<Guid> SaveMathExecutionAsync(MathExecutionRecord execution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);

        var mathExecutionId = Guid.NewGuid();

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            const string executionSql = """
INSERT POLOXI.Legal_MathExecution(MathExecutionId,TenantId,UserId,ProblemText,CorrelationId,OutcomeCode,VerificationStatusCode,FinalAnswer,CanonicalAnswer,SolutionSummary,RemainingUncertainty,DiscoveryConfidence,SelfConsistencyAgreement,ResearchPriority,EvidenceSupport,MathematicalVerification,FalsificationCoverage,EpistemicStateLabel,ModelCode,DurationMilliseconds,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@MathExecutionId,@TenantId,@UserId,@ProblemText,@CorrelationId,@OutcomeCode,@VerificationStatusCode,@FinalAnswer,@CanonicalAnswer,@SolutionSummary,@RemainingUncertainty,@DiscoveryConfidence,@SelfConsistencyAgreement,@ResearchPriority,@EvidenceSupport,@MathematicalVerification,@FalsificationCoverage,@EpistemicStateLabel,@ModelCode,@DurationMilliseconds,SYSUTCDATETIME(),@UserId,0);
""";
            await connection.ExecuteAsync(new CommandDefinition(executionSql, new
            {
                MathExecutionId = mathExecutionId,
                execution.TenantId,
                execution.UserId,
                execution.ProblemText,
                execution.CorrelationId,
                execution.OutcomeCode,
                execution.VerificationStatusCode,
                execution.FinalAnswer,
                execution.CanonicalAnswer,
                execution.SolutionSummary,
                execution.RemainingUncertainty,
                execution.DiscoveryConfidence,
                execution.SelfConsistencyAgreement,
                execution.ResearchPriority,
                execution.EvidenceSupport,
                execution.MathematicalVerification,
                execution.FalsificationCoverage,
                execution.EpistemicStateLabel,
                execution.ModelCode,
                execution.DurationMilliseconds,
            }, transaction, cancellationToken: cancellationToken));

            if (execution.Obligations.Count > 0)
            {
                const string obligationSql = """
INSERT POLOXI.Legal_MathObligation(MathObligationId,MathExecutionId,TenantId,ObligationKey,Statement,VerificationMethodCode,StatusCode,CounterexampleStatusCode,DiscoveryConfidence,VerificationNote,SortOrder,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(NEWID(),@MathExecutionId,@TenantId,@ObligationKey,@Statement,@VerificationMethodCode,@StatusCode,@CounterexampleStatusCode,@DiscoveryConfidence,@VerificationNote,@SortOrder,SYSUTCDATETIME(),@UserId,0);
""";
                await connection.ExecuteAsync(new CommandDefinition(obligationSql, execution.Obligations.Select(o => new
                {
                    MathExecutionId = mathExecutionId,
                    execution.TenantId,
                    UserId = execution.UserId,
                    o.ObligationKey,
                    o.Statement,
                    o.VerificationMethodCode,
                    o.StatusCode,
                    o.CounterexampleStatusCode,
                    o.DiscoveryConfidence,
                    o.VerificationNote,
                    o.SortOrder,
                }), transaction, cancellationToken: cancellationToken));
            }

            if (execution.Candidates.Count > 0)
            {
                const string candidateSql = """
INSERT POLOXI.Legal_MathCandidate(MathCandidateId,MathExecutionId,TenantId,CandidateKey,ObjectTypeCode,Name,Description,DiscoveryConfidence,VerificationStatusCode,SortOrder,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(NEWID(),@MathExecutionId,@TenantId,@CandidateKey,@ObjectTypeCode,@Name,@Description,@DiscoveryConfidence,@VerificationStatusCode,@SortOrder,SYSUTCDATETIME(),@UserId,0);
""";
                await connection.ExecuteAsync(new CommandDefinition(candidateSql, execution.Candidates.Select(c => new
                {
                    MathExecutionId = mathExecutionId,
                    execution.TenantId,
                    UserId = execution.UserId,
                    c.CandidateKey,
                    c.ObjectTypeCode,
                    c.Name,
                    c.Description,
                    c.DiscoveryConfidence,
                    c.VerificationStatusCode,
                    c.SortOrder,
                }), transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return mathExecutionId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyList<MathExecutionSummary>> ListMathExecutionsAsync(Guid tenantId, int take = 50, CancellationToken cancellationToken = default)
    {
        var top = take <= 0 ? 50 : Math.Min(take, 500);

        const string sql = """
SELECT TOP (@Take) MathExecutionId,ProblemText,CorrelationId,OutcomeCode,VerificationStatusCode,CanonicalAnswer,DiscoveryConfidence,SelfConsistencyAgreement,DurationMilliseconds,CreatedDateUtc
FROM POLOXI.Legal_MathExecution
WHERE TenantId=@TenantId AND IsDeleted=0
ORDER BY CreatedDateUtc DESC;
""";

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<MathExecutionSummary>(new CommandDefinition(sql, new { TenantId = tenantId, Take = top }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<MathExecutionDetail?> GetMathExecutionAsync(Guid tenantId, Guid mathExecutionId, CancellationToken cancellationToken = default)
    {
        const string headerSql = """
SELECT MathExecutionId,TenantId,UserId,ProblemText,CorrelationId,OutcomeCode,VerificationStatusCode,FinalAnswer,CanonicalAnswer,SolutionSummary,RemainingUncertainty,DiscoveryConfidence,SelfConsistencyAgreement,ResearchPriority,EvidenceSupport,MathematicalVerification,FalsificationCoverage,EpistemicStateLabel,ModelCode,DurationMilliseconds,CreatedDateUtc
FROM POLOXI.Legal_MathExecution
WHERE TenantId=@TenantId AND MathExecutionId=@MathExecutionId AND IsDeleted=0;
""";

        const string obligationSql = """
SELECT ObligationKey,Statement,VerificationMethodCode,StatusCode,CounterexampleStatusCode,DiscoveryConfidence,VerificationNote,SortOrder
FROM POLOXI.Legal_MathObligation
WHERE TenantId=@TenantId AND MathExecutionId=@MathExecutionId AND IsDeleted=0
ORDER BY SortOrder;
""";

        const string candidateSql = """
SELECT CandidateKey,ObjectTypeCode,Name,Description,DiscoveryConfidence,VerificationStatusCode,SortOrder
FROM POLOXI.Legal_MathCandidate
WHERE TenantId=@TenantId AND MathExecutionId=@MathExecutionId AND IsDeleted=0
ORDER BY SortOrder;
""";

        var args = new { TenantId = tenantId, MathExecutionId = mathExecutionId };

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var header = await connection.QuerySingleOrDefaultAsync<MathExecutionDetail>(new CommandDefinition(headerSql, args, cancellationToken: cancellationToken));
        if (header is null)
        {
            return null;
        }

        var obligations = (await connection.QueryAsync<MathObligationRecord>(new CommandDefinition(obligationSql, args, cancellationToken: cancellationToken))).AsList();
        var candidates = (await connection.QueryAsync<MathCandidateRecord>(new CommandDefinition(candidateSql, args, cancellationToken: cancellationToken))).AsList();

        return header with { Obligations = obligations, Candidates = candidates };
    }
}
