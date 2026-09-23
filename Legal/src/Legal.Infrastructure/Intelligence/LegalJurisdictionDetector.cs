using System.Text.RegularExpressions;

namespace Legal.Infrastructure.Intelligence;

public sealed class LegalJurisdictionDetector:ILegalJurisdictionDetector
{
    private static readonly Regex ExplicitJurisdiction=new(
        @"\bjurisdiction\s+(?<jurisdiction>[\p{L}][\p{L} .'-]{1,100})$",
        RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex NamedCodeCitation=new(
        @"\b(?<jurisdiction>[A-Z][A-Za-z.]{1,19}(?:\s+[A-Z][A-Za-z.]{1,19}){0,4})\s+(?<authority>Code|Codes|Statutes?|Regulations?|Administrative\s+Code|Rev(?:ised)?\.?\s+Stat(?:utes?)?\.?)\s*(?:§+|section|sec\.?)?\s*(?<section>[A-Za-z0-9][A-Za-z0-9.:-]{0,30})\b",
        RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex ReporterStyleCodeCitation=new(
        @"\b(?<title>\d{1,3})\s+(?<jurisdiction>[A-Z][A-Za-z.]{1,12})\s+(?<authority>C(?:ode)?\.?)\s*§+\s*(?<section>[A-Za-z0-9][A-Za-z0-9.:-]{0,30})\b",
        RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

    public bool TryDetect(string query,out LegalJurisdictionCitation citation)
    {
        citation=new(string.Empty,string.Empty,string.Empty);
        if(string.IsNullOrWhiteSpace(query))return false;
        var explicitJurisdiction=ExplicitJurisdiction.Match(query.Trim());
        var searchText=explicitJurisdiction.Success?query[..explicitJurisdiction.Index].Trim():query;
        if(explicitJurisdiction.Success)
        {
            var display=Regex.Replace(explicitJurisdiction.Groups["jurisdiction"].Value.Trim(),@"\s+"," ");
            var explicitMatches=NamedCodeCitation.Matches(searchText).Cast<Match>()
                .Concat(ReporterStyleCodeCitation.Matches(searchText).Cast<Match>())
                .GroupBy(match=>match.Value,StringComparer.OrdinalIgnoreCase)
                .Select(group=>group.First())
                .ToList();
            if(explicitMatches.Count==1)
            {
                var explicitMatch=explicitMatches[0];
                var citationText=TrimCitationToJurisdiction(explicitMatch.Value,explicitMatch.Groups["authority"].Value,display);
                citation=CreateCitation(display,explicitMatch,citationText);
                return true;
            }
        }
        var matches=NamedCodeCitation.Matches(searchText).Cast<Match>()
            .Concat(ReporterStyleCodeCitation.Matches(searchText).Cast<Match>())
            .GroupBy(match=>match.Value,StringComparer.OrdinalIgnoreCase)
            .Select(group=>group.First())
            .ToList();
        if(matches.Count!=1)return false;
        var match=matches[0];
        var jurisdiction=NormalizeJurisdiction(match.Groups["jurisdiction"].Value);
        if(string.IsNullOrWhiteSpace(jurisdiction))return false;
        citation=CreateCitation(match.Groups["jurisdiction"].Value,match);
        return true;
    }

    private static LegalJurisdictionCitation CreateCitation(string jurisdictionValue,Match match,string? citationText=null)
    {
        var jurisdiction=NormalizeJurisdiction(jurisdictionValue);
        var authority=match.Groups["authority"].Value.Contains("reg",StringComparison.OrdinalIgnoreCase)||
                      match.Groups["authority"].Value.Contains("administrative",StringComparison.OrdinalIgnoreCase)
            ?"REGULATION"
            :"STATUTE";
        return new(jurisdiction,authority,citationText??match.Value.Trim());
    }

    private static string TrimCitationToJurisdiction(string citation,string authority,string jurisdiction)
    {
        var marker=$"{jurisdiction} {authority}";
        var index=citation.LastIndexOf(marker,StringComparison.OrdinalIgnoreCase);
        return index>=0?citation[index..].Trim():citation.Trim();
    }

    private static string NormalizeJurisdiction(string value) =>
        $"NAME:{Regex.Replace(value.Trim(),@"\s+"," ").ToUpperInvariant()}";
}
