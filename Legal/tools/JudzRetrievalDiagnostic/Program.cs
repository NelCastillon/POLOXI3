using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Infrastructure.DependencyInjection;
using Legal.Infrastructure.Intelligence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Judz Retrieval Integration Diagnostic.
// Proves whether Judz can retrieve known California authorities OUTSIDE the full POLOXI decision loop
// and then through the same hybrid retrieval orchestration, without touching scoring/graph/reasoning.
// It reuses the app's DI exactly (AddLegalInfrastructure + appsettings connection string) and, for
// three fixed cases, logs the selected provider, endpoint, normalized citation, source-court IDs,
// HTTP status, raw/parser result counts, filtering reasons, and final passage count. No model calls.
if(args.Length!=1)
    throw new ArgumentException("Usage: JudzRetrievalDiagnostic <path-to-appsettings.json>. Tenants and grounding settings are loaded from the database. No model calls are made.");

var appsettingsPath=args[0];

var configuration=new ConfigurationBuilder()
    .AddJsonFile(Path.GetFullPath(appsettingsPath),optional:false)
    .AddEnvironmentVariables()
    .Build();

var services=new ServiceCollection();
services.AddLogging(builder=>builder.AddSimpleConsole(options=>options.SingleLine=true).SetMinimumLevel(LogLevel.Information));
var tenantAccessor=new DatabaseTenantAccessor();
services.AddSingleton(tenantAccessor);
services.AddScoped<IEpistemicTenantAccessor>(sp=>sp.GetRequiredService<DatabaseTenantAccessor>());
services.AddLegalInfrastructure(configuration);

await using var provider=services.BuildServiceProvider();
Console.WriteLine("=== Judz Retrieval Integration Diagnostic ===");
Console.WriteLine("Tenant selection: database-backed; all tenants returned by ISaasRepository.ListTenantsAsync().");

var cases=new[]
{
    new DiagnosticCase(
        "California Vehicle Code \u00A7 22350",
        "California Vehicle Code \u00A7 22350 basic speed law",
        LegalAuthorityKind.Statute,
        null),
    new DiagnosticCase(
        "California Code of Civil Procedure \u00A7 437c (summary judgment)",
        "California Code of Civil Procedure \u00A7 437c summary judgment",
        LegalAuthorityKind.Statute,
        new LegalAuthorityScope
        {
            IssueScopeCode=LegalAuthorityIssueScopes.ProceduralLaw,
            GoverningLaw="California",
            ProceduralLaw="California",
        }),
    new DiagnosticCase(
        "Judicial interpretation of \u00A7 437c",
        "summary judgment burden of proof Code of Civil Procedure 437c",
        LegalAuthorityKind.Case,
        new LegalAuthorityScope
        {
            IssueScopeCode=LegalAuthorityIssueScopes.ProceduralLaw,
            AuthorityRoleCode=LegalAuthorityRoles.Controlling,
            GoverningLaw="California",
            CourtOrForum="Supreme Court of California",
            SourceCourt="Supreme Court of California",
            CourtSystem="California",
            ProceduralLaw="California",
        }),
};

