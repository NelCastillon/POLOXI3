using Ams.Knowledge.Application.Abstractions.Persistence;
using Ams.Knowledge.Application.Services;
using Ams.Knowledge.Contracts.Concepts;
using Ams.Knowledge.Contracts.Hierarchy;
using Ams.Knowledge.Contracts.Mappings;
using Ams.Knowledge.Contracts.Validation;
using Ams.Knowledge.Infrastructure.BackgroundProcessing;
using Ams.Knowledge.Infrastructure.Configuration;
using Ams.Knowledge.Infrastructure.Persistence;
using Ams.Knowledge.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ams.Knowledge.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KnowledgeSqlOptions>(options =>
        {
            options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            options.ImportRootPath = configuration["Knowledge:ImportRootPath"] ?? string.Empty;
        });

        services.AddMemoryCache();
        services.AddScoped<KnowledgeSqlConnectionFactory>();
        services.AddTransient<KnowledgeDatabaseMigrator>();
        services.AddScoped<IKnowledgeBackgroundProcessor, KnowledgeBackgroundProcessor>();
        services.AddScoped<KnowledgeRepository>();
        services.AddScoped<IKnowledgeQueryRepository>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IKnowledgeCommandRepository>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IConceptResolutionRepository>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IKnowledgeDocumentRoutingProvider>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IKnowledgeResolutionPolicyProvider>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IKnowledgeHierarchyRepository>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IExternalMappingRepository>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IKnowledgeValidationRuleRepository>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IKnowledgeValidationPolicyProvider>(provider => provider.GetRequiredService<KnowledgeRepository>());
        services.AddScoped<IKnowledgeAdministrationService, KnowledgeAdministrationService>();
        services.AddScoped<IConceptResolver, ConceptResolver>();
        services.AddScoped<IKnowledgeHierarchyService, KnowledgeHierarchyService>();
        services.AddScoped<IExternalMappingService, ExternalMappingService>();
        services.AddScoped<IKnowledgeValidationService, KnowledgeValidationService>();
        services.AddScoped<ISemanticRuleEvaluator, RelationalSemanticRuleEvaluator>();
        return services;
    }
}
