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

sealed class DatabaseTenantAccessor:IEpistemicTenantAccessor
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if(tenantId==Guid.Empty)throw new ArgumentException("Database returned an empty tenant ID.",nameof(tenantId));
        TenantId=tenantId;
    }
}