using(var discoveryScope=provider.CreateScope())
{
    var tenants=await discoveryScope.ServiceProvider.GetRequiredService<ISaasRepository>().ListTenantsAsync();
    if(tenants.Count==0)
        throw new InvalidOperationException("The database returned no tenants. No diagnostic was run.");

    foreach(var tenant in tenants)
    {
        tenantAccessor.SetTenant(tenant.TenantId);
        using var tenantScope=provider.CreateScope();
        var sp=tenantScope.ServiceProvider;
        var config=await sp.GetRequiredService<IIntelligenceWideRepository>().GetLegalGroundingConfigurationAsync(tenant.TenantId);
        var official=sp.GetRequiredService<IOfficialLegalAuthoritySource>();
        var retriever=sp.GetRequiredService<ILegalRetriever>();

        Console.WriteLine("====================================================================");
        Console.WriteLine($"TENANT: {tenant.Name} ({tenant.TenantId}) status={tenant.StatusCode}");
        Console.WriteLine($"LegalGrounding.Enabled={config.Enabled} CourtListenerEnabled={config.CourtListenerEnabled} GovInfoEnabled={config.GovInfoEnabled} CornellLiiEnabled={config.CornellLiiEnabled} Timeout={config.TimeoutSeconds}s");
        Console.WriteLine($"CourtListenerBaseUrl={config.CourtListenerBaseUrl} (token={(string.IsNullOrWhiteSpace(config.CourtListenerApiToken)?"absent":"present")})");
        Console.WriteLine();

        foreach(var testCase in cases)
        {
            Console.WriteLine("--------------------------------------------------------------------");
            Console.WriteLine($"CASE: {testCase.Label}");
            Console.WriteLine($"query=\"{testCase.Query}\" kind={testCase.Kind}");
            Console.WriteLine();

            Console.WriteLine("[Lane A] Direct IOfficialLegalAuthoritySource.SearchAsync");
            try
            {
                var direct=await official.SearchAsync(testCase.Query,config);
                PrintDiagnostic(direct.Diagnostic,direct.Snippets.Count);
                PrintSnippets(direct.Snippets);
            }
            catch(Exception exception)
            {
                Console.WriteLine($"  EXCEPTION: {exception.GetType().Name}: {exception.Message}");
            }
            Console.WriteLine();

            Console.WriteLine("[Lane B] Full ILegalRetriever.SearchScopedAsync");
            try
            {
                var request=new LegalProviderSearchRequest(testCase.Query,testCase.Kind,testCase.Scope);
                var full=await retriever.SearchScopedAsync(request,config);
                foreach(var diagnostic in full.Providers)
                    PrintDiagnostic(diagnostic,full.Snippets.Count);
                Console.WriteLine($"  final passage count = {full.Snippets.Count}");
                PrintSnippets(full.Snippets);
            }
            catch(Exception exception)
            {
                Console.WriteLine($"  EXCEPTION: {exception.GetType().Name}: {exception.Message}");
            }
            Console.WriteLine();
        }

        // ── Per-state statutory smoke test ─────────────────────────────────────────────────────
        // One known citation per US state. Proves whether the 0325/0326/0327 registry descriptors
        // actually resolve to a live, fetchable section (Lane A). Use this to PROMOTE pending rows:
        // a PASS here is the evidence required to flip IsEnabled=0 -> 1 for that state.
        Console.WriteLine("====================================================================");
        Console.WriteLine("PER-STATE STATUTORY SMOKE TEST (one known citation per state)");
        Console.WriteLine("PASS => descriptor resolved a live section; safe to enable. FAIL => keep IsEnabled=0.");
        Console.WriteLine();
        var passed=0;var failed=0;
        foreach(var probe in StateProbes)
        {
            try
            {
                var result=await official.SearchAsync(probe.Query,config);
                var ok=result.Snippets.Count>0;
                if(ok)passed++;else failed++;
                Console.WriteLine($"  [{(ok?"PASS":"FAIL")}] {probe.State,-16} query=\"{probe.Query}\" outcome={result.Diagnostic.OutcomeCode} snippets={result.Snippets.Count}");
            }
            catch(Exception exception)
            {
                failed++;
                Console.WriteLine($"  [FAIL] {probe.State,-16} query=\"{probe.Query}\" EXCEPTION={exception.GetType().Name}: {exception.Message}");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"State smoke-test summary: {passed} PASS / {failed} FAIL of {StateProbes.Length} probes.");
        Console.WriteLine();
    }
}

Console.WriteLine("--------------------------------------------------------------------");
Console.WriteLine("Diagnostic complete. Compare Lane A (direct source access) vs Lane B (routing+filtering):");
Console.WriteLine(" - Lane A empty  => provider configuration / HTTP-API access / parsing problem.");
Console.WriteLine(" - Lane A found but Lane B empty => routing or filtering defect, not source access.");

static void PrintDiagnostic(LegalProviderRetrievalDiagnostic diagnostic,int laneSnippetCount)
{
    Console.WriteLine($"  provider={diagnostic.ProviderCode} selected={diagnostic.Selected} outcome={diagnostic.OutcomeCode}");
    Console.WriteLine($"    rawResults={diagnostic.RawResultCount} parserResults={diagnostic.ReturnedCount}");
    if(diagnostic.NormalizedCitation is not null)Console.WriteLine($"    normalizedCitation={diagnostic.NormalizedCitation}");
    if(diagnostic.EndpointUrl is not null)Console.WriteLine($"    endpoint={diagnostic.EndpointUrl}");
    if(diagnostic.HttpStatus is not null)Console.WriteLine($"    httpStatus={diagnostic.HttpStatus}");
    if(diagnostic.SourceCourtIds is{Count:>0})Console.WriteLine($"    sourceCourtIds=[{string.Join(",",diagnostic.SourceCourtIds)}]");
    if(diagnostic.Detail is not null)Console.WriteLine($"    filteringReason={diagnostic.Detail}");
}

static void PrintSnippets(IReadOnlyCollection<WideExternalKnowledgeSnippet> snippets)
{
    foreach(var snippet in snippets.Take(3))
    {
        var text=snippet.Snippet.Length>160?snippet.Snippet[..160]+"\u2026":snippet.Snippet;
        Console.WriteLine($"    passage: {snippet.Title} <{snippet.Url}> provider={snippet.SourceProvider}");
        Console.WriteLine($"             {text}");
    }
}

sealed record DiagnosticCase(string Label,string Query,LegalAuthorityKind Kind,LegalAuthorityScope? Scope);

sealed record StateProbe(string State,string Query);

sealed class DatabaseTenantAccessor:IEpistemicTenantAccessor
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if(tenantId==Guid.Empty)throw new ArgumentException("Database returned an empty tenant ID.",nameof(tenantId));
        TenantId=tenantId;
    }
}

