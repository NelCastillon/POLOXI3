using System;
using System.IO;
using System.Linq;
using Legal.Application;
using Legal.Application.Features.Intelligence;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Covers the persisted Domain Pack discovery prompt V4 (migration 0427) and the Wide2 contract-context
// bound that guarantees the SELECTED DOMAIN PACK block is never truncated. These are the two paths by
// which Domain Pack hierarchy rules actually reach the model; the runtime overlay builder is covered
// separately in DecisionDomainPackModeConfigurationTests.
public sealed class DecisionDomainPackPromptV4Tests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private const string MigrationFile = "0427_LegalDecisionDomainPackDiscoveryPromptV4DeterministicDimensionRoots.sql";

    [Fact]
    public void Service_Selects_V5_Domain_Pack_Prompt()
    {
        var field = typeof(LegalDecisionService).GetField(
            "DiscoveryPromptCodeDomainPack",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(field);
        Assert.Equal("DECISION_DISCOVERY_DOMAIN_PACK_V5", field!.GetRawConstantValue());
    }

    [Fact]
    public void Migration_0429_Upserts_Complete_V5_Template()
    {
        var sql = ReadMigration("0429_LegalDecisionDomainPackDiscoveryPromptV5IntegratedContract.sql");

        Assert.Contains("(N'DECISION_DISCOVERY_DOMAIN_PACK_V5'", sql);
        Assert.Contains("MERGE POLOXI.Legal_DecisionPrompt", sql);
        Assert.Contains("Revision 3 — explicit L1/L2 ownership and per-L2 coverage contract", sql);
        Assert.Contains("EXPLICIT L1/L2 AND PROPOSITION CONTRACT", sql);
        Assert.Contains("matter description, incident narrative and saved Additional Narrative;", sql);
        Assert.Contains("12. COVERAGE, BUDGET AND FINAL CHECK", sql);
        Assert.Contains("and MATTER_INPUT follow below.';", sql);
    }

    [Fact]
    public void Migration_Derives_V4_From_V3_Without_Touching_Prior_Versions()
    {
        var sql = ReadMigration(MigrationFile);

        Assert.Contains("PromptCode = N'DECISION_DISCOVERY_DOMAIN_PACK_V3'", sql);
        Assert.Contains("(N'DECISION_DISCOVERY_DOMAIN_PACK_V4'", sql);
        Assert.DoesNotContain("UPDATE POLOXI.Legal_DecisionPrompt", sql);
        Assert.Contains("MERGE POLOXI.Legal_DecisionPrompt", sql);
    }

    [Fact]
    public void Migration_Replaces_Coverage_Guidance_With_Exactly_One_L1_Per_Dimension()
    {
        var sql = ReadMigration(MigrationFile);

        // The V1 wording that contradicted the runtime overlay is the REPLACE source, not retained text.
        Assert.Contains("N'Use Domain Pack Dimensions as coverage guidance for relevant L1 roots.'", sql);
        Assert.Contains("N'Do not emit every dimension automatically.'", sql);
        Assert.Contains("IS its own L1 root", sql);
        Assert.Contains("Emit exactly one L1 per Dimension", sql);
        Assert.Contains("Do not merge,", sql);
        Assert.Contains("every active Domain Pack Dimension is present as exactly one L1 root", sql);
    }

    [Fact]
    public void Migration_Adds_Sections_That_Previously_Lived_Only_In_The_Overlay()
    {
        var sql = ReadMigration(MigrationFile);

        Assert.Contains("FULL CONTEXT BOUNDARY", sql);
        Assert.Contains("do not treat missing input as proof", sql);
        Assert.Contains("SOURCE-FIRST, BRANCH-DIRECTED GENERATION", sql);
        Assert.Contains("Do not infer document contents", sql);
        Assert.Contains("L3+ MATERIALITY GATE", sql);
        Assert.Contains("COVERAGE AND OUTPUT", sql);
        Assert.Contains("Do not equate an empty result with", sql);
    }

    [Fact]
    public void Migration_Inserts_Before_The_Original_Prompt_Handoff_Anchor()
    {
        var sql = ReadMigration(MigrationFile);

        Assert.Contains("N'The ORIGINAL_PROMPT (exact stored text), the SELECTED_DOMAIN_PACK'", sql);
        Assert.Contains("REPLACE(@T, @Anchor, @NewSections + @Anchor)", sql);
    }

    [Fact]
    public void BoundContractContext_Truncates_Prefix_But_Keeps_Pack_Block_Intact()
    {
        var prefix = new string('x', 20_000);
        var packBlock = IntelligenceWide2Service.DomainPackBlockMarker + "{\"PackCode\":\"PI_PACK\"} RULES-TAIL";
        var context = prefix + packBlock;

        var bounded = IntelligenceWide2Service.BoundContractContext(context, 9_500);

        Assert.EndsWith(packBlock, bounded);
        Assert.Equal(9_500 + packBlock.Length, bounded.Length);
    }

    [Fact]
    public void BoundContractContext_Without_Pack_Block_Behaves_Like_Plain_Truncate()
    {
        var context = new string('y', 12_000);

        var bounded = IntelligenceWide2Service.BoundContractContext(context, 9_500);

        Assert.Equal(9_500, bounded.Length);
    }

    [Fact]
    public void BoundContractContext_Short_Input_Is_Unchanged()
    {
        var context = "Query contract: short" + IntelligenceWide2Service.DomainPackBlockMarker + "{}";

        Assert.Equal(context, IntelligenceWide2Service.BoundContractContext(context, 9_500));
        Assert.Equal(string.Empty, IntelligenceWide2Service.BoundContractContext(string.Empty, 9_500));
    }

    [Fact]
    public void BoundContractContext_AnswerStage_Bounds_Or_Drops_Pack_Block()
    {
        var prefix = new string('x', 20_000);
        var packBlock = IntelligenceWide2Service.DomainPackBlockMarker + new string('p', 10_000);
        var context = prefix + packBlock;

        var bounded = IntelligenceWide2Service.BoundContractContext(context, 9_500, 6_000);
        Assert.StartsWith(new string('x', 9_500), bounded);
        Assert.Contains(IntelligenceWide2Service.DomainPackBlockMarker, bounded);
        Assert.Contains("domain pack excerpt bounded", bounded);
        Assert.True(bounded.Length < 9_500 + 6_200);

        var dropped = IntelligenceWide2Service.BoundContractContext(context, 9_500, 0);
        Assert.Equal(9_500, dropped.Length);
        Assert.DoesNotContain(IntelligenceWide2Service.DomainPackBlockMarker, dropped);
    }

    [Fact]
    public void SelectLevelBalancedInterpretiveBranches_Keeps_Every_Level_Represented()
    {
        var branches = Enumerable.Range(0, 28).Select(i => Branch(1, i))
            .Concat(Enumerable.Range(0, 128).Select(i => Branch(2, i)))
            .Concat(Enumerable.Range(0, 61).Select(i => Branch(3, i)))
            .OrderBy(b => b.LevelNumber).ThenByDescending(b => b.Confidence).ToArray();

        var selected = IntelligenceWide2Service.SelectLevelBalancedInterpretiveBranches(branches, 24);

        Assert.Equal(24, selected.Length);
        Assert.Equal(8, selected.Count(b => b.LevelNumber == 1));
        Assert.Equal(8, selected.Count(b => b.LevelNumber == 2));
        Assert.Equal(8, selected.Count(b => b.LevelNumber == 3));
        Assert.Equal(selected.OrderBy(b => b.LevelNumber).Select(b => b.BranchCode), selected.Select(b => b.BranchCode));
        Assert.Same(branches, IntelligenceWide2Service.SelectLevelBalancedInterpretiveBranches(branches, 1_000));
    }

    private static WideBranchRecord Branch(int level, int index) =>
        new(Guid.NewGuid(), Guid.Empty, null, Guid.Empty, level, $"L{level}_{index}", $"L{level} {index}", "interp", null, null, "GROUNDED", 0, 0.9m - index * 0.001m, true, null, false, null, index);

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
