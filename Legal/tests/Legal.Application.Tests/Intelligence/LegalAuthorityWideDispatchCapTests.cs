using System.Collections;
using System.Reflection;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Regression guard for the statute dispatch-starvation fix in IntelligenceWideService (the regular
// Wide pipeline used by Deep Citations). The producer (ExtractLegalAuthorities) and the retrieval
// dispatch loop must share MaximumAuthoritiesPerBranch, and that cap must be large enough that two
// Case-first leads can never consume every slot and drop an extracted statute such as
// "California Code of Civil Procedure section 581" before the mandatory identity gate.
public sealed class LegalAuthorityWideDispatchCapTests
{
    private static readonly Type ServiceType=typeof(IntelligenceWideService);

    [Fact]
    public void SharedCap_IsAtLeastFour_SoTwoCaseLeadsCannotStarveStatutes()
    {
        Assert.True(SharedCap>=4,$"MaximumAuthoritiesPerBranch must be at least 4 but was {SharedCap}.");
    }

    [Fact]
    public void ExtractLegalAuthorities_WithCasesBeforeStatute_RetainsTheStatute()
    {
        // Two case citations precede a statute. With the previous Take(2) dispatch the statute would
        // have been dropped; the producer must now surface it within the shared cap.
        const string text=
            "Under Rowland v. Christian and Ortega v. Kmart Corp., and per 42 U.S.C. § 1983, the duty applies.";

        var authorities=InvokeExtract(text);

        Assert.Contains(authorities,a=>AuthorityQuery(a).Contains("1983",StringComparison.OrdinalIgnoreCase));
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

    private static string AuthorityQuery(object authority)
    {
        var queryProperty=authority.GetType().GetProperty("Query")
            ??throw new InvalidOperationException("LegalAuthorityReference.Query not found.");
        return (string)(queryProperty.GetValue(authority)??string.Empty);
    }
}
