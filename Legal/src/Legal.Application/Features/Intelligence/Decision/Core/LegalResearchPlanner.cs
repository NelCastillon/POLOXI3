using System.Text.Json;
using System.Text.RegularExpressions;
using Legal.Application.Abstractions.Intelligence;

namespace Legal.Application.Features.Intelligence.Decision.Core;

public sealed class LegalResearchPlanner:ILegalResearchPlanner
{
    private static readonly Regex ExactCitation=new(
        @"\b(?:\d{1,3}\s+)?(?:U\.?S\.?C\.?|C\.?F\.?R\.?|[A-Z][A-Za-z.]{1,15}\s+(?:Code|C\.?|Stat(?:utes?)?\.?|Admin(?:istrative)?\.?\s+Code))\s*(?:§+|section|sec\.?)?\s*[A-Za-z0-9][A-Za-z0-9.:-]{0,30}\b",
        RegexOptions.Compiled|RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

    public LegalSearchPlan Plan(LegalResearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var need=request.ResearchNeed;
        var proposition=need.PropositionToResolve?.Trim();
        if(string.IsNullOrWhiteSpace(proposition))throw new ArgumentException("The Research Need must contain an atomic proposition.",nameof(request));
        var maximum=Math.Clamp(request.MaximumOperations,1,12);
        var operations=new List<LegalSearchOperation>(maximum);
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach(Match citation in ExactCitation.Matches($"{need.SearchQuery} {proposition}"))
            Add(LegalSearchOperationKind.ExactAuthority,citation.Value,ResolveAuthorityKind(need),null);

        Add(LegalSearchOperationKind.Lexical,BuildQualifiedQuery(need.SearchQuery??proposition,request.Jurisdiction),ResolveAuthorityKind(need),null);

        var concepts=ReadValues(need.SearchConceptsJson);
        if(concepts.Count>0)
            Add(LegalSearchOperationKind.Semantic,BuildQualifiedQuery($"{proposition} {string.Join(' ',concepts)}",request.Jurisdiction),ResolveAuthorityKind(need),null);

        var firstExact=operations.FirstOrDefault(operation=>operation.Kind==LegalSearchOperationKind.ExactAuthority);
        if(firstExact is not null)
            Add(LegalSearchOperationKind.CitationExpansion,$"authorities citing {firstExact.Query}",LegalAuthorityKind.Case,firstExact.LegalSearchOperationId);

        Add(LegalSearchOperationKind.TargetedFallback,BuildQualifiedQuery(need.ResearchQuestion??proposition,request.Jurisdiction),ResolveAuthorityKind(need),null);

        return new(Guid.NewGuid(),need.DecisionResearchNeedId,request.DecisionSessionId,request.TenantId,
            proposition,request.Jurisdiction,request.AuthorityCutoffDate,operations)
        {
            AtomicPropositionId = need.AtomicPropositionId == Guid.Empty
                ? need.DecisionResearchNeedId
                : need.AtomicPropositionId,
            AuthorityScope = request.AuthorityScope ?? need.AuthorityScope,
        };

        void Add(LegalSearchOperationKind kind,string? query,LegalAuthorityKind authorityKind,Guid? parent)
        {
            var normalized=Regex.Replace(query?.Trim()??string.Empty,@"\s+"," ");
            if(operations.Count>=maximum||string.IsNullOrWhiteSpace(normalized)||!seen.Add($"{kind}:{normalized}"))return;
            operations.Add(new(Guid.NewGuid(),kind,normalized,authorityKind,operations.Count+1,parent));
        }
    }

    // Qualifies a lexical/semantic/targeted query with the governing SOVEREIGN only. The incoming
    // jurisdiction value frequently carries a full court caption (e.g. "Superior Court of California,
    // County of Los Angeles"). Appending that caption verbatim pollutes every query with court-name
    // free text and depresses provider relevance. Reduce the caption to its enclosing sovereign (state
    // or federal jurisdiction) and append only that as structured scope. Exact-citation operations are
    // never routed through here, so a citation is never diluted with jurisdiction text.
    private static string BuildQualifiedQuery(string query,string? jurisdiction)
    {
        var sovereign=LegalJurisdictionScope.LooksLikeCourtCaption(jurisdiction)
            ?LegalJurisdictionScope.ExtractSovereign(jurisdiction)
                ??LegalJurisdictionScope.ExtractFederalJurisdiction(jurisdiction)
            :jurisdiction?.Trim();
        return string.IsNullOrWhiteSpace(sovereign)?query:$"{query} jurisdiction {sovereign}";
    }

    private static LegalAuthorityKind ResolveAuthorityKind(DecisionResearchNeedPersistence need)
    {
        var values=ReadValues(need.AuthorityKindsJson);
        var combined=$"{string.Join(' ',values)} {need.AuthorityKind} {need.RequiredEvidenceKind}";
        var regulation=combined.Contains("regulation",StringComparison.OrdinalIgnoreCase);
        var statute=combined.Contains("statute",StringComparison.OrdinalIgnoreCase)||combined.Contains("code",StringComparison.OrdinalIgnoreCase);
        var caseLaw=combined.Contains("case",StringComparison.OrdinalIgnoreCase)||combined.Contains("opinion",StringComparison.OrdinalIgnoreCase);
        if((regulation?1:0)+(statute?1:0)+(caseLaw?1:0)>1)return LegalAuthorityKind.Any;
        if(regulation)return LegalAuthorityKind.Regulation;
        if(statute)return LegalAuthorityKind.Statute;
        if(caseLaw)return LegalAuthorityKind.Case;
        return LegalAuthorityKind.Any;
    }

    private static IReadOnlyList<string> ReadValues(string? json)
    {
        if(string.IsNullOrWhiteSpace(json))return [];
        try{return JsonSerializer.Deserialize<List<string>>(json)?.Where(value=>!string.IsNullOrWhiteSpace(value)).Select(value=>value.Trim()).ToList()??[];}
        catch(JsonException){return [];}
    }
}
