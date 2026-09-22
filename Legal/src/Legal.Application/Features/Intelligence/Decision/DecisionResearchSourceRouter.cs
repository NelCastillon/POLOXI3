using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;

namespace Legal.Application.Features.Intelligence.Decision;

public sealed class DecisionResearchSourceRouter : IDecisionResearchSourceRouter
{
    public DecisionResearchRoute Route(
        DecisionResearchNeedPersistence researchNeed,
        string? jurisdiction,
        DateTime? authorityCutoffDate = null)
    {
        ArgumentNullException.ThrowIfNull(researchNeed);
        var sourceClass = researchNeed.SourceClassCode?.Trim().ToUpperInvariant();
        var authorityKinds = ParseValues(researchNeed.AuthorityKindsJson);
        var documentTypes = ParseValues(researchNeed.RequiredEvidenceKind);

        if (!researchNeed.IsResearchable ||
            sourceClass == DecisionResearchSourceClasses.None ||
            researchNeed.ApplicationDeferred ||
            researchNeed.ResearchNeedTypeCode.Equals(DecisionResearchNeedTypes.Application, StringComparison.OrdinalIgnoreCase) ||
            researchNeed.ResearchNeedTypeCode.Equals(DecisionResearchNeedTypes.Derived, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                DecisionResearchRouteCodes.NoneDerived,
                DecisionResearchSourceClasses.None,
                researchNeed.ResearchNeedTypeCode,
                false,
                "The accepted need is derived/application work and must be resolved from verified inputs rather than retrieval.",
                jurisdiction,
                [],
                authorityCutoffDate,
                []);
        }

        if (sourceClass == DecisionResearchSourceClasses.MatterDocument)
        {
            return new(
                DecisionResearchRouteCodes.MatterCorpus,
                DecisionResearchSourceClasses.MatterDocument,
                researchNeed.ResearchNeedTypeCode,
                true,
                "The accepted need requires evidence from the tenant-isolated matter corpus.",
                jurisdiction,
                [],
                null,
                documentTypes);
        }

        if (sourceClass == DecisionResearchSourceClasses.LegalAuthority)
        {
            return new(
                DecisionResearchRouteCodes.LegalAuthority,
                DecisionResearchSourceClasses.LegalAuthority,
                researchNeed.ResearchNeedTypeCode,
                true,
                "The accepted need requires external legal authority.",
                jurisdiction,
                authorityKinds,
                authorityCutoffDate,
                []);
        }

        return new(
            DecisionResearchRouteCodes.NoneDerived,
            sourceClass ?? DecisionResearchSourceClasses.None,
            researchNeed.ResearchNeedTypeCode,
            false,
            "The accepted research need has no supported source class.",
            jurisdiction,
            [],
            authorityCutoffDate,
            []);
    }

    private static IReadOnlyCollection<string> ParseValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];
        try
        {
            var values = JsonSerializer.Deserialize<string[]>(value);
            if (values is not null)
                return values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim().ToUpperInvariant()).Distinct().ToArray();
        }
        catch (JsonException)
        {
        }
        return value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.ToUpperInvariant())
            .Distinct()
            .ToArray();
    }
}
