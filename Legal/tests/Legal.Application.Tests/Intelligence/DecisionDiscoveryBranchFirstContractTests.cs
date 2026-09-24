using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Branch-first discovery (v2) reconstruction contract. Locks in that DECISION_DISCOVERY_V2 mirrors the
// /legal/search Wide semantic pipeline (shared semanticRoots forest + scoreless global candidates) and
// that the legal decision service forks on the feature flag while leaving the legacy v1 path intact.
public sealed class DecisionDiscoveryBranchFirstContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Migration0313_SeedsBranchFirstFlagPromptAndSemanticSchema()
    {
        var sql = ReadMigration("0313_LegalDecisionDiscoveryBranchFirst.sql");

        // Feature flag (default OFF for rollback safety).
        Assert.Contains("Decision.Discovery.BranchFirst.Enabled", sql, StringComparison.Ordinal);
        Assert.Contains("POLOXI.Legal_DecisionSetting", sql, StringComparison.Ordinal);

        // v2 prompt row alongside the untouched v1 prompt code.
        Assert.Contains("DECISION_DISCOVERY_V2", sql, StringComparison.Ordinal);
        Assert.Contains("POLOXI.Legal_DecisionPrompt", sql, StringComparison.Ordinal);

        // Schema mirrors the Wide semantic proposal: shared roots/branches + scoreless global candidates.
        Assert.Contains("semanticRoots", sql, StringComparison.Ordinal);
        Assert.Contains("candidates", sql, StringComparison.Ordinal);
        Assert.Contains("rationaleSummary", sql, StringComparison.Ordinal);

        // Branches are a shared axis of competition — no per-candidate scores are emitted by the LLM.
        Assert.DoesNotContain("candidateBranchScores", sql, StringComparison.Ordinal);

        // v2 discovery is routed through a resolvable model route.
        Assert.Contains("POLOXI.Legal_DecisionModelRoute", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0314_EnablesBranchFirstByDefault_OnlyForTheSeededFalseDefault()
    {
        var sql = ReadMigration("0314_LegalDecisionDiscoveryBranchFirstEnable.sql");

        Assert.Contains("Decision.Discovery.BranchFirst.Enabled", sql, StringComparison.Ordinal);
        // Flips to true, but only when the row still carries the seeded 'false' — never clobbers a
        // deliberate tenant override.
        Assert.Contains("N'true'", sql, StringComparison.Ordinal);
        Assert.Contains("target.SettingValue = N'false'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void DecisionService_ForksDiscoveryOnBranchFirstFlag_AndKeepsLegacyPathIntact()
    {
        var source = ReadSource("Legal.Application", "LegalDecisionService.cs");

        // Both prompt codes exist; v1 is preserved for rollback.
        Assert.Contains("DiscoveryPromptCode = \"DECISION_DISCOVERY\"", source, StringComparison.Ordinal);
        Assert.Contains("DiscoveryPromptCodeV2 = \"DECISION_DISCOVERY_V2\"", source, StringComparison.Ordinal);

        // The flag selects the prompt code and the adapter, mirroring /legal/search.
        Assert.Contains("v2Settings.BranchFirstDiscoveryEnabled", source, StringComparison.Ordinal);
        Assert.Contains("branchFirstDiscovery ? DiscoveryPromptCodeV2 : DiscoveryPromptCode", source, StringComparison.Ordinal);
        Assert.Contains("AdaptSemanticProposal(", source, StringComparison.Ordinal);

        // The adapter reads the Wide semantic shape and attaches the shared branch forest to candidates.
        Assert.Contains("semanticRoots", source, StringComparison.Ordinal);
        Assert.Contains("Branches: sharedBranches", source, StringComparison.Ordinal);
    }

    private static string ReadMigration(string fileName) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Legal.Infrastructure", "Migrations", fileName));

    private static string ReadSource(string project, string fileName) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", project, fileName));

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
