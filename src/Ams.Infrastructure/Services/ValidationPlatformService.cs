using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Platform;
using Ams.Application.Services;
using Dapper;

namespace Ams.Infrastructure.Services;

public sealed class ValidationPlatformService(ISqlConnectionFactory connectionFactory) : IValidationPlatformService
{
    public async Task<ValidationExecutionResponse> ValidateAsync(ExecuteValidationsRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty || request.EntityId == Guid.Empty || string.IsNullOrWhiteSpace(request.EntityTypeCode) || string.IsNullOrWhiteSpace(request.CorrelationId))
            throw new ArgumentException("Tenant, entity type, entity id, and correlation id are required.");
        if (request.Facts.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Validation facts must be a JSON object.");

        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string existingSql = "SELECT ValidationExecutionId FROM Validation.ValidationExecution WHERE TenantId=@TenantId AND CorrelationId=@CorrelationId AND IsDeleted=0;";
        var existingId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(existingSql, request, cancellationToken: cancellationToken));
        if (existingId.HasValue)
            return await LoadResponseAsync(connection, existingId.Value, request.CorrelationId, cancellationToken);

        var executionId = Guid.NewGuid();
        const string startSql = """
            INSERT Validation.ValidationExecution(ValidationExecutionId,TenantId,EntityTypeCode,EntityId,CorrelationId,StatusCode,RequestedByUserId,StartedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
            VALUES(@ValidationExecutionId,@TenantId,@EntityTypeCode,@EntityId,@CorrelationId,N'PROCESSING',@ActorUserId,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0);
            """;
        await connection.ExecuteAsync(new CommandDefinition(startSql, new { ValidationExecutionId = executionId, request.TenantId, request.EntityTypeCode, request.EntityId, request.CorrelationId, request.ActorUserId }, cancellationToken: cancellationToken));

