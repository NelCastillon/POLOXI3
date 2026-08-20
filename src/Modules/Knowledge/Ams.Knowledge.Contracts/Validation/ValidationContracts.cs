namespace Ams.Knowledge.Contracts.Validation;

public sealed record SemanticPropertyValue(string PropertyPath, IReadOnlyCollection<string> Values);

public sealed record SemanticValidationRequest(
    Guid TenantId,
    Guid AppliesToConceptId,
    string EntityTypeCode,
    Guid EntityId,
    IReadOnlyCollection<SemanticPropertyValue> Properties,
    DateTime EffectiveUtc);

public sealed record SemanticValidationIssue(
    Guid RuleId,
    string RuleCode,
    string SeverityCode,
    string Message,
    string? PropertyPath);

public sealed record SemanticValidationResult(
    bool IsValid,
    bool HasBlockingIssues,
    IReadOnlyCollection<SemanticValidationIssue> Issues);

public interface IKnowledgeValidationService
{
    Task<SemanticValidationResult> ValidateAsync(SemanticValidationRequest request, CancellationToken cancellationToken = default);
}
