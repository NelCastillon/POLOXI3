namespace Ams.Application.Features.Intelligence;

public sealed record IntelligenceSearchIntent(string? EntityTypeCode,string? ModuleCode,bool OrderByRecency,bool IsEntityList,string SearchText,string SourceEngineCode="DB_PATTERN",decimal Confidence=1,string? PatternCode=null);

public static class IntelligenceSearchIntentInterpreter
{
    public static IntelligenceSearchIntent Interpret(string query,IReadOnlyCollection<IntelligenceSearchIntentPatternDto> patterns)
    {
        var trimmed=query.Trim();
        var normalized=$" {trimmed.ToLowerInvariant()} ";
        var orderByRecency=patterns.Where(pattern=>pattern.ExtractionStrategyCode.Equals("RECENCY",StringComparison.OrdinalIgnoreCase)).SelectMany(pattern=>pattern.MatchPhrases).Any(phrase=>ContainsPhrase(normalized,phrase));

        foreach(var pattern in patterns.Where(pattern=>!pattern.ExtractionStrategyCode.Equals("RECENCY",StringComparison.OrdinalIgnoreCase)).OrderBy(pattern=>pattern.Priority))
        {
            if(pattern.MatchPhrases.Count>0&&!pattern.MatchPhrases.All(phrase=>ContainsPhrase(normalized,phrase)))continue;
            var searchText=ExtractSearchText(trimmed,pattern);
            return new(pattern.EntityTypeCode,pattern.ModuleCode,orderByRecency,pattern.IsEntityList,searchText,"DB_PATTERN",1,pattern.PatternCode);
        }

        return new(null,null,orderByRecency,false,trimmed,"NONE",0);
    }

    private static bool ContainsPhrase(string normalizedQuery,string phrase)=>normalizedQuery.Contains($" {phrase.Trim().ToLowerInvariant()} ",StringComparison.OrdinalIgnoreCase);

    private static string ExtractSearchText(string query,IntelligenceSearchIntentPatternDto pattern)
    {
        var cleaned=query.Trim().TrimEnd('?','.','!');
        if(pattern.ExtractionStrategyCode.Equals("PREFIX",StringComparison.OrdinalIgnoreCase))
        {
            foreach(var phrase in pattern.ExtractionPhrases.OrderByDescending(phrase=>phrase.Length))
                if(cleaned.StartsWith(phrase,StringComparison.OrdinalIgnoreCase))return cleaned[phrase.Length..].Trim();
        }
        if(pattern.ExtractionStrategyCode.Equals("AFTER_MARKER",StringComparison.OrdinalIgnoreCase))
        {
            foreach(var phrase in pattern.ExtractionPhrases.OrderByDescending(phrase=>phrase.Length))
            {
                var marker=cleaned.LastIndexOf(phrase,StringComparison.OrdinalIgnoreCase);
                if(marker>=0)return cleaned[(marker+phrase.Length)..].Trim();
            }
        }
        return cleaned;
    }
}
