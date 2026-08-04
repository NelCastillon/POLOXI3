using Ams.Knowledge.Domain.Common;

namespace Ams.Knowledge.Domain.Rules;

public sealed class ConceptValidationRule : KnowledgeRecord
{
    public ConceptValidationRule(Guid id, Guid appliesToConceptId, string ruleCode, string ruleTypeCode, string? propertyPath, string operatorCode, string? expectedValue, int? minimumCount, int? maximumCount, string severityCode, string message, DateTime effectiveFromUtc, DateTime? effectiveToUtc, string statusCode, Guid? tenantId, bool isSystemDefined, Guid createdByUserId, DateTime createdUtc)
        : base(id, tenantId, isSystemDefined, createdByUserId, createdUtc)
    {
        if (appliesToConceptId == Guid.Empty)
            throw new KnowledgeDomainException("AppliesToConceptId is required.");
        if (minimumCount < 0 || maximumCount < 0 || minimumCount > maximumCount)
            throw new KnowledgeDomainException("Validation count bounds are invalid.");
        KnowledgeGuard.EffectiveDates(effectiveFromUtc, effectiveToUtc);

        AppliesToConceptId = appliesToConceptId;
        RuleCode = KnowledgeGuard.Code(ruleCode, "RuleCode", 100);
        RuleTypeCode = KnowledgeGuard.Code(ruleTypeCode, "RuleTypeCode", 50);
        PropertyPath = propertyPath?.Trim();
        OperatorCode = KnowledgeGuard.Code(operatorCode, "OperatorCode", 50);
        ExpectedValue = expectedValue;
        MinimumCount = minimumCount;
        MaximumCount = maximumCount;
        SeverityCode = KnowledgeGuard.Code(severityCode, "SeverityCode", 30);
        Message = KnowledgeGuard.Required(message, "Message", 1000);
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        StatusCode = KnowledgeGuard.Code(statusCode, "StatusCode", 30);
    }

    public Guid AppliesToConceptId { get; }
    public string RuleCode { get; }
    public string RuleTypeCode { get; }
    public string? PropertyPath { get; }
    public string OperatorCode { get; }
    public string? ExpectedValue { get; }
    public int? MinimumCount { get; }
    public int? MaximumCount { get; }
    public string SeverityCode { get; }
    public string Message { get; }
    public DateTime EffectiveFromUtc { get; }
    public DateTime? EffectiveToUtc { get; }
    public string StatusCode { get; }
}
