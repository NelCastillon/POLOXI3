using Legal.Application;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Epistemic;
using Legal.Infrastructure.Configuration;
using Legal.Infrastructure.Intelligence;
using Legal.Infrastructure.Persistence;
using Legal.Infrastructure.Persistence.ConnectionFactory;
using Legal.Infrastructure.Persistence.Repositories;
using Legal.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Legal.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLegalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqlOptions>(options =>
        {
            options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        });
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<LegalDatabaseMigrator>();

        services.AddScoped<IIntelligenceRepository, IntelligenceRepository>();
        services.AddScoped<IIntelligenceWideRepository, IntelligenceWideRepository>();
        services.AddScoped<IMathReasoningRepository, MathReasoningRepository>();
        services.AddScoped<IAiProviderRouteRepository, AiProviderRouteRepository>();
        services.AddScoped<IEpistemicClaimRepository, EpistemicClaimRepository>();
        services.AddScoped<IDecisionGovernanceRepository, DecisionGovernanceRepository>();

        // POLOXI Epistemic Authority Layer (EA-1/EA-2): deterministic, stateless governance services.
        // Settings are DB-backed and runtime-effective: resolved per scope from Core.ConfigurationSetting
        // (tenant override -> platform default -> code default). A default tenant accessor returns null so
        // non-HTTP scopes (workers, migrations, tests) safely fall back to platform/code defaults; hosts
        // register an IEpistemicTenantAccessor that reads the authenticated tenant.
        services.TryAddScoped<IEpistemicTenantAccessor, NullEpistemicTenantAccessor>();
        services.AddScoped(sp =>
        {
            var tenantId = sp.GetRequiredService<IEpistemicTenantAccessor>().TenantId;
            if (tenantId is null || tenantId == Guid.Empty)
                return new EpistemicAuthoritySettings();
            var repo = sp.GetRequiredService<IIntelligenceWideRepository>();
            return repo.ResolveEpistemicSettingsAsync(tenantId.Value).GetAwaiter().GetResult();
        });
        services.AddSingleton<IClaimAuthorityGate, ClaimAuthorityGate>();
        services.AddSingleton<IClaimIdentityResolver, ClaimIdentityResolver>();
        services.AddSingleton<IClaimVerificationPrioritizer, ClaimVerificationPrioritizer>();

        // EA-3: material-claim verification bridge into the V2.1 dependency-propagation loop (scoped:
        // depends on the scoped IEpistemicClaimRepository).
        services.AddScoped<IMaterialClaimVerificationService, MaterialClaimVerificationService>();

        // EA-4: closed-loop orchestrator (verify -> propagate -> research need). Scoped: composes the
        // scoped verification service.
        services.AddScoped<IEpistemicClosedLoopOrchestrator, EpistemicClosedLoopOrchestrator>();

        // EA-5: readiness blocking + output claim audit. Scoped: read authoritative claims through the
        // scoped IEpistemicClaimRepository.
        services.AddScoped<IDecisionReadinessEvaluator, DecisionReadinessEvaluator>();
        services.AddScoped<IOutputClaimAuditor, OutputClaimAuditor>();

        // EA-6: advisory bridge that projects the live V2 decision graph into authoritative EA claims
        // and runs readiness/output governance. Scoped: composes the scoped EA services.
        services.AddScoped<IEpistemicDecisionBridge, EpistemicDecisionBridge>();

        services.AddScoped<IPromptCatalog, PromptCatalog>();
        services.AddScoped<IAiProviderRouter, AiProviderRouter>();
        // Disable the HttpClient-level timeout (default 100s) so the per-request timeout defined by the
        // route policy (AzureOpenAiProvider.CreateTimeout, up to 900s for reasoning models such as
        // gpt-5.6-sol) governs cancellation instead of prematurely aborting long reasoning completions.
        services.AddHttpClient<IAiProvider, AzureOpenAiProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        services.AddHttpClient<IExternalKnowledgeProvider, TavilyExternalKnowledgeProvider>();

        // Legal-context grounding sources (selected when the LEGAL search context is used).
        services.AddHttpClient<ICourtListenerLegalSource, CourtListenerLegalRetriever>();
        services.AddHttpClient<IGovInfoLegalSource, GovInfoLegalRetriever>();
        services.AddHttpClient<ICornellLiiLegalSource, CornellLiiLegalRetriever>();
        services.AddScoped<ILegalRetriever, LegalRetriever>();

        services.AddScoped<IAdaptiveRetriever, StandardPoloxiRetriever>();

        // ABV + ambiguity subsystems (mirrors Ams.Infrastructure registrations).
        services.AddScoped<Legal.Application.Abstractions.Persistence.IIntelligenceAbvRepository>(provider => provider.GetRequiredService<IIntelligenceRepository>() as Legal.Application.Abstractions.Persistence.IIntelligenceAbvRepository
            ?? throw new InvalidOperationException("The intelligence repository must support ABV persistence."));
        services.AddScoped<Legal.Application.Abstractions.Persistence.IIntelligenceAmbiguityRepository>(provider => provider.GetRequiredService<IIntelligenceRepository>() as Legal.Application.Abstractions.Persistence.IIntelligenceAmbiguityRepository
            ?? throw new InvalidOperationException("The intelligence repository must support ambiguity persistence."));
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAbvGovernanceEngine, Legal.Application.Features.Intelligence.Abv.AbvGovernanceEngine>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IAbvResolutionEngine, Legal.Application.Features.Intelligence.Abv.AbvResolutionEngine>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IQueryComplexityAnalyzer, Legal.Application.Features.Intelligence.Ambiguity.QueryComplexityAnalyzer>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IModelEscalationPolicy, Legal.Application.Features.Intelligence.Ambiguity.ModelEscalationPolicy>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityPromptStrategy, Legal.Application.Features.Intelligence.Ambiguity.LightAmbiguityPromptStrategy>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityPromptStrategy, Legal.Application.Features.Intelligence.Ambiguity.MediumAmbiguityPromptStrategy>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityPromptStrategy, Legal.Application.Features.Intelligence.Ambiguity.HeavyAmbiguityPromptStrategy>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityPromptSelector, Legal.Application.Features.Intelligence.Ambiguity.AmbiguityPromptSelector>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IHierarchyValidator, Legal.Application.Features.Intelligence.Ambiguity.HierarchyValidator>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IInterpretationStitcher, Legal.Application.Features.Intelligence.Ambiguity.InterpretationStitcher>();
        services.AddSingleton<Legal.Application.Abstractions.Intelligence.IAmbiguityNarrowingEngine, Legal.Application.Features.Intelligence.Ambiguity.AmbiguityNarrowingEngine>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IAmbiguityModelRouter, Legal.Application.Features.Intelligence.Ambiguity.AmbiguityModelRouter>();
        services.AddScoped<Legal.Application.Abstractions.Intelligence.IAmbiguityResolutionEngine, Legal.Application.Features.Intelligence.Ambiguity.AmbiguityResolutionEngine>();

        services.AddScoped<IIntelligenceWideService, IntelligenceWideService>();

        // POLOXI Scientific Reasoning — Mathematics V1 pack.
        services.AddScoped<IMathReasoningService, MathReasoningService>();

        // POLOXI Formalization Gate (Research → Formalize → Math handoff).
        services.AddScoped<IFormalizationService, FormalizationService>();

        // POLOXI Legal Decision Intelligence (/legal/decision) — self-contained module.
        services.AddScoped<ILegalDecisionRepository, LegalDecisionRepository>();
        services.AddScoped<ILegalDecisionAiProvider, LegalDecisionAiProvider>();
        services.AddScoped<ILegalDecisionRetriever, LegalDecisionRetriever>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Core.IDependencyPropagationService, Legal.Application.Features.Intelligence.Decision.Core.DependencyPropagationService>();
        services.AddScoped<Legal.Application.Features.Intelligence.Decision.Core.ILegalDecisionImpactMapper, Legal.Application.Features.Intelligence.Decision.Core.LegalDecisionImpactMapper>();
        services.AddScoped<ILegalDecisionService, LegalDecisionService>();

        return services;
    }
}

// Default ambient-tenant accessor for non-HTTP scopes (workers, migrations, tests): no tenant, so
// EpistemicAuthoritySettings resolution falls back to Platform + code defaults. Hosts override this.
internal sealed class NullEpistemicTenantAccessor : IEpistemicTenantAccessor
{
    public Guid? TenantId => null;
}
