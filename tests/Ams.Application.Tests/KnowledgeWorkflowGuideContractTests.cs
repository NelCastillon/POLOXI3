using Ams.Knowledge.Infrastructure.Persistence;
using Xunit;

namespace Ams.Application.Tests;

public sealed class KnowledgeWorkflowGuideContractTests
{
    [Fact]
    public void Migration_CreatesSearchableWorkflowTableAndSeedsCompleteLifecycle()
    {
        var assembly = typeof(KnowledgeDatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0007_WorkflowKnowledgeBase.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        Assert.Contains("knowledge.WorkflowGuideStep", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TenantId UNIQUEIDENTIFIER NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ValidationRequirements", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NextUserMove", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ModuleSequenceNumber", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ModuleDisplayName", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NavigationRoute", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHEN 'CRM_LEAD' THEN 10", sql, StringComparison.Ordinal);
        Assert.Contains("WHEN 'CRM_LEAD' THEN N'/crm/leads'", sql, StringComparison.Ordinal);
        Assert.Contains("ELSE source.PageRoute END", sql, StringComparison.Ordinal);
        Assert.Contains("LEAD_CREATE", sql, StringComparison.Ordinal);
        Assert.Contains("POLICY_GENERATE", sql, StringComparison.Ordinal);
        Assert.Contains("ENDORSEMENT_COMPLETE", sql, StringComparison.Ordinal);
        Assert.Contains("RENEWAL_COMPLETE", sql, StringComparison.Ordinal);
        Assert.Contains("Save Lead", sql, StringComparison.Ordinal);
        Assert.Contains("Request Quote", sql, StringComparison.Ordinal);
        Assert.Contains("Accept Carrier Binder", sql, StringComparison.Ordinal);
        Assert.Contains("Mark Renewed", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void NavigationMigration_DefersNewColumnReferencesUntilAfterAlterTable()
    {
        var assembly = typeof(KnowledgeDatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0008_WorkflowKnowledgeNavigation.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        Assert.Contains("ALTER TABLE knowledge.WorkflowGuideStep ADD ModuleSequenceNumber", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXEC sys.sp_executesql", sql, StringComparison.OrdinalIgnoreCase);
        Assert.True(sql.IndexOf("EXEC sys.sp_executesql", StringComparison.OrdinalIgnoreCase) > sql.IndexOf("ADD NavigationRoute", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("ALTER TABLE knowledge.WorkflowGuideStep ALTER COLUMN ModuleSequenceNumber", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runtime_ExposesTenantAwareSearchClientPageAndNavigation()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Modules", "Knowledge", "Ams.Knowledge.Infrastructure", "Persistence", "Repositories", "KnowledgeRepository.Queries.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "Ams.Api", "Controllers", "Knowledge", "KnowledgeControllers.cs"));
        var client = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Services", "ApiClients.Knowledge.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Knowledge", "KnowledgeBase.razor"));
        var navigation = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Layout", "NavSidebar.razor"));

        Assert.Contains("TenantId IS NULL OR TenantId = @TenantId", repository, StringComparison.Ordinal);
        Assert.Contains("ROW_NUMBER() OVER (PARTITION BY WorkflowCode, StepCode", repository, StringComparison.Ordinal);
        Assert.Contains("INTO #Filtered", repository, StringComparison.Ordinal);
        Assert.Contains("SELECT COUNT(*) FROM #Filtered", repository, StringComparison.Ordinal);
        Assert.Contains("SELECT * FROM #Filtered", repository, StringComparison.Ordinal);
        Assert.Contains("ORDER BY ModuleSequenceNumber, SequenceNumber", repository, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT COUNT(*) FROM Filtered", repository, StringComparison.Ordinal);
        Assert.Contains("api/knowledge/workflow-guide", controller, StringComparison.Ordinal);
        Assert.Contains("KnowledgePolicies.ConceptsRead", controller, StringComparison.Ordinal);
        Assert.Contains("SearchWorkflowGuideStepsAsync", client, StringComparison.Ordinal);
        Assert.Contains("@page \"/knowledge-base\"", page, StringComparison.Ordinal);
        Assert.Contains("Next user move", page, StringComparison.Ordinal);
        Assert.Contains("Required before continuing", page, StringComparison.Ordinal);
        Assert.Contains("OrderBy(x => x.ModuleSequenceNumber)", page, StringComparison.Ordinal);
        Assert.Contains("Nav.NavigateTo(step.NavigationRoute)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("if (route.Contains('{'))", page, StringComparison.Ordinal);
        Assert.Contains("/knowledge-base", navigation, StringComparison.Ordinal);
    }
}
