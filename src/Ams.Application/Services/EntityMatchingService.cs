using System.ComponentModel.DataAnnotations;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Features.SearchMatching;

namespace Ams.Application.Services;

public sealed class EntityMatchingService(ISearchMatchingRepository repository, ISemanticQueryExpander? semanticQueryExpander = null) : IEntityMatchingService
{
    private readonly ISemanticQueryExpander _semanticQueryExpander = semanticQueryExpander ?? new NullSemanticQueryExpander();
    public async Task<EntityMatchResult> FindModuleMatchesAsync(ModuleMatchRequest request, CancellationToken cancellationToken = default)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        if (request.TenantId == Guid.Empty) throw new ValidationException("TenantId is required.");
        var policy = await repository.GetPolicyAsync(request.TenantId, request.ProfileCode, cancellationToken)
            ?? throw new InvalidOperationException($"Active matching profile '{request.ProfileCode}' was not found for the tenant.");
        return await FindMatchesAsync(new EntityMatchRequest
        {
            TenantId = request.TenantId,
            ProfileCode = policy.ProfileCode,
            EntityTypeCode = policy.EntityTypeCode,
            SourceEntityId = request.SourceEntityId,
            CorrelationId = request.CorrelationId,
            RequestedByUserId = request.RequestedByUserId,
            Fields = request.Fields
        }, cancellationToken);
    }

    public async Task<EntityMatchResult> FindMatchesAsync(EntityMatchRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var policy = await repository.GetPolicyAsync(request.TenantId, request.ProfileCode, cancellationToken)
            ?? throw new InvalidOperationException($"Active matching profile '{request.ProfileCode}' was not found for the tenant.");
        if (!policy.EntityTypeCode.Equals(request.EntityTypeCode, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("The requested entity type does not match the configured profile.");

        var executionId = await repository.BeginExecutionAsync(request, policy, cancellationToken);
        try
        {
            var projections = await repository.GetCandidatesAsync(request.TenantId, request.EntityTypeCode, request.Fields, policy.MaximumCandidates, cancellationToken);
            var candidates = projections.Where(projection => projection.EntityId != request.SourceEntityId).Select(projection => Score(request, projection, policy, []))
                .Where(candidate => candidate.OverallScore >= policy.PossibleThreshold)
                .OrderByDescending(candidate => candidate.OverallScore)
                .ThenBy(candidate => candidate.DisplayName)
                .Take(policy.MaximumCandidates)
                .ToList();
            await repository.CompleteExecutionAsync(executionId, candidates, cancellationToken);
            return new(executionId, policy.ProfileCode, policy.ExactThreshold, policy.StrongThreshold, policy.PossibleThreshold, candidates);
        }
        catch (Exception ex)
        {
            await repository.FailExecutionAsync(executionId, ex.Message, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<SearchMatchResult>> SearchAsync(EnterpriseFuzzySearchRequest request, CancellationToken cancellationToken = default)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        var policy = await repository.GetPolicyAsync(request.TenantId, MatchProfileCodes.GlobalEnterpriseSearch, cancellationToken)
            ?? throw new InvalidOperationException("The global enterprise search profile is not configured.");
        var expansion = await _semanticQueryExpander.ExpandAsync(request.TenantId, request.Query, policy.SemanticMaximumConcepts, cancellationToken);
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? $"search:{Guid.NewGuid():N}" : request.CorrelationId.Trim();
        await repository.SaveSemanticEvidenceAsync(request.TenantId, request.RequestedByUserId, correlationId, request.Query, expansion.Terms, expansion.Concepts, cancellationToken);
        var retrievalQuery = string.Join(' ', new[] { request.Query }.Concat(expansion.Terms).Distinct(StringComparer.OrdinalIgnoreCase));
        var projections = await repository.SearchProjectionsAsync(request.TenantId, retrievalQuery, request.EntityTypeCodes, request.GrantedPermissions, Math.Min(request.MaximumResults, policy.MaximumCandidates), cancellationToken);
        var fields = new Dictionary<string, string?> { ["DisplayName"] = request.Query, ["SearchText"] = request.Query };
        var matchRequest = new EntityMatchRequest { TenantId = request.TenantId, ProfileCode = policy.ProfileCode, EntityTypeCode = policy.EntityTypeCode, CorrelationId = correlationId, RequestedByUserId = request.RequestedByUserId, Fields = fields };
        return projections.Select(projection => Score(matchRequest, projection, policy, expansion.Terms))
            .Where(candidate => candidate.OverallScore >= policy.PossibleThreshold)
            .OrderByDescending(candidate => candidate.OverallScore)
            .Take(request.MaximumResults)
            .Select(candidate => new SearchMatchResult(candidate.EntityId, projections.First(p => p.EntityId == candidate.EntityId).EntityTypeCode, candidate.DisplayName, candidate.SecondaryText, candidate.NavigationRoute, candidate.OverallScore, candidate.Reasons))
            .ToList();
    }

    private static MatchCandidate Score(EntityMatchRequest request, MatchProjection projection, MatchPolicy policy, IReadOnlyCollection<string> semanticTerms)
    {
        var reasons = new List<MatchReason>();
        decimal achievedWeight = 0;
        decimal consideredWeight = 0;
        var criticalDiscrepancy = false;
        var requiredFieldMissing = false;

        foreach (var field in policy.Fields.Where(field => field.IsRequired))
        {
            request.Fields.TryGetValue(field.FieldCode, out var inputValue);
            projection.Fields.TryGetValue(field.FieldCode, out var candidateValue);
            if (!string.IsNullOrWhiteSpace(inputValue) && !string.IsNullOrWhiteSpace(candidateValue)) continue;
            requiredFieldMissing = true;
            reasons.Add(new(field.FieldCode, field.AlgorithmCode, 0, 0, "REQUIRED_FIELD_MISSING", field.IsSensitive ? $"Required {field.DisplayName} comparison could not be completed securely." : $"Required {field.DisplayName} is missing.", false, true));
        }

        foreach (var field in policy.Fields.Where(field => field.IsActiveFor(request.Fields, projection.Fields)))
        {
            request.Fields.TryGetValue(field.FieldCode, out var inputValue);
            projection.Fields.TryGetValue(field.FieldCode, out var candidateValue);
            if (string.IsNullOrWhiteSpace(inputValue) || string.IsNullOrWhiteSpace(candidateValue)) continue;

            consideredWeight += field.Weight;
            var comparisonInput = field.AlgorithmCode.Equals("SEMANTIC_ADVISORY", StringComparison.OrdinalIgnoreCase) && semanticTerms.Count > 0
                ? string.Join(' ', semanticTerms)
                : inputValue;
            var similarity = SearchMatchingAlgorithms.Similarity(field.AlgorithmCode, comparisonInput, candidateValue, policy.EntityTypeCode, field.FieldCode, policy.NormalizationTerms);
            var discrepancy = field.IsCriticalIdentifier && similarity < 100;
            if (discrepancy) criticalDiscrepancy = true;
            var qualifies = similarity >= field.MinimumSimilarity && (!field.ExactMatchOnly || similarity == 100);
            var weighted = qualifies ? field.Weight * similarity / 100m : 0;
            achievedWeight += weighted;
            reasons.Add(new(field.FieldCode, field.AlgorithmCode, similarity, weighted, discrepancy ? "CRITICAL_DISCREPANCY" : qualifies ? "MATCH_SIGNAL" : "BELOW_FIELD_THRESHOLD", field.IsSensitive ? $"{field.DisplayName} comparison was evaluated securely." : $"{field.DisplayName} similarity is {similarity:0.##}%.", similarity == 100, discrepancy));
        }

        var score = criticalDiscrepancy || requiredFieldMissing || consideredWeight == 0 ? 0 : Math.Round(achievedWeight / consideredWeight * 100m, 4);
        var band = score >= policy.ExactThreshold ? "EXACT" : score >= policy.StrongThreshold ? "STRONG" : score >= policy.PossibleThreshold ? "POSSIBLE" : "BELOW_THRESHOLD";
        return new(projection.EntityId, projection.DisplayName, projection.SecondaryText, projection.NavigationRoute, score, band, reasons, band == "EXACT", policy.RequiresReview);
    }

    private static void Validate(EntityMatchRequest request)
    {
        Validator.ValidateObject(request, new ValidationContext(request), true);
        if (request.TenantId == Guid.Empty) throw new ValidationException("TenantId is required.");
    }
}

internal static class MatchFieldPolicyExtensions
{
    public static bool IsActiveFor(this MatchFieldPolicy policy, IReadOnlyDictionary<string, string?> requestFields, IReadOnlyDictionary<string, string?> candidateFields)
        => requestFields.ContainsKey(policy.FieldCode) && candidateFields.ContainsKey(policy.FieldCode);
}
