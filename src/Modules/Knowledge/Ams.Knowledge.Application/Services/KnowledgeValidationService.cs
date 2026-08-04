using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Common.Validation;
using Ams.Knowledge.Contracts.Validation;

namespace Ams.Knowledge.Application.Services;

public sealed class KnowledgeValidationService : IKnowledgeValidationService
{
    private readonly IKnowledgeValidationRuleRepository _repository;
    private readonly IKnowledgeValidationPolicyProvider _policyProvider;
    private readonly ISemanticRuleEvaluator _evaluator;

    public KnowledgeValidationService(IKnowledgeValidationRuleRepository repository, IKnowledgeValidationPolicyProvider policyProvider, ISemanticRuleEvaluator evaluator)
    {
        _repository = repository;
        _policyProvider = policyProvider;
        _evaluator = evaluator;
    }

    public async Task<SemanticValidationResult> ValidateAsync(SemanticValidationRequest request, CancellationToken cancellationToken = default)
    {
        RequestValidator.Validate(request);
        if (request.TenantId == Guid.Empty || request.AppliesToConceptId == Guid.Empty || request.EntityId == Guid.Empty)
            throw new ApplicationValidationException(["Tenant, concept, and entity identifiers are required."]);

        var rules = await _repository.GetEffectiveRulesAsync(request.TenantId, request.AppliesToConceptId, request.EffectiveUtc, cancellationToken);
        var blockingSeverityCodes = await _policyProvider.GetBlockingSeverityCodesAsync(request.TenantId, cancellationToken);
        var issues = rules.Select(rule => _evaluator.Evaluate(rule, request)).Where(issue => issue is not null).Cast<SemanticValidationIssue>().ToArray();
        var hasBlockingIssues = issues.Any(issue => blockingSeverityCodes.Contains(issue.SeverityCode));
        return new SemanticValidationResult(issues.Length == 0, hasBlockingIssues, issues);
    }
}
