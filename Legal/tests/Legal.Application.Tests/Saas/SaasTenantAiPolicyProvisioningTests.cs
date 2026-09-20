using System.Reflection;
using Legal.Infrastructure.Persistence;
using Legal.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Legal.Application.Tests.Saas;

public sealed class SaasTenantAiPolicyProvisioningTests
{
    [Fact]
    public void BackfillMigration_UsesSaasTenantsAndRepairsWideIntentRoutes()
    {
        var assembly = typeof(LegalDatabaseMigrator).Assembly;
        var resourceName = Assert.Single(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith("0261_BackfillSaaSTenantAiFeaturePolicies.sql", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        Assert.Contains("FROM SaaS.SaaS_Tenant tenant", sql, StringComparison.Ordinal);
        Assert.Contains("model.CapabilityCode = N'CHAT'", sql, StringComparison.Ordinal);
        Assert.Contains("MERGE AI.Legal_FeaturePolicy", sql, StringComparison.Ordinal);
        Assert.Contains("target.IsEnabled = 1", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantCreation_ProvisionsFeaturePoliciesInItsTransaction()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "Legal.Infrastructure",
            "Persistence", "Repositories", "SaasRepository.cs"));
        var source = File.ReadAllText(sourcePath);
        var methodStart = source.IndexOf("public async Task<Guid> CreateTenantAsync", StringComparison.Ordinal);
        var nextMethod = source.IndexOf("public async Task<Guid?> GetRoleIdByCodeAsync", methodStart, StringComparison.Ordinal);
        var method = source[methodStart..nextMethod];

        Assert.Contains("BEGIN TRANSACTION", method, StringComparison.Ordinal);
        Assert.Contains("INSERT SaaS.SaaS_Tenant", method, StringComparison.Ordinal);
        Assert.Contains("INSERT AI.Legal_FeaturePolicy", method, StringComparison.Ordinal);
        Assert.Contains("model.CapabilityCode = N'CHAT'", method, StringComparison.Ordinal);
        Assert.Contains("COMMIT TRANSACTION", method, StringComparison.Ordinal);
    }
}
