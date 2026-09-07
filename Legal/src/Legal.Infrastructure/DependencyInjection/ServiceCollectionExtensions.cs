using Legal.Application;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Infrastructure.Configuration;
using Legal.Infrastructure.Intelligence;
using Legal.Infrastructure.Persistence;
using Legal.Infrastructure.Persistence.ConnectionFactory;
using Legal.Infrastructure.Persistence.Repositories;
using Legal.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IAiProviderRouteRepository, AiProviderRouteRepository>();

        services.AddScoped<IPromptCatalog, PromptCatalog>();
        services.AddScoped<IAiProviderRouter, AiProviderRouter>();
        services.AddHttpClient<IAiProvider, AzureOpenAiProvider>();
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

        return services;
    }
}
