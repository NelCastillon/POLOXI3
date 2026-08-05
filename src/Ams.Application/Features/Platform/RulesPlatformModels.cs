using System.Text.Json;

namespace Ams.Application.Features.Platform;

public sealed record EvaluateRulesRequest(
    Guid TenantId,
    string EntityTypeCode,
    Guid EntityId,
    string? ModuleCode,
    string CorrelationId,
    JsonElement Facts,
    Guid? ActorUserId);

public sealed record RuleEvaluationResult(
    Guid RuleExecutionId,
    Guid RuleDefinitionId,
    string RuleCode,
    int VersionNumber,
    string StatusCode,
    bool? IsMatch,
    string SeverityCode,
    bool StopsProcessing,
    JsonElement Outcome,
    string? ErrorMessage);

public sealed record RulesEvaluationResponse(
    string CorrelationId,
    bool StoppedProcessing,
    IReadOnlyCollection<RuleEvaluationResult> Results);
