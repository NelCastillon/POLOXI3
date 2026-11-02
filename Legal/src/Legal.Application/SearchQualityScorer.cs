using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// Enterprise Search Quality Indicator scorer. Fully deterministic and DB-backed: reads the weighted
// rubric, grade bands, and model recommendations seeded by migration 0475 and computes a per-result
// quality assessment from the completed WideSearchResponse. No LLM is involved at any stage — the score,
// grade, factor breakdown, and every suggestion are derived purely from response signals and config rows.
// Entirely fail-soft: any error (config read, missing rows, bad data) returns null so the critical
// /legal/search path and Run History capture are never broken.
public sealed class SearchQualityScorer(
    IIntelligenceWideRepository wideRepository,
    ILogger<SearchQualityScorer> logger):ISearchQualityScorer
{
    public async Task<SearchQualityAssessmentDto?> AssessAsync(WideSearchResponse response,Guid tenantId,string? requestedModelCode,string? contextCode,CancellationToken cancellationToken=default)
    {
        SearchQualityConfig config;
        try
        {
            config=await wideRepository.GetSearchQualityConfigAsync(tenantId,cancellationToken);
        }
        catch(Exception exception)
        {
            // Infrastructure failure (bad SQL, connectivity, schema drift). Fail soft so search still
            // returns, but log at Error so a broken config query is loud instead of silently blanking.
            logger.LogError(exception,"Search quality config load failed for tenant {TenantId}; quality indicator omitted (migration 0475 applied? Core.ConfigurationSetting present?).",tenantId);
            return null;
        }

        try
        {
            if(config.Factors.Count==0||config.GradeBands.Count==0)return null; // scoring disabled / not seeded

            var totalWeight=config.Factors.Sum(f=>f.Weight);
            if(totalWeight<=0)return null;

            var factorScores=new List<SearchQualityFactorScoreDto>(config.Factors.Count);
            decimal weightedSum=0;
            foreach(var factor in config.Factors)
            {
                var raw=ResolveMetric(response,factor.MetricCode);
                var normalized=Normalize(raw,factor.MinValue,factor.MaxValue,factor.HigherIsBetter);
                var contribution=normalized*factor.Weight;
                weightedSum+=contribution;
                factorScores.Add(new SearchQualityFactorScoreDto(factor.FactorCode,factor.DisplayName,factor.Description,raw,normalized,factor.Weight,contribution));
            }

            var score=Math.Clamp(weightedSum/totalWeight,0m,1m);
            var band=config.GradeBands
                .Where(b=>score>=b.MinScore)
                .OrderByDescending(b=>b.MinScore)
                .FirstOrDefault()
                ??config.GradeBands.OrderBy(b=>b.MinScore).First();

            var suggestions=BuildSuggestions(response,config,requestedModelCode,contextCode,score);

            return new SearchQualityAssessmentDto(
                score,
                (int)Math.Round(score*100m,MidpointRounding.AwayFromZero),
                band.Grade,
                band.Label,
                band.ColorHex,
                factorScores,
                suggestions);
        }
        catch(Exception exception)
        {
            logger.LogWarning(exception,"Search quality scoring failed soft for tenant {TenantId}; omitting quality indicator.",tenantId);
            return null;
        }
    }

    // Maps a configured MetricCode
    // mis-seeded factor degrades gracefully to its worst normalized value rather than throwing.
    private static decimal ResolveMetric(WideSearchResponse r,string metricCode)=>metricCode switch
    {
        "DecisionConfidence"      => r.DecisionConfidence??r.FinalConfidence,
        "FinalConfidence"         => r.FinalConfidence,
        "WinnerStability"         => r.WinnerStability??1m,
        "TopKStability"           => r.TopKStability??1m,
        "EvidenceCoverage"        => r.EvidenceCoverage,
        "DecisionEvidenceCoverage"=> r.DecisionEvidenceCoverage,
        "EnterpriseEvidenceCount" => r.EnterpriseEvidenceCount,
        "ExternalEvidenceCount"   => r.ExternalEvidenceCount,
        "CandidateCount"          => r.Candidates?.Count??0,
        "BranchCount"             => r.Branches?.Count??0,
        "DepthReached"            => r.DepthReached,
        "LlmCallCount"            => r.LlmCallCount,
        "IsVerified"              => IsVerified(r.AnswerVerificationCode)?1m:0m,
        _                         => 0m
    };

    private static bool IsVerified(string? verificationCode)=>
        !string.IsNullOrWhiteSpace(verificationCode)
        && !verificationCode.Trim().Equals("INTERPRETIVE",StringComparison.OrdinalIgnoreCase)
        && !verificationCode.Trim().Equals("UNVERIFIED",StringComparison.OrdinalIgnoreCase);

    private static decimal Normalize(decimal raw,decimal min,decimal max,bool higherIsBetter)
    {
        if(max<=min)return 0m;
        var clamped=Math.Clamp(raw,min,max);
        var fraction=(clamped-min)/(max-min);
        return higherIsBetter?fraction:1m-fraction;
    }

    // Deterministic, DB-backed suggestions. Primary suggestion: when a recommendation row maps the run's
    // detected search type (answer kind) to a different/stronger model than the one actually used, advise it.
    private static IReadOnlyCollection<SearchQualitySuggestionDto> BuildSuggestions(
        WideSearchResponse r,SearchQualityConfig config,string? requestedModelCode,string? contextCode,decimal score)
    {
        var suggestions=new List<SearchQualitySuggestionDto>();
        var usedModel=r.ModelCodeUsed?.Trim();

        // Best recommendation for this answer kind (exact match wins over the generic default row).
        var recommendation=config.ModelRecommendations
            .Where(m=>string.IsNullOrEmpty(m.ContextCode)||string.Equals(m.ContextCode,contextCode,StringComparison.OrdinalIgnoreCase))
            .Where(m=>string.Equals(m.AnswerKindCode,r.AnswerKindCode,StringComparison.OrdinalIgnoreCase)||string.IsNullOrEmpty(m.AnswerKindCode))
            .OrderBy(m=>string.IsNullOrEmpty(m.AnswerKindCode)?1:0)
            .ThenBy(m=>m.Priority)
            .FirstOrDefault();

        if(recommendation is not null
           && !string.IsNullOrEmpty(recommendation.RecommendedModelCode)
           && !string.Equals(recommendation.RecommendedModelCode,usedModel,StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(new SearchQualitySuggestionDto(
                "MODEL",
                $"For {DescribeKind(r.AnswerKindCode)} searches, try {recommendation.RecommendedModelLabel}. {recommendation.Rationale}",
                recommendation.RecommendedModelCode,
                recommendation.RecommendedModelLabel));
        }

        // Low grounding hint when enterprise evidence is thin.
        if(r.EnterpriseEvidenceCount==0 && r.EvidenceCoverage<0.5m)
            suggestions.Add(new SearchQualitySuggestionDto(
                "QUERY",
                "This answer leaned on interpretation with little enterprise grounding. Add more specific terms or select a matter/context to retrieve verified evidence."));

        // Unverified-answer hint: the result was not corroborated against retrieved evidence.
        if(!IsVerified(r.AnswerVerificationCode))
            suggestions.Add(new SearchQualitySuggestionDto(
                "QUERY",
                "This result was not verified against retrieved evidence. Narrow the question or scope it to a matter/context so POLOXI can corroborate the answer."));

        // Shallow-reasoning hint: little branching/depth means the comparison explored few dimensions.
        if(r.DepthReached<=1 && (r.Branches?.Count??0)<=1)
            suggestions.Add(new SearchQualitySuggestionDto(
                "GENERAL",
                "The analysis explored only a shallow set of factors. Ask a more comparative or multi-criteria question to drive deeper reasoning."));

        // Overall low-quality nudge.
        if(score<0.5m && suggestions.Count==0)
            suggestions.Add(new SearchQualitySuggestionDto(
                "GENERAL",
                "Result confidence is modest. Refine the question with more detail, or re-run with a stronger model for deeper reasoning."));

        return suggestions;
    }

    private static string DescribeKind(string? answerKindCode)=>answerKindCode?.Trim().ToUpperInvariant() switch
    {
        "ENTITY_RANKING"  => "ranking and comparison",
        "LEGAL_AUTHORITY" => "legal-authority",
        "DECISION"        => "decision",
        "INTERPRETIVE"    => "open interpretive",
        "CONTENT_ENUMERATION" => "enumeration",
        _                 => "this type of"
    };
}
