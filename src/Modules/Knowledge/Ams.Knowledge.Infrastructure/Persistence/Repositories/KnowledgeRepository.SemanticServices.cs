using System.Globalization;
using System.Text.Json;
using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Contracts.Concepts;
using Ams.Knowledge.Contracts.Hierarchy;
using Ams.Knowledge.Contracts.Mappings;
using Dapper;
using Microsoft.Extensions.Caching.Memory;

namespace Ams.Knowledge.Infrastructure.Persistence.Repositories;

public sealed partial class KnowledgeRepository
{
    public Task<IReadOnlyCollection<ConceptCandidate>> FindApprovedExternalCandidatesAsync(ConceptResolutionRequest request, CancellationToken cancellationToken = default)
        => FindCandidatesAsync(request, "EXTERNAL", "CONFIDENCE_EXACT_EXTERNAL_CODE", request.Input, cancellationToken);

    public Task<IReadOnlyCollection<ConceptCandidate>> FindPreferredLabelCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default)
        => FindCandidatesAsync(request, "PREFERRED", "CONFIDENCE_EXACT_PREFERRED_LABEL", normalizedInput, cancellationToken);

    public Task<IReadOnlyCollection<ConceptCandidate>> FindApprovedLabelCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default)
        => FindCandidatesAsync(request, "LABEL", "CONFIDENCE_EXACT_APPROVED_SYNONYM", normalizedInput, cancellationToken);

    public Task<IReadOnlyCollection<ConceptCandidate>> FindContextualCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, CancellationToken cancellationToken = default)
        => FindCandidatesAsync(request, "CONTEXT", "CONFIDENCE_CONTEXT_CARRIER_TERM", normalizedInput, cancellationToken);

    public async Task<IReadOnlyCollection<ConceptCandidate>> FindFuzzyCandidatesAsync(ConceptResolutionRequest request, string normalizedInput, int maximumCandidates, CancellationToken cancellationToken = default)
    {
        var confidence = await GetDecimalConfigurationAsync(request.TenantId, "CONFIDENCE_FUZZY", cancellationToken);
        const string sql = """
SELECT TOP (@MaximumCandidates) concept.KnowledgeConceptId AS ConceptId, concept.ConceptCode, concept.PreferredLabel,
       concept.VersionNumber, @Confidence AS Confidence, N'FUZZY' AS MatchReasonCode
FROM knowledge.ConceptLabel label
INNER JOIN knowledge.KnowledgeConcept concept ON concept.KnowledgeConceptId = label.KnowledgeConceptId
INNER JOIN knowledge.ConceptScheme scheme ON scheme.ConceptSchemeId = concept.ConceptSchemeId
WHERE label.IsDeleted = 0 AND label.IsDeprecated = 0 AND label.IsSearchable = 1
  AND concept.IsDeleted = 0 AND concept.StatusCode = N'PUBLISHED'
  AND (concept.TenantId IS NULL OR concept.TenantId = @TenantId)
  AND (@ConceptSchemeCode IS NULL OR scheme.SchemeCode = @ConceptSchemeCode)
  AND (DIFFERENCE(label.NormalizedLabel, @NormalizedInput) >= 3 OR label.NormalizedLabel LIKE '%' + @NormalizedInput + '%')
ORDER BY DIFFERENCE(label.NormalizedLabel, @NormalizedInput) DESC, LEN(label.NormalizedLabel), concept.PreferredLabel;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ConceptCandidate>(new CommandDefinition(sql, new { MaximumCandidates = Math.Clamp(maximumCandidates, 1, 50), Confidence = confidence, request.TenantId, request.ConceptSchemeCode, NormalizedInput = normalizedInput }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<KnowledgeResolutionPolicy> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"knowledge:resolution-policy:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out KnowledgeResolutionPolicy? cached) && cached is not null)
            return cached;

        var policy = new KnowledgeResolutionPolicy(
            await GetDecimalConfigurationAsync(tenantId, "RESOLUTION_AUTO_THRESHOLD", cancellationToken),
            await GetDecimalConfigurationAsync(tenantId, "RESOLUTION_REVIEW_THRESHOLD", cancellationToken),
            await GetIntegerConfigurationAsync(tenantId, "RESOLUTION_MAX_CANDIDATES", cancellationToken));
        _cache.Set(cacheKey, policy, TimeSpan.FromMinutes(5));
        return policy;
    }

    public async Task<bool> IsDescendantOfAsync(Guid tenantId, Guid conceptId, Guid ancestorConceptId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT COUNT(1)
FROM knowledge.ConceptHierarchyClosure closure
INNER JOIN knowledge.KnowledgeConcept descendant ON descendant.KnowledgeConceptId = closure.DescendantConceptId
INNER JOIN knowledge.KnowledgeConcept ancestor ON ancestor.KnowledgeConceptId = closure.AncestorConceptId
WHERE closure.DescendantConceptId = @ConceptId AND closure.AncestorConceptId = @AncestorConceptId
  AND (descendant.TenantId IS NULL OR descendant.TenantId = @TenantId)
  AND (ancestor.TenantId IS NULL OR ancestor.TenantId = @TenantId);
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { TenantId = tenantId, ConceptId = conceptId, AncestorConceptId = ancestorConceptId }, cancellationToken: cancellationToken)) > 0;
    }

    public Task<IReadOnlyCollection<ConceptHierarchyNode>> GetAncestorsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default)
        => GetHierarchyAsync(tenantId, conceptId, ancestors: true, cancellationToken);

    public Task<IReadOnlyCollection<ConceptHierarchyNode>> GetDescendantsAsync(Guid tenantId, Guid conceptId, CancellationToken cancellationToken = default)
        => GetHierarchyAsync(tenantId, conceptId, ancestors: false, cancellationToken);

    public async Task<ExternalMappingResult?> ResolveApprovedAsync(ExternalMappingRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP (1) mapping.ExternalConceptMappingId, concept.KnowledgeConceptId AS ConceptId, concept.ConceptCode,
       concept.PreferredLabel, concept.VersionNumber AS ConceptVersionNumber, COALESCE(mapping.ConfidenceScore, 1.0) AS Confidence,
       mapping.MatchTypeCode, mapping.IsApproved
FROM knowledge.ExternalConceptMapping mapping
INNER JOIN knowledge.KnowledgeConcept concept ON concept.KnowledgeConceptId = mapping.KnowledgeConceptId
WHERE mapping.TenantId = @TenantId AND mapping.IsDeleted = 0 AND mapping.IsApproved = 1
  AND mapping.SourceSystemTypeCode = @SourceSystemTypeCode
  AND (@SourceSystemId IS NULL OR mapping.SourceSystemId = @SourceSystemId)
  AND ((@ExternalCode IS NOT NULL AND mapping.ExternalCode = @ExternalCode) OR mapping.NormalizedExternalValue = UPPER(LTRIM(RTRIM(@ExternalValue))))
  AND (@ExternalPath IS NULL OR mapping.ExternalPath = @ExternalPath)
  AND (@CarrierProductId IS NULL OR mapping.CarrierProductId IS NULL OR mapping.CarrierProductId = @CarrierProductId)
  AND (@StateCode IS NULL OR mapping.StateCode IS NULL OR mapping.StateCode = @StateCode)
  AND (@LineOfBusinessConceptId IS NULL OR mapping.LineOfBusinessConceptId IS NULL OR mapping.LineOfBusinessConceptId = @LineOfBusinessConceptId)
  AND mapping.EffectiveFromUtc <= @EffectiveUtc AND (mapping.EffectiveToUtc IS NULL OR mapping.EffectiveToUtc > @EffectiveUtc)
  AND concept.IsDeleted = 0 AND concept.StatusCode = N'PUBLISHED'
ORDER BY CASE WHEN mapping.ExternalCode = @ExternalCode THEN 0 ELSE 1 END,
         CASE WHEN mapping.CarrierProductId = @CarrierProductId THEN 0 ELSE 1 END,
         CASE WHEN mapping.StateCode = @StateCode THEN 0 ELSE 1 END,
         mapping.ConfidenceScore DESC, mapping.ApprovedDateUtc DESC;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ExternalMappingResult>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyCollection<SemanticValidationRuleDefinition>> GetEffectiveRulesAsync(Guid tenantId, Guid appliesToConceptId, DateTime effectiveUtc, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT ConceptValidationRuleId AS RuleId, RuleCode, RuleTypeCode, PropertyPath, OperatorCode, ExpectedValue,
       MinimumCount, MaximumCount, SeverityCode, Message
FROM knowledge.ConceptValidationRule
WHERE AppliesToConceptId = @AppliesToConceptId AND IsDeleted = 0 AND StatusCode = N'PUBLISHED'
  AND (TenantId IS NULL OR TenantId = @TenantId)
  AND EffectiveFromUtc <= @EffectiveUtc AND (EffectiveToUtc IS NULL OR EffectiveToUtc > @EffectiveUtc)
ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END, RuleCode;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<SemanticValidationRuleDefinition>(new CommandDefinition(sql, new { TenantId = tenantId, AppliesToConceptId = appliesToConceptId, EffectiveUtc = effectiveUtc }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlySet<string>> GetBlockingSeverityCodesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"knowledge:validation-policy:{tenantId}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlySet<string>? cached) && cached is not null)
            return cached;

        var json = await GetConfigurationAsync(tenantId, "VALIDATION_BLOCKING_SEVERITIES", cancellationToken);
        var values = JsonSerializer.Deserialize<string[]>(json)
            ?? throw new InvalidOperationException("The Knowledge blocking-severity configuration is invalid.");
        IReadOnlySet<string> result = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    private async Task<IReadOnlyCollection<ConceptCandidate>> FindCandidatesAsync(ConceptResolutionRequest request, string mode, string confidenceCode, string input, CancellationToken cancellationToken)
    {
        var confidence = await GetDecimalConfigurationAsync(request.TenantId, confidenceCode, cancellationToken);
        const string sql = """
SELECT DISTINCT concept.KnowledgeConceptId AS ConceptId, concept.ConceptCode, concept.PreferredLabel, concept.VersionNumber,
       @Confidence AS Confidence,
       CASE @Mode WHEN N'EXTERNAL' THEN N'EXACT_EXTERNAL_CODE' WHEN N'PREFERRED' THEN N'EXACT_PREFERRED_LABEL' WHEN N'LABEL' THEN N'EXACT_APPROVED_SYNONYM' ELSE N'CONTEXT_CARRIER_TERM' END AS MatchReasonCode
FROM knowledge.KnowledgeConcept concept
INNER JOIN knowledge.ConceptScheme scheme ON scheme.ConceptSchemeId = concept.ConceptSchemeId
LEFT JOIN knowledge.ConceptLabel label ON label.KnowledgeConceptId = concept.KnowledgeConceptId AND label.IsDeleted = 0 AND label.IsDeprecated = 0 AND label.IsSearchable = 1
LEFT JOIN knowledge.ExternalConceptMapping mapping ON mapping.KnowledgeConceptId = concept.KnowledgeConceptId AND mapping.TenantId = @TenantId AND mapping.IsDeleted = 0 AND mapping.IsApproved = 1
WHERE concept.IsDeleted = 0 AND concept.StatusCode = N'PUBLISHED' AND (concept.TenantId IS NULL OR concept.TenantId = @TenantId)
  AND (@ConceptSchemeCode IS NULL OR scheme.SchemeCode = @ConceptSchemeCode)
  AND
  (
      (@Mode = N'EXTERNAL' AND @CarrierId IS NOT NULL AND mapping.SourceSystemId = @CarrierId AND (mapping.ExternalCode = @Input OR mapping.NormalizedExternalValue = UPPER(LTRIM(RTRIM(@Input)))))
   OR (@Mode = N'PREFERRED' AND concept.NormalizedPreferredLabel = @Input)
   OR (@Mode = N'LABEL' AND label.NormalizedLabel = @Input AND label.LabelTypeCode <> N'CARRIER_TERM')
   OR (@Mode = N'CONTEXT' AND label.NormalizedLabel = @Input AND label.LabelTypeCode = N'CARRIER_TERM'
       AND (@CarrierId IS NULL OR mapping.SourceSystemId = @CarrierId)
       AND (@CarrierProductId IS NULL OR mapping.CarrierProductId IS NULL OR mapping.CarrierProductId = @CarrierProductId)
       AND (@StateCode IS NULL OR mapping.StateCode IS NULL OR mapping.StateCode = @StateCode)
       AND (@LineOfBusinessConceptId IS NULL OR mapping.LineOfBusinessConceptId IS NULL OR mapping.LineOfBusinessConceptId = @LineOfBusinessConceptId))
  );
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ConceptCandidate>(new CommandDefinition(sql, new { request.TenantId, request.ConceptSchemeCode, request.CarrierId, request.CarrierProductId, request.StateCode, request.LineOfBusinessConceptId, Mode = mode, Input = input, Confidence = confidence }, cancellationToken: cancellationToken))).AsList();
    }

    private async Task<IReadOnlyCollection<ConceptHierarchyNode>> GetHierarchyAsync(Guid tenantId, Guid conceptId, bool ancestors, CancellationToken cancellationToken)
    {
        var sql = ancestors
            ? """SELECT concept.KnowledgeConceptId AS ConceptId, concept.ConceptCode, concept.PreferredLabel, closure.Depth FROM knowledge.ConceptHierarchyClosure closure INNER JOIN knowledge.KnowledgeConcept concept ON concept.KnowledgeConceptId = closure.AncestorConceptId WHERE closure.DescendantConceptId = @ConceptId AND (concept.TenantId IS NULL OR concept.TenantId = @TenantId) ORDER BY closure.Depth, concept.PreferredLabel;"""
            : """SELECT concept.KnowledgeConceptId AS ConceptId, concept.ConceptCode, concept.PreferredLabel, closure.Depth FROM knowledge.ConceptHierarchyClosure closure INNER JOIN knowledge.KnowledgeConcept concept ON concept.KnowledgeConceptId = closure.DescendantConceptId WHERE closure.AncestorConceptId = @ConceptId AND (concept.TenantId IS NULL OR concept.TenantId = @TenantId) ORDER BY closure.Depth, concept.PreferredLabel;""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<ConceptHierarchyNode>(new CommandDefinition(sql, new { TenantId = tenantId, ConceptId = conceptId }, cancellationToken: cancellationToken))).AsList();
    }

    private async Task<string> GetConfigurationAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        const string sql = """
SELECT TOP (1) ConfigurationValue
FROM knowledge.Configuration
WHERE ConfigurationCode = @Code AND IsActive = 1 AND (TenantId IS NULL OR TenantId = @TenantId)
ORDER BY CASE WHEN TenantId = @TenantId THEN 0 ELSE 1 END;
""";
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql, new { TenantId = tenantId, Code = code }, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Required Knowledge configuration '{code}' was not found.");
    }

    private async Task<decimal> GetDecimalConfigurationAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        var value = await GetConfigurationAsync(tenantId, code, cancellationToken);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidOperationException($"Knowledge configuration '{code}' is not a valid decimal.");
    }

    private async Task<int> GetIntegerConfigurationAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        var value = await GetConfigurationAsync(tenantId, code, cancellationToken);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidOperationException($"Knowledge configuration '{code}' is not a valid integer.");
    }
}