        try
        {
            const string definitionsSql = """
                SELECT ValidationDefinitionId,ValidationCode,ConditionJson,FailureJson,SeverityCode,IsBlocking,CanBeWaived,WaiverPermissionCode
                FROM Validation.ValidationDefinition
                WHERE (TenantId=@TenantId OR TenantId IS NULL) AND EntityTypeCode=@EntityTypeCode
                  AND (@ModuleCode IS NULL OR SourceModuleCode IS NULL OR SourceModuleCode=@ModuleCode)
                  AND (JurisdictionCode IS NULL OR JurisdictionCode=@JurisdictionCode)
                  AND (CarrierId IS NULL OR CarrierId=@CarrierId)
                  AND (LineOfBusinessCode IS NULL OR LineOfBusinessCode=@LineOfBusinessCode)
                  AND IsActive=1 AND IsDeleted=0 AND EffectiveFromUtc<=SYSUTCDATETIME()
                  AND (EffectiveToUtc IS NULL OR EffectiveToUtc>SYSUTCDATETIME())
                ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END,IsBlocking DESC,SeverityCode DESC,ValidationCode,VersionNumber DESC;
                """;
            var definitions = (await connection.QueryAsync<ValidationDefinitionRow>(new CommandDefinition(definitionsSql, request, cancellationToken: cancellationToken))).AsList();
            foreach (var definition in definitions)
            {
                var resultId = Guid.NewGuid();
                string status;
                string message;
                JsonElement evidence;
                try
                {
                    using var condition = JsonDocument.Parse(definition.ConditionJson);
                    using var failure = JsonDocument.Parse(definition.FailureJson);
                    var failed = JsonConditionEvaluator.Evaluate(condition.RootElement, request.Facts);
                    status = failed ? "FAILED" : "PASSED";
                    message = failed && failure.RootElement.TryGetProperty("message", out var messageNode) ? messageNode.GetString() ?? definition.ValidationCode : definition.ValidationCode;
                    evidence = JsonSerializer.SerializeToElement(new { condition = condition.RootElement, failed, failure = failed ? failure.RootElement : (JsonElement?)null });
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException)
                {
                    status = "ERROR";
                    message = ex.Message;
                    evidence = JsonSerializer.SerializeToElement(new { error = ex.Message });
                }
                const string resultSql = """
                    INSERT Validation.ValidationResult(ValidationResultId,TenantId,ValidationExecutionId,ValidationDefinitionId,StatusCode,SeverityCode,IsBlocking,Message,EvidenceJson,EvaluatedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
                    VALUES(@ValidationResultId,@TenantId,@ValidationExecutionId,@ValidationDefinitionId,@StatusCode,@SeverityCode,@IsBlocking,@Message,@EvidenceJson,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0);
                    """;
                await connection.ExecuteAsync(new CommandDefinition(resultSql, new { ValidationResultId = resultId, request.TenantId, ValidationExecutionId = executionId, definition.ValidationDefinitionId, StatusCode = status, definition.SeverityCode, definition.IsBlocking, Message = message, EvidenceJson = evidence.GetRawText(), request.ActorUserId }, cancellationToken: cancellationToken));
            }
            await connection.ExecuteAsync(new CommandDefinition("UPDATE Validation.ValidationExecution SET StatusCode=N'COMPLETED',CompletedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE ValidationExecutionId=@ValidationExecutionId AND TenantId=@TenantId;", new { ValidationExecutionId = executionId, request.TenantId, request.ActorUserId }, cancellationToken: cancellationToken));
        }
        catch
        {
            await connection.ExecuteAsync(new CommandDefinition("UPDATE Validation.ValidationExecution SET StatusCode=N'FAILED',CompletedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE ValidationExecutionId=@ValidationExecutionId AND TenantId=@TenantId;", new { ValidationExecutionId = executionId, request.TenantId, request.ActorUserId }, cancellationToken: cancellationToken));
            throw;
        }
        return await LoadResponseAsync(connection, executionId, request.CorrelationId, cancellationToken);
    }

    private static async Task<ValidationExecutionResponse> LoadResponseAsync(System.Data.IDbConnection connection, Guid executionId, string correlationId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT result.ValidationResultId,result.ValidationDefinitionId,definition.ValidationCode,result.StatusCode,result.SeverityCode,result.IsBlocking,definition.CanBeWaived,definition.WaiverPermissionCode,result.Message,result.EvidenceJson
            FROM Validation.ValidationResult result JOIN Validation.ValidationDefinition definition ON definition.ValidationDefinitionId=result.ValidationDefinitionId
            WHERE result.ValidationExecutionId=@ValidationExecutionId AND result.IsDeleted=0 ORDER BY result.IsBlocking DESC,result.SeverityCode DESC,definition.ValidationCode;
            SELECT StatusCode FROM Validation.ValidationExecution WHERE ValidationExecutionId=@ValidationExecutionId AND IsDeleted=0;
            """;
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { ValidationExecutionId = executionId }, cancellationToken: cancellationToken));
        var rows = (await grid.ReadAsync<ValidationResultRow>()).AsList();
        var executionStatus = await grid.ReadSingleAsync<string>();
        var results = rows.Select(row => new ValidationResultDto(row.ValidationResultId, row.ValidationDefinitionId, row.ValidationCode, row.StatusCode, row.SeverityCode, row.IsBlocking, row.CanBeWaived, row.WaiverPermissionCode, row.Message, JsonDocument.Parse(row.EvidenceJson).RootElement.Clone())).ToArray();
        return new(executionId, correlationId, executionStatus, results.All(result => result.StatusCode is "PASSED" or "NOT_APPLICABLE"), results.Any(result => result.IsBlocking && result.StatusCode is "FAILED" or "ERROR"), results);
    }

    private sealed record ValidationDefinitionRow(Guid ValidationDefinitionId, string ValidationCode, string ConditionJson, string FailureJson, string SeverityCode, bool IsBlocking, bool CanBeWaived, string? WaiverPermissionCode);
    private sealed record ValidationResultRow(Guid ValidationResultId, Guid ValidationDefinitionId, string ValidationCode, string StatusCode, string SeverityCode, bool IsBlocking, bool CanBeWaived, string? WaiverPermissionCode, string Message, string EvidenceJson);
}
