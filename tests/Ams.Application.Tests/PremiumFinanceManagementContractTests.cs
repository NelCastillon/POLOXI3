using System.ComponentModel.DataAnnotations;
using Ams.Application.Features.PremiumFinance;
using Xunit;

namespace Ams.Application.Tests;

public sealed class PremiumFinanceManagementContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void RequestContracts_RejectInvalidFinancialAndIdentityValues()
    {
        var option = new AddPremiumFinanceQuoteOptionRequest(
            Guid.Empty, Guid.Empty, Guid.Empty, null, "", -1, -1, 0, 101, -1, 0, 0,
            null, null, null, null, null);
        var cancellation = new CancelPremiumFinanceRequest(Guid.Empty, Guid.Empty, "", null, null);
        var provider = new UpsertPremiumFinanceProviderRequest(
            Guid.Empty, null, "", "", null, "invalid-email", null, null, null, "", "not-a-url", null,
            true, true, true, true, true, true, null, true, null);

        Assert.NotEmpty(Validate(option));
        Assert.NotEmpty(Validate(cancellation));
        Assert.NotEmpty(Validate(provider));
    }

    [Fact]
    public void Service_EnforcesTenantProviderCapabilitiesTransitionsAndFinancialInvariants()
    {
        var service = Read("src", "Ams.Application", "PremiumFinanceService.cs");

        Assert.Contains("EnsureTenant(request.TenantId)", service, StringComparison.Ordinal);
        Assert.Contains("p => p.SupportsQuotes", service, StringComparison.Ordinal);
        Assert.Contains("p => p.SupportsApplications && p.SupportsAgreements", service, StringComparison.Ordinal);
        Assert.Contains("p => p.SupportsPaymentSchedules", service, StringComparison.Ordinal);
        Assert.Contains("Down payment plus amount financed must equal total premium, taxes, and fees.", service, StringComparison.Ordinal);
        Assert.Contains("EnsureRequestTransition", service, StringComparison.Ordinal);
        Assert.Contains("Status cannot change from {current} to {target}.", service, StringComparison.Ordinal);
        Assert.Contains("Agreement does not belong to the selected request.", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Cancellation_ValidatesStateBeforeProviderInvocation()
    {
        var service = Read("src", "Ams.Application", "PremiumFinanceService.cs");
        var stateCheck = service.IndexOf("Premium finance request cannot be cancelled in its current status.", StringComparison.Ordinal);
        var providerCall = service.IndexOf("await adapter.CancelRequestAsync(request", StringComparison.Ordinal);
        var persistenceCall = service.IndexOf("await repository.CancelRequestAsync(request", StringComparison.Ordinal);

        Assert.True(stateCheck >= 0);
        Assert.True(providerCall > stateCheck);
        Assert.True(persistenceCall > providerCall);
    }

    [Fact]
    public void Repository_FencesSourcesRelationshipsCatalogsAndProviderIdentityByTenant()
    {
        var repository = Read("src", "Ams.Infrastructure", "Persistence", "Repositories", "PremiumFinanceRepository.cs");

        Assert.Contains("WHERE q.TenantId=@TenantId AND q.QuoteId=@SourceId", repository, StringComparison.Ordinal);
        Assert.Contains("SupportsQuotes=1", repository, StringComparison.Ordinal);
        Assert.Contains("Agreement does not belong to the selected request.", repository, StringComparison.Ordinal);
        Assert.Contains("OptionGroupCode=N'ActivityType'", repository, StringComparison.Ordinal);
        Assert.Contains("OptionGroupCode=N'DocumentRole'", repository, StringComparison.Ordinal);
        Assert.Contains("Premium finance provider code already exists for tenant.", repository, StringComparison.Ordinal);
        Assert.Contains("Premium finance provider key already exists for tenant.", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_RequiresTenantPermissionsAndRouteBodyConsistency()
    {
        var controller = Read("src", "Ams.Api", "Controllers", "PremiumFinanceController.cs");
        var security = Read("src", "Ams.Api", "Security", "AuthenticatedRequestContext.cs");

        Assert.Contains("[Authorize]", controller, StringComparison.Ordinal);
        Assert.Contains("if (id != request.FinanceAgreementId)", controller, StringComparison.Ordinal);
        Assert.Contains("if (id != request.PremiumFinanceRequestId)", controller, StringComparison.Ordinal);
        Assert.Contains("CanManagePremiumFinance", controller, StringComparison.Ordinal);
        Assert.Contains("HasTenantAccess(user, tenantId)", security, StringComparison.Ordinal);
        Assert.Contains("PREMIUM_FINANCE_MANAGE", security, StringComparison.Ordinal);
    }

    [Fact]
    public void Workbench_ExposesEveryMutationAndValidationFeedback()
    {
        var page = Read("src", "Ams.Web", "Components", "Pages", "PremiumFinance", "PremiumFinanceWorkbench.razor");
        var client = Read("src", "Ams.Web", "Services", "ApiClients.PremiumFinance.cs");

        Assert.Contains("SaveRequestChangesAsync", page, StringComparison.Ordinal);
        Assert.Contains("SaveActivityAsync", page, StringComparison.Ordinal);
        Assert.Contains("SaveDocumentAsync", page, StringComparison.Ordinal);
        Assert.Contains("SaveAgreementAsync", page, StringComparison.Ordinal);
        Assert.Contains("SaveScheduleAsync", page, StringComparison.Ordinal);
        Assert.Contains("SaveCancellationAsync", page, StringComparison.Ordinal);
        Assert.Contains("<ValidationSummary", page, StringComparison.Ordinal);
        Assert.Contains("@onclick:stopPropagation=\"true\"", page, StringComparison.Ordinal);
        Assert.Contains("UpdatePremiumFinanceRequestAsync", client, StringComparison.Ordinal);
        Assert.Contains("ReplacePremiumFinancePaymentScheduleAsync", client, StringComparison.Ordinal);
        Assert.Contains("CancelPremiumFinanceRequestAsync", client, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRequestPopup_UsesDatabaseOptionsAndEligibilityGatedLookup()
    {
        var page = Read("src", "Ams.Web", "Components", "Pages", "PremiumFinance", "PremiumFinanceWorkbench.razor");

        Assert.Contains("Options(\"SourceType\")", page, StringComparison.Ordinal);
        Assert.Contains("x.IsActive && x.SupportsQuotes", page, StringComparison.Ordinal);
        Assert.Contains("@onclick=\"LoadSourceAsync\"", page, StringComparison.Ordinal);
        Assert.Contains("ResetSourceLookup", page, StringComparison.Ordinal);
        Assert.Contains("_source?.IsEligible != true", page, StringComparison.Ordinal);
        Assert.Contains("GetPremiumFinanceSourceAsync", page, StringComparison.Ordinal);
        Assert.Contains("OnParametersSetAsync", page, StringComparison.Ordinal);
        Assert.Contains("OpenRequest(sourceType?.OptionCode??SourceType)", page, StringComparison.Ordinal);
        Assert.Contains("disabled=\"@IsSourceLaunch\"", page, StringComparison.Ordinal);
        Assert.Contains("class=\"text-danger\"", page, StringComparison.Ordinal);

        var quotePage = Read("src", "Ams.Web", "Components", "Pages", "QuoteDetail.razor");
        var policyPage = Read("src", "Ams.Web", "Components", "Pages", "PolicyDetail.razor");
        var renewalPage = Read("src", "Ams.Web", "Components", "Pages", "RenewalDetail.razor");
        Assert.Contains("sourceType=Quote", quotePage, StringComparison.Ordinal);
        Assert.Contains("sourceType=Policy", policyPage, StringComparison.Ordinal);
        Assert.Contains("sourceType=Renewal", renewalPage, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateRequestWorkflow_ValidatesConfiguredSourceProviderAndFinancialLimits()
    {
        var service = Read("src", "Ams.Application", "PremiumFinanceService.cs");

        Assert.Contains("RequireReferenceCodeAsync(request.TenantId, \"SourceType\"", service, StringComparison.Ordinal);
        Assert.Contains("p => p.SupportsQuotes", service, StringComparison.Ordinal);
        Assert.Contains("Requested down payment must be between zero and the total premium", service, StringComparison.Ordinal);
        Assert.Contains("Requested payments must be between 1 and 120.", service, StringComparison.Ordinal);
        Assert.Contains("repository.GetSourceAsync(request.TenantId", service, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceTypeMigration_IsDatabaseBackedRegisteredAndIdempotent()
    {
        var migration = Read("src", "Ams.Infrastructure", "Migrations", "0131_PremiumFinanceSourceTypes.sql");
        var repair = Read("src", "Ams.Infrastructure", "Migrations", "0133_PremiumFinanceSourceTypeRepair.sql");
        var migrator = Read("src", "Ams.Infrastructure", "Persistence", "DatabaseMigrator.cs");
        var project = Read("src", "Ams.Infrastructure", "Ams.Infrastructure.csproj");

        Assert.Contains("Billing.PremiumFinanceReferenceOption", migration, StringComparison.Ordinal);
        Assert.Contains("N'SourceType'", migration, StringComparison.Ordinal);
        Assert.Contains("N'Quote'", migration, StringComparison.Ordinal);
        Assert.Contains("N'Policy'", migration, StringComparison.Ordinal);
        Assert.Contains("N'Renewal'", migration, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", migration, StringComparison.Ordinal);
        Assert.Contains("existing.IsActive = 1", repair, StringComparison.Ordinal);
        Assert.Contains("existing.IsDeleted = 0", repair, StringComparison.Ordinal);
        Assert.Contains("0322_Premium_Finance_Source_Types", migrator, StringComparison.Ordinal);
        Assert.Contains("0323_Premium_Finance_Workflow_Completion", migrator, StringComparison.Ordinal);
        Assert.Contains("0324_Premium_Finance_Source_Type_Repair", migrator, StringComparison.Ordinal);
        Assert.Contains("0131_PremiumFinanceSourceTypes.sql", project, StringComparison.Ordinal);
        Assert.Contains("0133_PremiumFinanceSourceTypeRepair.sql", project, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowCompletionMigration_HardensRelationshipsFinancialsAndReferenceData()
    {
        var migration = Read("src", "Ams.Infrastructure", "Migrations", "0132_PremiumFinanceWorkflowCompletion.sql");
        var project = Read("src", "Ams.Infrastructure", "Ams.Infrastructure.csproj");

        Assert.Contains("CK_PremiumFinanceRequest_DownPayment", migration, StringComparison.Ordinal);
        Assert.Contains("FK_FinanceAgreement_PremiumFinanceRequest", migration, StringComparison.Ordinal);
        Assert.Contains("FK_PremiumFinanceProviderTransaction_Request", migration, StringComparison.Ordinal);
        Assert.Contains("IX_PremiumFinanceDocument_Agreement", migration, StringComparison.Ordinal);
        Assert.Contains("N'ManuallyRecorded'", migration, StringComparison.Ordinal);
        Assert.Contains("N'Legacy manual record'", migration, StringComparison.Ordinal);
        Assert.Contains("0132_PremiumFinanceWorkflowCompletion.sql", project, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderWorkflows_PersistResultsAndDoNotIgnoreCancellationFailures()
    {
        var service = Read("src", "Ams.Application", "PremiumFinanceService.cs");
        var repository = Read("src", "Ams.Infrastructure", "Persistence", "Repositories", "PremiumFinanceRepository.cs");

        Assert.Contains("if (!result.IsSuccessful)", service, StringComparison.Ordinal);
        Assert.Contains("ProviderTransactionStatus", service, StringComparison.Ordinal);
        Assert.Contains("repository.CancelRequestAsync(request, financeCompanyId, result", service, StringComparison.Ordinal);
        Assert.Contains("ProviderResponseJson", repository, StringComparison.Ordinal);
        Assert.Contains("N'CancelRequest'", repository, StringComparison.Ordinal);
        Assert.Contains("N'OptionSelected'", repository, StringComparison.Ordinal);
        Assert.Contains("@ApplicationStatusCode=N'Declined'", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void DialogWorkflows_UseConfiguredTransitionsProvidersAdaptersAndPaymentStatuses()
    {
        var page = Read("src", "Ams.Web", "Components", "Pages", "PremiumFinance", "PremiumFinanceWorkbench.razor");

        Assert.Contains("AllowedRequestStatuses", page, StringComparison.Ordinal);
        Assert.Contains("x.IsActive&&x.SupportsQuotes", page, StringComparison.Ordinal);
        Assert.Contains("_providerForm.ProviderKey", page, StringComparison.Ordinal);
        Assert.Contains("Options(\"PaymentStatus\")", page, StringComparison.Ordinal);
        Assert.Contains("pf-schedule-editor", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ParseSchedule", page, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrityMigration_IsRegisteredEmbeddedAndIdempotent()
    {
        var migration = Read("src", "Ams.Infrastructure", "Migrations", "0129_PremiumFinanceIntegrityHardening.sql");
        var migrator = Read("src", "Ams.Infrastructure", "Persistence", "DatabaseMigrator.cs");
        var project = Read("src", "Ams.Infrastructure", "Ams.Infrastructure.csproj");

        Assert.Contains("IF NOT EXISTS", migration, StringComparison.Ordinal);
        Assert.Contains("UX_FinanceCompany_Tenant_ProviderKey", migration, StringComparison.Ordinal);
        Assert.Contains("CK_PremiumFinanceProviderTransaction_Parent", migration, StringComparison.Ordinal);
        Assert.Contains("CK_PremiumFinancePaymentSchedule_Paid", migration, StringComparison.Ordinal);
        Assert.Contains("0320_Premium_Finance_Integrity_Hardening", migrator, StringComparison.Ordinal);
        Assert.Contains("0129_PremiumFinanceIntegrityHardening.sql", project, StringComparison.Ordinal);
    }

    private static IReadOnlyList<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, true);
        return results;
    }

    private static string Read(params string[] segments)
        => File.ReadAllText(Path.Combine([Root, .. segments]));
}
