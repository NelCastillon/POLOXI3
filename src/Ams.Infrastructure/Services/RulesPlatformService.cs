using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Platform;
using Ams.Application.Services;
using Dapper;

namespace Ams.Infrastructure.Services;

public sealed class RulesPlatformService(ISqlConnectionFactory connectionFactory) : IRulesPlatformService
{
    public async Task<RulesEvaluationResponse> EvaluateAsync(EvaluateRulesRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.EntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.EntityTypeCode) || string.IsNullOrWhiteSpace(request.CorrelationId))
            throw new ArgumentException("Tenant, entity type, entity id, and correlation id are required.");
        if (request.Facts.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Rule facts must be a JSON object.");

        const string selectSql = """
            SELECT RuleDefinitionId,RuleCode,VersionNumber,ConditionJson,OutcomeJson,SeverityCode,StopsProcessing
            FROM Rules.RuleDefinition
            WHERE (TenantId=@TenantId OR TenantId IS NULL) AND EntityTypeCode=@EntityTypeCode
              AND (@ModuleCode IS NULL OR SourceModuleCode IS NULL OR SourceModuleCode=@ModuleCode)
              AND IsActive=1 AND IsDeleted=0 AND EffectiveFromUtc<=SYSUTCDATETIME()
              AND (EffectiveToUtc IS NULL OR EffectiveToUtc>SYSUTCDATETIME())
            ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END,SeverityCode DESC,RuleCode,VersionNumber DESC;
            """;
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var definitions = (await connection.QueryAsync<RuleDefinitionRow>(new CommandDefinition(selectSql, request, cancellationToken: cancellationToken))).AsList();
        var results = new List<RuleEvaluationResult>(definitions.Count);
        var stopped = false;
        foreach (var definition in definitions)
        {
            var executionId = Guid.NewGuid();
            bool? isMatch = null;
            string? error = null;
            JsonElement outcome;
            try
            {
                using var condition = JsonDocument.Parse(definition.ConditionJson);
                using var outcomeDocument = JsonDocument.Parse(definition.OutcomeJson);
                isMatch = JsonConditionEvaluator.Evaluate(condition.RootElement, request.Facts);
                outcome = outcomeDocument.RootElement.Clone();
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                error = ex.Message;
                outcome = JsonDocument.Parse("{}").RootElement.Clone();
            }

            var status = error is null ? "COMPLETED" : "FAILED";
            var resultJson = JsonSerializer.Serialize(new { definition.RuleCode, definition.VersionNumber, isMatch, outcome });
            const string insertSql = """
                INSERT Rules.RuleExecution(RuleExecutionId,TenantId,RuleDefinitionId,EntityTypeCode,EntityId,CorrelationId,StatusCode,IsMatch,InputSnapshotJson,ResultJson,ErrorMessage,EvaluatedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
                VALUES(@RuleExecutionId,@TenantId,@RuleDefinitionId,@EntityTypeCode,@EntityId,@CorrelationId,@StatusCode,@IsMatch,@InputSnapshotJson,@ResultJson,@ErrorMessage,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0);
                """;
            await connection.ExecuteAsync(new CommandDefinition(insertSql, new
            {
                RuleExecutionId = executionId,
                request.TenantId,
                definition.RuleDefinitionId,
                request.EntityTypeCode,
                request.EntityId,
                request.CorrelationId,
                StatusCode = status,
                IsMatch = isMatch,
                InputSnapshotJson = request.Facts.GetRawText(),
                ResultJson = resultJson,
                ErrorMessage = error,
                request.ActorUserId
            }, cancellationToken: cancellationToken));
            results.Add(new(executionId, definition.RuleDefinitionId, definition.RuleCode, definition.VersionNumber, status, isMatch, definition.SeverityCode, definition.StopsProcessing, outcome, error));
            if (isMatch == true && definition.StopsProcessing)
            {
                stopped = true;
                break;
            }
        }
        return new(request.CorrelationId, stopped, results);
    }

    private sealed record RuleDefinitionRow(Guid RuleDefinitionId, string RuleCode, int VersionNumber, string ConditionJson, string OutcomeJson, string SeverityCode, bool StopsProcessing);
}
