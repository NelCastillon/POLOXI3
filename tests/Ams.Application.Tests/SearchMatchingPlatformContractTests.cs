using Ams.Infrastructure.Persistence;
using Ams.Application.Features.SearchMatching;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Ams.Application.Tests;

public sealed class SearchMatchingPlatformContractTests
{
    [Fact]
    public void CompletionMigration_DefinesCapabilitiesEvidenceDecisionsAndStrictProfiles()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0092_SearchMatchingPlatformCompletion.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        foreach (var table in new[] { "Search.SearchCapability", "Search.SemanticQueryEvidence", "Search.MatchReviewDecision" })
            Assert.Contains(table, sql, StringComparison.OrdinalIgnoreCase);
        foreach (var decision in new[] { "USE_EXISTING", "CREATE_NEW", "COMPARE", "MERGE_REQUEST" })
            Assert.Contains(decision, sql, StringComparison.Ordinal);
        foreach (var profile in new[] { "LOCATION_MATCH", "VEHICLE_MATCH", "CLAIM_PARTY_MATCH", "COMMISSION_LINE_RECONCILIATION" })
            Assert.Contains(profile, sql, StringComparison.Ordinal);
        Assert.Contains("N'VEHICLE_MATCH',N'Vin'", sql, StringComparison.Ordinal);
        Assert.Contains("IsCriticalIdentifier", sql, StringComparison.Ordinal);
        Assert.Contains("ExactMatchOnly", sql, StringComparison.Ordinal);
        Assert.Contains("AllowAutomaticLink=0", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO Core.Tenant", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT Search.MatchExecution", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Runtime_UsesHybridFullTextKnowledgeEvidenceAndNoHardcodedCommissionScores()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "SearchMatchingRepository.cs"));
        var service = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "Services", "EntityMatchingService.cs"));
        var commission = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "CommissionAccountingRepository.cs"));
        var commissionService = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "Services", "CommissionAccountingService.cs"));
        var duplicateUi = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Crm", "DuplicateManagement.razor"));

        Assert.Contains("CONTAINSTABLE", repository, StringComparison.Ordinal);
        Assert.Contains("Search.SearchCapability", repository, StringComparison.Ordinal);
        Assert.Contains("SaveSemanticEvidenceAsync", service, StringComparison.Ordinal);
        Assert.Contains("SEMANTIC_ADVISORY", service, StringComparison.Ordinal);
        Assert.Contains("SearchMatching:", commission, StringComparison.Ordinal);
        Assert.Contains("MatchProfileCodes.CommissionLineReconciliation", commissionService, StringComparison.Ordinal);
        Assert.Contains("MatchReviewDecisionCodes.MergeRequest", duplicateUi, StringComparison.Ordinal);
        Assert.Contains("MatchReviewDecisionCodes.CreateNew", duplicateUi, StringComparison.Ordinal);
    }

    [Fact]
    public void AdministrationMigration_AddsDatabaseBackedSemanticPolicy()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0093_SearchMatchingAdministration.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        Assert.Contains("SemanticMaximumConcepts", sql, StringComparison.Ordinal);
        Assert.Contains("BETWEEN 1 AND 50", sql, StringComparison.Ordinal);
        Assert.Contains("COL_LENGTH", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalCompletion_DatabaseBacksSemanticPreprocessingAndTelemetry()
    {
        var assembly = typeof(DatabaseMigrator).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(name => name.EndsWith("0094_SearchMatchingOperationalCompletion.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var migration = reader.ReadToEnd();
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "SearchMatchingRepository.cs"));
        var expander = File.ReadAllText(Path.Combine(root, "src", "Ams.Api", "Services", "KnowledgeSemanticQueryExpander.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Intelligence", "SearchMatchingSettings.razor"));

        foreach (var key in new[] { "maximumTokens", "maximumPhraseLength", "maximumPhrases" })
        {
            Assert.Contains(key, migration, StringComparison.Ordinal);
            Assert.Contains(key, repository, StringComparison.Ordinal);
        }
        Assert.Contains("GetSemanticPreprocessingSettingsAsync", expander, StringComparison.Ordinal);
        Assert.DoesNotContain("Take(12)", expander, StringComparison.Ordinal);
        Assert.DoesNotContain("phrases.Take(30)", expander, StringComparison.Ordinal);
        Assert.Contains("SearchMatchingOperationalTelemetry", repository, StringComparison.Ordinal);
        Assert.Contains("Retained tenant activity", page, StringComparison.Ordinal);
        Assert.Contains("No retained matching executions", page, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateScan_OrchestratesSharedMatchingAndOnlyMaterializesSearchEvidence()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var service = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "DuplicateService.cs"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "DuplicateRepository.cs"));
        var matching = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "Services", "EntityMatchingService.cs"));

        Assert.Contains("GetScanSourcesAsync", service, StringComparison.Ordinal);
        Assert.Contains("_matchingService.FindMatchesAsync", service, StringComparison.Ordinal);
        Assert.Contains("SourceHash(source.Fields)", service, StringComparison.Ordinal);
        foreach (var profile in new[] { "MatchProfileCodes.AccountDuplicate", "MatchProfileCodes.ContactDuplicate", "MatchProfileCodes.LeadDuplicate" })
            Assert.Contains(profile, service, StringComparison.Ordinal);
        Assert.Contains("FROM Search.MatchExecution", repository, StringComparison.Ordinal);
        Assert.Contains("FROM Search.EntityProjection", repository, StringComparison.Ordinal);
        Assert.DoesNotContain("OverallScore =", repository, StringComparison.Ordinal);
        Assert.Contains("projection.EntityId != request.SourceEntityId", matching, StringComparison.Ordinal);
    }

    [Fact]
    public void AdministrationRuntime_IsTenantFencedConcurrentAndClonesInheritedRules()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "SearchMatchingRepository.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "Ams.Api", "Controllers", "SearchMatchingController.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Intelligence", "SearchMatchingSettings.razor"));
        var navigation = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Layout", "NavSidebar.razor"));
        var service = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "Services", "EntityMatchingService.cs"));

        Assert.Contains("TenantId=@TenantId", repository, StringComparison.Ordinal);
        Assert.Contains("RowVersion=@RowVersion", repository, StringComparison.Ordinal);
        Assert.Contains("rule.MatchProfileId=@PlatformProfileId", repository, StringComparison.Ordinal);
        Assert.Contains("@ExistingHash<>@RequestHash", repository, StringComparison.Ordinal);
        Assert.Contains("ROW_NUMBER() OVER(PARTITION BY ProfileCode", repository, StringComparison.Ordinal);
        Assert.Contains("tenantAlgorithm.AlgorithmCode=algorithm.AlgorithmCode", repository, StringComparison.Ordinal);
        Assert.Contains("IntelligencePolicies.Configure", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("[Authorize(Policy = IntelligencePolicies.Search)]\npublic sealed class SearchMatchingController", controller.Replace("\r", string.Empty), StringComparison.Ordinal);
        Assert.Contains("GetSearchMatchingAdministrationAsync", page, StringComparison.Ordinal);
        Assert.Contains("/intelligence/search-matching/settings", navigation, StringComparison.Ordinal);
        Assert.Contains("policy.SemanticMaximumConcepts", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ExpandAsync(request.TenantId, request.Query, 12", service, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ALIAS")]
    [InlineData("CUSTOM_ALGORITHM")]
    public void AdministrationValidation_RejectsUnsupportedRuntimeCodes(string value)
    {
        object request = value == "ALIAS"
            ? new SaveNormalizationTermSettingRequest { EntityTypeCode = "Global", FieldCode = "Global", SourceValue = "corp", NormalizedValue = "corporation", TermKindCode = value }
            : new SaveMatchAlgorithmSettingRequest { AlgorithmCode = value, DisplayName = "Custom", AlgorithmKindCode = "FUZZY", ConfigurationJson = "{}" };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, true));
    }

    [Fact]
    public void ProfileValidation_RejectsInvertedDatabaseThresholds()
    {
        var request = new SaveMatchProfileSettingRequest { ProfileCode = "TEST", EntityTypeCode = "Account", DisplayName = "Test", ExactThreshold = 80, StrongThreshold = 90, PossibleThreshold = 95, MaximumCandidates = 25, SemanticMaximumConcepts = 12 };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, true));
        Assert.Contains(results, result => result.ErrorMessage!.Contains("Strong threshold", StringComparison.Ordinal));
        Assert.Contains(results, result => result.ErrorMessage!.Contains("Possible threshold", StringComparison.Ordinal));
    }
}