partial class Program
{
    // One well-known citation per state used to smoke-test the 0325/0326/0327 registry descriptors.
    public static readonly StateProbe[] StateProbes =
    [
        new("Alabama","Ala. Code 6-5-410"),
        new("Alaska","AS 09.65.290"),
        new("Arizona","A.R.S. 12-820"),
        new("Arkansas","Ark. Code 16-55-201"),
        new("California","California Vehicle Code § 22350"),
        new("Colorado","C.R.S. 13-21-111"),
        new("Connecticut","Conn. Gen. Stat. 52-572h"),
        new("Delaware","Del. Code tit. 10 § 8119"),
        new("Florida","Fla. Stat. 768.81"),
        new("Georgia","O.C.G.A. 51-3-1"),
        new("Hawaii","HRS 663-1"),
        new("Idaho","Idaho Code 6-1601"),
        new("Illinois","735 ILCS 5/2-1005"),
        new("Indiana","Ind. Code 34-51-2-5"),
        new("Iowa","Iowa Code 668.3"),
        new("Kansas","K.S.A. 60-258a"),
        new("Kentucky","KRS 411.182"),
        new("Louisiana","La. R.S. 9:2800"),
        new("Maine","Me. Rev. Stat. tit. 14 § 156"),
        new("Maryland","Md. Code Cts. & Jud. Proc. 3-902"),
        new("Massachusetts","Mass. Gen. Laws ch. 231 § 85"),
        new("Michigan","MCL 600.2912"),
        new("Minnesota","Minn. Stat. 604.01"),
        new("Mississippi","Miss. Code 11-7-15"),
        new("Missouri","Mo. Rev. Stat. 537.765"),
        new("Montana","MCA 27-1-702"),
        new("Nebraska","Neb. Rev. Stat. 25-21,185.09"),
        new("Nevada","NRS 41.130"),
        new("New Hampshire","RSA 507:7-d"),
        new("New Jersey","N.J. Stat. 2A:15-5.1"),
        new("New Mexico","N.M. Stat. 41-3A-1"),
        new("New York","N.Y. C.P.L.R. 1411"),
        new("North Carolina","N.C. Gen. Stat. 1-139"),
        new("North Dakota","N.D. Cent. Code 32-03.2-02"),
        new("Ohio","R.C. 2315.33"),
        new("Oklahoma","Okla. Stat. tit. 23 § 13"),
        new("Oregon","ORS 31.600"),
        new("Pennsylvania","42 Pa. Cons. Stat. 7102"),
        new("Rhode Island","R.I. Gen. Laws 9-20-4"),
        new("South Carolina","S.C. Code 15-38-15"),
        new("South Dakota","S.D. Codified Laws 20-9-2"),
        new("Tennessee","Tenn. Code 29-11-103"),
        new("Texas","Tex. Civ. Prac. & Rem. Code 33.001"),
        new("Utah","Utah Code 78B-5-818"),
        new("Vermont","Vt. Stat. tit. 12 § 1036"),
        new("Virginia","Va. Code 8.01-58"),
        new("Washington","RCW 4.22.005"),
        new("West Virginia","W. Va. Code 55-7-13a"),
        new("Wisconsin","Wis. Stat. 895.045"),
        new("Wyoming","Wyo. Stat. 1-1-109"),
    ];
}
