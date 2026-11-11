using System.Collections;
using System.Reflection;
using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Regression guard for the Decision2 (Wide2) citation projection fix. MapProposedLegalAuthorities must be
// VERIFIED-FIRST: identity-verified legal snippets the pipeline actually retrieved surface as VERIFIED
// authorities (deriving name/kind/url/relevance from the snippet), while LLM proposals that no verified
// snippet confirmed are appended as honestly-labeled UNVERIFIED supplements. Previously only the shallow
// query-level LLM proposals were projected, so a personal-injury matter showed generic UNVERIFIED cites.
public sealed class Wide2LegalAuthorityProjectionTests
{
    private static readonly Type ServiceType=typeof(IntelligenceWide2Service);
    private static readonly Type ProposedType=ServiceType.GetNestedType("ProposedLegalAuthority",BindingFlags.NonPublic)
        ??throw new InvalidOperationException("ProposedLegalAuthority not found.");
    private static readonly Type ReferenceType=ServiceType.GetNestedType("LegalAuthorityReference",BindingFlags.NonPublic)
        ??throw new InvalidOperationException("LegalAuthorityReference not found.");
    private static readonly Type KindType=ReferenceType.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)[0].GetParameters()[1].ParameterType;
    private static readonly object CaseKind=Enum.Parse(KindType,"Case");

    [Fact]
    public void VerifiedLegalSnippet_SurfacesAsVerifiedAuthority()
    {
        var snippet=new WideExternalKnowledgeSnippet("rowland v christian","Rowland v. Christian, 69 Cal. 2d 108","https://scholar.example/rowland","Duty of care in negligence actions.",0.9m,DateTime.UtcNow)
        {
            AuthorityKind="Case",
            AuthorityIdentityVerified=true
        };

        var result=InvokeMap(EmptyProposals(),[snippet]);

        var verified=Assert.Single(result);
        Assert.Equal("VERIFIED",GetProp(verified,"VerificationStatus"));
        Assert.Equal("Rowland v. Christian, 69 Cal. 2d 108",GetProp(verified,"Name"));
        Assert.Equal("Case",GetProp(verified,"Kind"));
    }

    [Fact]
    public void UnconfirmedProposal_RemainsUnverifiedSupplement()
    {
        var proposals=MakeProposals(("Cabral v. Ralphs Grocery Co.","Case","Duty and foreseeability.",["Cabral","Ralphs"]));

        var result=InvokeMap(proposals,[]);

        var supplement=Assert.Single(result);
        Assert.Equal("UNVERIFIED",GetProp(supplement,"VerificationStatus"));
        Assert.Equal("Cabral v. Ralphs Grocery Co.",GetProp(supplement,"Name"));
    }

    [Fact]
    public void VerifiedEvidence_OrdersBeforeUnverifiedProposals()
    {
        var snippet=new WideExternalKnowledgeSnippet("rowland v christian","Rowland v. Christian, 69 Cal. 2d 108","https://scholar.example/rowland","Duty of care.",0.9m,DateTime.UtcNow)
        {
            AuthorityKind="Case",
            AuthorityIdentityVerified=true
        };
        var proposals=MakeProposals(("Cabral v. Ralphs Grocery Co.","Case","Duty and foreseeability.",["Cabral","Ralphs"]));

        var result=InvokeMap(proposals,[snippet]);

        Assert.Equal(2,result.Count);
        Assert.Equal("VERIFIED",GetProp(result[0],"VerificationStatus"));
        Assert.Equal("UNVERIFIED",GetProp(result[1],"VerificationStatus"));
    }

    [Fact]
    public void MatterCorpusSnippet_WithoutAuthorityKind_IsNotProjectedAsAuthority()
    {
        var snippet=new WideExternalKnowledgeSnippet("matter corpus","Uploaded-Complaint.pdf","matter-corpus://doc","Client complaint.",0.9m,DateTime.UtcNow)
        {
            AuthorityIdentityVerified=true // no AuthorityKind -> not a legal authority
        };

        var result=InvokeMap(EmptyProposals(),[snippet]);

        Assert.Empty(result);
    }

    private static IReadOnlyList<object> InvokeMap(object proposals,WideExternalKnowledgeSnippet[] externalKnowledge)
    {
        var method=ServiceType.GetMethod("MapProposedLegalAuthorities",BindingFlags.NonPublic|BindingFlags.Static)
            ??throw new InvalidOperationException("MapProposedLegalAuthorities not found.");
        var result=(IEnumerable)method.Invoke(null,[proposals,externalKnowledge])!;
        return result.Cast<object>().ToArray();
    }

    private static object EmptyProposals()=>Array.CreateInstance(ProposedType,0);

    private static object MakeProposals(params (string Name,string Kind,string Relevance,string[] Tokens)[] items)
    {
        var array=Array.CreateInstance(ProposedType,items.Length);
        for(var i=0;i<items.Length;i++)
        {
            var reference=Activator.CreateInstance(ReferenceType,(object?[])[items[i].Name,CaseKind,(IReadOnlyList<string>)items[i].Tokens])!;
            var proposed=Activator.CreateInstance(ProposedType,(object?[])[reference,items[i].Name,items[i].Kind,items[i].Relevance])!;
            array.SetValue(proposed,i);
        }
        return array;
    }

    private static string GetProp(object dto,string name)=>
        (string)(dto.GetType().GetProperty(name)!.GetValue(dto)??string.Empty);
}
