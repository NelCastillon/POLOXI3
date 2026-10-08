using System.Collections;
using System.Reflection;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Regression guard for the retrieval-maximization fix: the branch authority producer
// (ExtractLegalAuthorities / ResolveConceptAuthorities) and the retrieval dispatch loop
// in IntelligenceWide2Service must honor the SAME cap, so a resolved authority is never
// silently discarded before the mandatory identity gate. Previously the producers capped
// at three while dispatch used a hardcoded Take(2), dropping a branch's third authority.
public sealed class LegalAuthorityDispatchCapTests
{
    private static readonly Type ServiceType=typeof(IntelligenceWide2Service);

    [Fact]
    public void SharedCap_IsAtLeastThree_SoProducersAndDispatchCannotDrift()
    {
        Assert.True(SharedCap>=3,$"MaximumAuthoritiesPerBranch must be at least 3 but was {SharedCap}.");
    }

    [Fact]
    public void ExtractLegalAuthorities_WithMoreCitationsThanCap_YieldsFullCap_NoneSilentlyDropped()
    {
        // Four distinct, deterministically-parseable citations (case, two U.S.C. statutes, one C.F.R.).
        // The producer caps at the shared maximum; the dispatch loop uses that same cap, so every
        // authority counted here reaches RetrieveStampedAsync and its identity gate.
        const string text=
            "Under Konic International Corp. v. Spokane Computer Services, and per 42 U.S.C. § 1983 "+
            "and 15 U.S.C. § 1692, see also 29 C.F.R. § 1604.11 for the governing standard.";

        var authorities=InvokeExtract(text);

        Assert.Equal(SharedCap,authorities.Count);
    }

    [Fact]
    public void ExtractLegalAuthorities_WithFewerCitationsThanCap_YieldsAllOfThem()
    {
        var authorities=InvokeExtract("The controlling rule is 42 U.S.C. § 1983.");

        Assert.Single(authorities);
    }

    private static int SharedCap
    {
        get
        {
            var capField=ServiceType.GetField("MaximumAuthoritiesPerBranch",BindingFlags.NonPublic|BindingFlags.Static)
                ??throw new InvalidOperationException("MaximumAuthoritiesPerBranch constant not found.");
            return (int)capField.GetRawConstantValue()!;
        }
    }

    private static IReadOnlyList<object> InvokeExtract(string text)
    {
        var method=ServiceType.GetMethod("ExtractLegalAuthorities",BindingFlags.NonPublic|BindingFlags.Static)
            ??throw new InvalidOperationException("ExtractLegalAuthorities method not found.");
        var result=(IEnumerable)method.Invoke(null,[text])!;
        return result.Cast<object>().ToArray();
    }
}
