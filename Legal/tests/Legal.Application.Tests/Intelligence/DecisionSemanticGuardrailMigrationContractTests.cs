using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class DecisionSemanticGuardrailMigrationContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Migration0289_DefinesScopeCorrectUniquenessAndRelationIntegrity()
    {
        var sql = ReadMigration("0289_LegalDecisionPersonalInjurySemanticGuardrails.sql");

        Assert.Contains("UX_Legal_DecisionDomainConcept_GlobalCode", sql, StringComparison.Ordinal);
        Assert.Contains("TenantId IS NULL AND IsDeleted = 0", sql, StringComparison.Ordinal);
        Assert.Contains("UX_Legal_DecisionDomainConcept_TenantCode", sql, StringComparison.Ordinal);
        Assert.Contains("TenantId IS NOT NULL AND IsDeleted = 0", sql, StringComparison.Ordinal);
        Assert.Contains("FK_Legal_DecisionDomainConceptRelation_Source", sql, StringComparison.Ordinal);
        Assert.Contains("FK_Legal_DecisionDomainConceptRelation_Target", sql, StringComparison.Ordinal);
        Assert.Contains("UX_Legal_DecisionDomainConceptRelation_GlobalEdge", sql, StringComparison.Ordinal);
        Assert.Contains("UX_Legal_DecisionDomainConceptRelation_TenantEdge", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0290_UpgradesAlreadyMigratedDatabasesAndRejectsMissingEndpoints()
    {
        var sql = ReadMigration("0290_LegalDecisionPersonalInjurySemanticGuardrailsHardening.sql");

        Assert.Contains("COL_LENGTH(N'POLOXI.Legal_DecisionDomainConceptRelation', N'SourceDecisionDomainConceptId')", sql, StringComparison.Ordinal);
        Assert.Contains("THROW 50002", sql, StringComparison.Ordinal);
        Assert.Contains("DROP INDEX UX_Legal_DecisionDomainConcept_PackCodeScope", sql, StringComparison.Ordinal);
        Assert.Contains("CK_Legal_DecisionDomainConceptRelation_ActiveEndpoints", sql, StringComparison.Ordinal);
    }

    private static string ReadMigration(string fileName) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Legal.Infrastructure", "Migrations", fileName));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Legal.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Legal solution root.");
    }
}