using System.Reflection;
using Ams.Knowledge.Infrastructure.Persistence;
using Ams.Knowledge.Infrastructure.BackgroundProcessing;
using Xunit;

namespace Ams.Application.Tests;

public sealed class KnowledgeMigrationContractTests
{
    [Fact]
    public void EmbeddedMigrations_AreOrderedAndContainRequiredOperationalContracts()
    {
        var assembly = typeof(KnowledgeDatabaseMigrator).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(5, resources.Length);
        Assert.Contains("0001_InsuranceSemanticLayerFoundation", resources[0], StringComparison.Ordinal);
        Assert.Contains("0002_InsuranceSemanticLayerMvpCatalog", resources[1], StringComparison.Ordinal);
        Assert.Contains("0003_ImportStagingLifecycle", resources[2], StringComparison.Ordinal);
        Assert.Contains("0004_EnterpriseInsuranceCatalog", resources[3], StringComparison.Ordinal);
        Assert.Contains("0005_GovernedTerminologyAndSearch", resources[4], StringComparison.Ordinal);

        var foundation = Read(assembly, resources[0]);
        Assert.Contains("CREATE SCHEMA knowledge", foundation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("knowledge.ImportJob", foundation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LeaseExpiresDateUtc", foundation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("knowledge.SemanticOutboxMessage", foundation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UQ_ImportStagingRecord_Number", foundation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WORKER_MAX_RETRIES", foundation, StringComparison.OrdinalIgnoreCase);

        var importLifecycle = Read(assembly, resources[2]);
        Assert.Contains("'STAGED'", importLifecycle, StringComparison.OrdinalIgnoreCase);

        var terminology = Read(assembly, resources[4]);
        Assert.Contains("knowledge.TerminologySource", terminology, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("knowledge.CarrierTerminology", terminology, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("knowledge.SemanticSearchProjection", terminology, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LicenseAcknowledgedByUserId", terminology, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnterpriseCatalog_IsBroadIdempotentSyntheticAndTenantSafe()
    {
        var assembly = typeof(KnowledgeDatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.Contains("0004_EnterpriseInsuranceCatalog", StringComparison.Ordinal));
        var sql = Read(assembly, resource);

        Assert.Contains("LOB.WORKERS_COMPENSATION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOB.CYBER_LIABILITY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BENEFIT.MEDICAL.PPO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIFE.TERM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ANNUITY.FIXED", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ConceptValidationRule", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PublicationItem", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AMS-authored synthetic reference terminology", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM Core.Tenant", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TenantCode = 'DEMO' OR TenantCode LIKE 'ENT-%'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO Core.Tenant", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AuthorityCode = 'ACORD'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AuthorityCode = 'ISO'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FoundationMigration_IsIdempotentAndTenantScoped()
    {
        var assembly = typeof(KnowledgeDatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.Contains("0001_InsuranceSemanticLayerFoundation", StringComparison.Ordinal));
        var sql = Read(assembly, resource);

        Assert.Contains("IF OBJECT_ID", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TenantId UNIQUEIDENTIFIER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_KnowledgeConcept_Search", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UQ_KnowledgeConcept_CodeVersion", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_SemanticOutboxMessage_Work", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM Master.PermissionAction", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@ReadPermissionActionId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("N'Read',1,N'View concept schemes", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BackgroundProcessor_RequiresLeaseFencingAndAtomicHierarchyRebuild()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Modules", "Knowledge", "Ams.Knowledge.Infrastructure", "BackgroundProcessing", "KnowledgeBackgroundProcessor.cs"));
        var source = File.ReadAllText(sourcePath);

        Assert.Contains("LeaseOwner = @LeaseOwner", source, StringComparison.Ordinal);
        Assert.Contains("Ams.Knowledge.HierarchyRebuild", source, StringComparison.Ordinal);
        Assert.Contains("StatusCode = N'STAGED'", source, StringComparison.Ordinal);
        Assert.Contains("Path.IsPathRooted", source, StringComparison.Ordinal);
    }

    private static string Read(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
