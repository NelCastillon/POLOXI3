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
    // California (and other states) use NAMED codes — "California Code of Civil Procedure section 581",
    // "California Civil Code section 1714" — which do NOT fit the generic "[Jurisdiction] Code § N" shape
    // (the word "Code" is embedded inside a multi-word code name). Without this, CA statute citations
    // mis-parse (section captured as "of") and never reach the statute resolver, so they stay UNVERIFIED.
    private static readonly Regex CaliforniaNamedCodeCitation=new(
        @"\b(?<jurisdiction>California|Cal\.?)\s+(?<authority>Code\s+of\s+Civil\s+Procedure|Civil\s+Code|Penal\s+Code|Evidence\s+Code|Vehicle\s+Code|Health\s+and\s+Safety\s+Code|Business\s+and\s+Professions\s+Code|Probate\s+Code|Family\s+Code|Labor\s+Code|Government\s+Code|Welfare\s+and\s+Institutions\s+Code|Corporations\s+Code|Insurance\s+Code|Revenue\s+and\s+Taxation\s+Code|Education\s+Code|Commercial\s+Code|Civil\s+Code\s+of\s+Procedure)\s*(?:§+|section|sec\.?)?\s*(?<section>\d[A-Za-z0-9.:()-]{0,30})\b",
        RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

    private static IEnumerable<Match> AllCitationMatches(string text)
    {
        // California named-code matches take precedence: the generic NamedCode/Reporter patterns also
        // (mis)match the same span (capturing section="of" from "Code of Civil Procedure"), which would
        // otherwise register as a second, overlapping citation and trip the ambiguity guard. Drop any
        // generic match whose span overlaps a California match so the correct citation wins cleanly.
        var californiaMatches=CaliforniaNamedCodeCitation.Matches(text).Cast<Match>().ToList();
        bool OverlapsCalifornia(Match m)=>californiaMatches.Any(c=>m.Index<c.Index+c.Length&&c.Index<m.Index+m.Length);
        var genericMatches=NamedCodeCitation.Matches(text).Cast<Match>()
            .Concat(ReporterStyleCodeCitation.Matches(text).Cast<Match>())
            .Where(m=>!OverlapsCalifornia(m));
        return californiaMatches.Concat(genericMatches);
    }


    public bool TryDetect(string query,out LegalJurisdictionCitation citation)
    {
        citation=new(string.Empty,string.Empty,string.Empty);
        if(string.IsNullOrWhiteSpace(query))return false;
        var explicitJurisdiction=ExplicitJurisdiction.Match(query.Trim());
        var searchText=explicitJurisdiction.Success?query[..explicitJurisdiction.Index].Trim():query;
        if(explicitJurisdiction.Success)
        {
            var display=Regex.Replace(explicitJurisdiction.Groups["jurisdiction"].Value.Trim(),@"\s+"," ");
            var explicitMatches=AllCitationMatches(searchText)
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
        var matches=AllCitationMatches(searchText)
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
