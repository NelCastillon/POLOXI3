using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Atomic L3 Factor Propositions for DECISION_DISCOVERY_V2 (migration 0323).
// Locks in that the prompt-only enhancement:
//   • additively strengthens Section 4 (semanticRoots) with the ATOMIC-FACTOR RULE requiring broad L2
//     dimensions to be decomposed into atomic, independently testable L3 factor propositions,
//   • is anchored on the stable R2 depth sentence and guarded so it applies to the R2 dual-hierarchy
//     prompt exactly once (idempotent), and
//   • changes ONLY the DECISION_DISCOVERY_V2 SystemPrompt — no schema shape change, no second engine.
public sealed class DecisionDiscoveryAtomicL3FactorsMigrationTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Migration0323_AddsAtomicFactorRule_ToV2Prompt_Idempotently()
    {
        var sql = ReadMigration("0323_LegalDecisionDiscoveryAtomicL3Factors.sql");

        // Targets only the DECISION_DISCOVERY_V2 prompt.
        Assert.Contains("PromptCode = N'DECISION_DISCOVERY_V2'", sql, StringComparison.Ordinal);

        // Additive: anchored on the stable R2 Section 4 depth sentence and preserves it in the replacement.
        Assert.Contains("Use L1 for major shared decision dependencies, L2 for substantive subdependencies, and L3+ when further decomposition is justified.", sql, StringComparison.Ordinal);

        // Introduces the ATOMIC-FACTOR RULE and its core semantic requirement.
        Assert.Contains("ATOMIC-FACTOR RULE", sql, StringComparison.Ordinal);
        Assert.Contains("ATOMIC, INDEPENDENTLY TESTABLE L3 factor propositions", sql, StringComparison.Ordinal);

        // Preserves the never-verified boundary (Core owns grounding/scoring).
        Assert.Contains("never mark a factor as verified", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0323_IsGuarded_ForR2Only_AndAppliesOnce()
    {
        var sql = ReadMigration("0323_LegalDecisionDiscoveryAtomicL3Factors.sql");

        // Only patches the R2 dual-hierarchy prompt (contains the R2 marker) ...
        Assert.Contains("SystemPrompt LIKE N'%produce TWO DISTINCT semantic hierarchies%'", sql, StringComparison.Ordinal);

        // ... and never re-applies once the ATOMIC-FACTOR RULE marker is already present (idempotent).
        Assert.Contains("SystemPrompt NOT LIKE N'%ATOMIC-FACTOR RULE%'", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0323_DoesNotAlterSchemaShape_OrOtherPrompts()
    {
        var sql = ReadMigration("0323_LegalDecisionDiscoveryAtomicL3Factors.sql");

        // Prompt-only: no OutputSchemaJson rewrite and no DDL in this migration.
        Assert.DoesNotContain("OutputSchemaJson =", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.Ordinal);

        // Does not touch the v1 discovery prompt or POLOXI Wide prompts.
        Assert.DoesNotContain("DECISION_DISCOVERY_V1", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("WIDE_SEMANTIC_PROPOSAL", sql, StringComparison.Ordinal);
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
