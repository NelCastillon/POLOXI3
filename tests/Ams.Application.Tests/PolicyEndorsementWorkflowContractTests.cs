using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Xunit;

namespace Ams.Application.Tests;

public sealed class PolicyEndorsementWorkflowContractTests
{
    [Fact]
    public void Change_RequiresExactlyOneTypedValue()
    {
        var change = new PolicyEndorsementChangeInput
        {
            CategoryCode = "Vehicle",
            OperationCode = "Update",
            Vehicle = new PolicyEndorsementVehicleChangeDto(),
            Driver = new PolicyEndorsementDriverChangeDto()
        };

        var results = Validate(change);

        Assert.Contains(results, result => result.ErrorMessage == "Exactly one typed endorsement change is required.");
    }

    [Fact]
    public void Change_RequiresCategoryToMatchTypedValue()
    {
        var change = new PolicyEndorsementChangeInput
        {
            CategoryCode = "Driver",
            OperationCode = "Update",
            Vehicle = new PolicyEndorsementVehicleChangeDto()
        };

        var results = Validate(change);

        Assert.Contains(results, result => result.ErrorMessage == "CategoryCode must be 'Vehicle' for the supplied typed change.");
    }

    [Theory]
    [MemberData(nameof(ConcurrencyRequests))]
    public void WorkflowMutations_RequireConcurrencyTokens(object request)
    {
        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Any(name => name.Contains("RowVersion", StringComparison.Ordinal)));
    }

    [Fact]
    public void EnterpriseReviewCompatibility_NormalizesLegacyStatusesAndSupportsDirectLinks()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "EndorsementWorkbench.razor"));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0105_EnterprisePolicyEndorsementReviewStatusNormalization.sql"));

        Assert.Contains("Status IN (N'PendingReview', N'Pending Review', N'In Review') THEN N'InReview'", repository, StringComparison.Ordinal);
        Assert.Contains("FromStatusCode=CASE WHEN endorsement.Status IN(N'PendingReview',N'Pending Review',N'In Review') THEN N'InReview'", repository, StringComparison.Ordinal);
        Assert.Contains("SupplyParameterFromQuery(Name = \"endorsement\")", page, StringComparison.Ordinal);
        Assert.Contains("_center.Endorsements.FirstOrDefault", page, StringComparison.Ordinal);
        Assert.Contains("Status = N'InReview'", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyEndorsementTypeCompatibility_RemainsTenantScopedAndRejectsAmbiguousNames()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.cs"));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0106_EnterprisePolicyEndorsementTypeNormalization.sql"));

        Assert.Contains("endorsement.TenantId=type.TenantId", repository, StringComparison.Ordinal);
        Assert.Contains("duplicateType.TenantId=type.TenantId", repository, StringComparison.Ordinal);
        Assert.Contains("duplicateType.TypeName=type.TypeName", repository, StringComparison.Ordinal);
        Assert.Contains("duplicateType.EndorsementTypeId<>type.EndorsementTypeId", repository, StringComparison.Ordinal);
        Assert.Contains("catalog.TenantId = endorsement.TenantId", migration, StringComparison.Ordinal);
        Assert.Contains("GROUP BY TenantId, TypeName", migration, StringComparison.Ordinal);
        Assert.Contains("HAVING COUNT(*) = 1", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void InReviewDecisions_AreTenantScopedDatabaseBackedAndRequireConfiguredNotes()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0107_EnterprisePolicyEndorsementInReviewDecisions.sql"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.cs"));
        var service = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "PolicyEndorsementService.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "EndorsementWorkbench.razor"));

        Assert.Contains("profile.TenantId=workflowRule.TenantId", migration.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Contains("N'NeedMoreInfo',@NeedMoreInfoRuleJson", migration, StringComparison.Ordinal);
        Assert.Contains("N'PendingApproval',@PendingApprovalRuleJson", migration, StringComparison.Ordinal);
        Assert.Contains("\"requiresNotes\":true", migration, StringComparison.Ordinal);
        Assert.Contains("JSON_VALUE(workflowRule.RuleJson,N'$.actionLabel')", repository, StringComparison.Ordinal);
        Assert.Contains("@RequiresNotes=CASE WHEN JSON_VALUE(workflowRule.RuleJson,N'$.requiresNotes')", repository, StringComparison.Ordinal);
        Assert.Contains("transition.RequiresNotes && string.IsNullOrWhiteSpace(request.Notes)", service, StringComparison.Ordinal);
        Assert.Contains("SELECT @RequiresApproval=workflowRule.RequiresApproval", repository, StringComparison.Ordinal);
        Assert.DoesNotContain("@ToStatusCode=N'PendingApproval' AND (profile.RequiresUnderwritingReview=1 OR profile.IsHighRisk=1)", repository, StringComparison.Ordinal);
        Assert.Contains("@transition.ActionLabel", page, StringComparison.Ordinal);
        Assert.Contains("@transition.Instruction", page, StringComparison.Ordinal);
        Assert.Contains("Notes = string.IsNullOrWhiteSpace(_transitionNotes) ? null : _transitionNotes.Trim()", page, StringComparison.Ordinal);
        Assert.DoesNotContain("TransitionInstruction", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ConditionalApproval_UsesMutuallyExclusiveDatabaseBackedBranches()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0111_EnterprisePolicyEndorsementConditionalApproval.sql"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.cs"));
        var service = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "PolicyEndorsementService.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "EndorsementWorkbench.razor"));

        Assert.Contains("\"conditionCode\":\"ApprovalRequired\"", migration, StringComparison.Ordinal);
        Assert.Contains("\"conditionCode\":\"ApprovalNotRequiredCarrier\"", migration, StringComparison.Ordinal);
        Assert.Contains("\"conditionCode\":\"ApprovalNotRequiredPolicy\"", migration, StringComparison.Ordinal);
        Assert.Contains("N'InReview',N'SubmittedToCarrier'", migration, StringComparison.Ordinal);
        Assert.Contains("N'InReview',N'PolicyUpdated'", migration, StringComparison.Ordinal);
        Assert.Contains("profile.RequiresUnderwritingReview=1 OR profile.IsHighRisk=1", repository, StringComparison.Ordinal);
        Assert.Contains("profile.RequiresUnderwritingReview=0 AND profile.IsHighRisk=0 AND profile.RequiresCarrierApproval=1", repository, StringComparison.Ordinal);
        Assert.Contains("profile.RequiresUnderwritingReview=0 AND profile.IsHighRisk=0 AND profile.RequiresCarrierApproval=0", repository, StringComparison.Ordinal);
        Assert.Contains("The requested endorsement transition does not satisfy the configured approval condition.", repository, StringComparison.Ordinal);
        Assert.Contains("detail.AvailableTransitions.SingleOrDefault", service, StringComparison.Ordinal);
        Assert.Contains("@foreach (var transition in _detail.AvailableTransitions)", page, StringComparison.Ordinal);
        Assert.Contains("string.Equals(transition.ToStatusCode, \"PendingApproval\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyTypeAliases_ResolveWorkflowRulesWithinTheSameTenant()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0108_EnterprisePolicyEndorsementTypeAliases.sql"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.cs"));

        Assert.Contains("CREATE TABLE Policy.EndorsementTypeAlias", migration, StringComparison.Ordinal);
        Assert.Contains("TenantId UNIQUEIDENTIFIER NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("FOREIGN KEY (TenantId, EndorsementTypeId)", migration, StringComparison.Ordinal);
        Assert.Contains("N'Coverage Change'", migration, StringComparison.Ordinal);
        Assert.Contains("N'Add Insured'", migration, StringComparison.Ordinal);
        Assert.Contains("N'Change Limit'", migration, StringComparison.Ordinal);
        Assert.Contains("alias.TenantId=endorsement.TenantId", migration.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Contains("alias.TenantId=type.TenantId", repository, StringComparison.Ordinal);
        Assert.Contains("alias.EndorsementTypeId=type.EndorsementTypeId", repository, StringComparison.Ordinal);
        Assert.Contains("alias.LegacyTypeValue=endorsement.EndorsementType", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovalAndRfiWorkflow_IsTenantScopedRoutedAuditedAndDatabaseBacked()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0109_EnterprisePolicyEndorsementApprovalAndRfiRouting.sql"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.ApprovalAndRfi.cs"));
        var mainRepository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "EndorsementWorkbench.razor"));

        Assert.Contains("CREATE TABLE Policy.EndorsementWorkflowRoute", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE Policy.PolicyEndorsementInformationRequest", migration, StringComparison.Ordinal);
        Assert.Contains("TenantId UNIQUEIDENTIFIER NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("FOREIGNKEY(TenantId,EndorsementId)", migration.Replace(" ", string.Empty), StringComparison.Ordinal);
        Assert.Contains("N'ENDORSEMENT_APPROVE'", migration, StringComparison.Ordinal);
        Assert.Contains("N'ENDORSEMENT_EDIT_DRAFT'", migration, StringComparison.Ordinal);
        Assert.Contains("CK_EndorsementWorkflowRoute_Target", migration, StringComparison.Ordinal);
        Assert.Contains("UX_EndorsementWorkflowRoute_Default", migration, StringComparison.Ordinal);
        Assert.Contains("UX_EndorsementWorkflowRoute_Level", migration, StringComparison.Ordinal);
        Assert.Contains("CK_EndorsementInformationRequest_DueDate", migration, StringComparison.Ordinal);
        Assert.Contains("N'Approved',N'Rejected'", migration, StringComparison.Ordinal);
        Assert.Contains("AssignedToUserId=@ActorUserId AND StatusCode=N'Pending'", mainRepository, StringComparison.Ordinal);
        Assert.Contains("route.TenantId=@TenantId", mainRepository, StringComparison.Ordinal);
        Assert.Contains("route.ApprovalLevelCode=@ApprovalLevelCode OR route.ApprovalLevelCode IS NULL", mainRepository, StringComparison.Ordinal);
        Assert.Contains("StatusCode=N'Pending',RequestedDateUtc=SYSUTCDATETIME()", mainRepository, StringComparison.Ordinal);
        Assert.Contains("StatusCode=N'InformationRequested'", mainRepository, StringComparison.Ordinal);
        Assert.Contains("Core.Notification", mainRepository, StringComparison.Ordinal);
        Assert.Contains("@FromStatus=N'PendingApproval'", repository, StringComparison.Ordinal);
        Assert.Contains("Only the assigned approver can request information", repository, StringComparison.Ordinal);
        Assert.Contains("@AssignmentStrategyCode=N'ExplicitUser'", repository, StringComparison.Ordinal);
        Assert.Contains("@AssignmentStrategyCode IN(N'Role',N'Permission')", repository, StringComparison.Ordinal);
        Assert.Contains("role.RoleCode=@AssignedRoleCode", repository, StringComparison.Ordinal);
        Assert.Contains("@AssignmentStrategyCode=N'Permission'", repository, StringComparison.Ordinal);
        Assert.Contains("@DueDateUtc<=SYSUTCDATETIME()", repository, StringComparison.Ordinal);
        Assert.Contains("RowVersion=@EndorsementRowVersion", repository, StringComparison.Ordinal);
        Assert.Contains("N'InformationRequested',@FromStatus,N'NeedMoreInfo'", repository, StringComparison.Ordinal);
        Assert.Contains("Status=N'NeedMoreInfo'", repository, StringComparison.Ordinal);
        Assert.Contains("Status=N'InReview'", repository, StringComparison.Ordinal);
        Assert.Contains("EventTypeCode=N'InformationRequested'", repository, StringComparison.Ordinal);
        Assert.Contains("EventTypeCode=N'InformationResponded'", repository, StringComparison.Ordinal);
        Assert.Contains("EventTypeCode=N'InformationResubmitted'", repository, StringComparison.Ordinal);
        Assert.Contains("IsAssignedApprover", page, StringComparison.Ordinal);
        Assert.Contains("My approvals", page, StringComparison.Ordinal);
        Assert.Contains("SelectApprovalAsync", page, StringComparison.Ordinal);
        Assert.Contains("AssignedToName", page, StringComparison.Ordinal);
        Assert.Contains("_workflowDueDate", page, StringComparison.Ordinal);
        Assert.Contains("Requests for Information", page, StringComparison.Ordinal);
        Assert.Contains("EndorsementRowVersion", page, StringComparison.Ordinal);
        Assert.Contains("InformationRequestRowVersion", page, StringComparison.Ordinal);
    }

    [Fact]
    public void InformationResponse_RequiresEndorsementAndRequestConcurrencyTokens()
    {
        var results = Validate(new RespondPolicyEndorsementInformationRequest());
        var members = results.SelectMany(result => result.MemberNames).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(RespondPolicyEndorsementInformationRequest.EndorsementRowVersion), members);
        Assert.Contains(nameof(RespondPolicyEndorsementInformationRequest.InformationRequestRowVersion), members);
    }

    [Fact]
    public void WorkbenchActions_UseTenantBackedConfigurationAndProtectDestructiveOrLossyOperations()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0110_EnterprisePolicyEndorsementDocumentWorkDefinitions.sql"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.cs"));
        var queues = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.WorkQueues.cs"));
        var service = File.ReadAllText(Path.Combine(root, "src", "Ams.Application", "PolicyEndorsementService.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "EndorsementWorkbench.razor"));

        Assert.Contains("CREATE TABLE Policy.PolicyEndorsementDocumentWorkDefinition", migration, StringComparison.Ordinal);
        Assert.Contains("RowVersion ROWVERSION NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("definition.TriggerCode=N'Workflow'", repository, StringComparison.Ordinal);
        Assert.Contains("definition.TriggerCode=N'AccountingCompleted'", queues, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM (VALUES(N'UpdatedDeclaration')", queues, StringComparison.Ordinal);
        Assert.Contains("ValidateOptionsAsync", service, StringComparison.Ordinal);
        Assert.Contains("No active endorsement reason is configured for the tenant", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ReasonCode = \"Other\"", service, StringComparison.Ordinal);
        Assert.Contains("OpenReverseDialog", page, StringComparison.Ordinal);
        Assert.Contains("Reversal effective date", page, StringComparison.Ordinal);
        Assert.Contains("Multiple workflow actions are available", page, StringComparison.Ordinal);
        Assert.Contains("GetAgencyProfileAsync", page, StringComparison.Ordinal);
        Assert.Contains("_detail.Changes.Count <= 1", page, StringComparison.Ordinal);
        Assert.Contains("ValidateWizard()", page, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrencyCode=\"USD\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Guid.NewGuid().ToString(\"N\")", page, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowRoutePreviews_AreTenantScopedDatabaseResolvedAndShownBeforeSubmission()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.ApprovalAndRfi.cs"));
        var transitionRepository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "PolicyEndorsementRepository.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "Ams.Api", "Controllers", "PolicyEndorsementsController.cs"));
        var client = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Services", "ApiClients.PolicyEndorsements.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "EndorsementWorkbench.razor"));

        Assert.Contains("GetRoutePreviewAsync", repository, StringComparison.Ordinal);
        Assert.Contains("endorsement.TenantId=@TenantId", repository, StringComparison.Ordinal);
        Assert.Contains("route.ApprovalLevelCode=@ApprovalLevelCode", repository, StringComparison.Ordinal);
        Assert.Contains("@AssignmentStrategyCode IN(N'Role',N'Permission')", repository, StringComparison.Ordinal);
        Assert.Contains("appUser.TenantId=@TenantId", repository, StringComparison.Ordinal);
        Assert.Contains("accountAssignment.AssignmentRoleCode,N'_',N'')", repository, StringComparison.Ordinal);
        Assert.Contains("IN(N'ACCOUNTMANAGER',N'PRODUCER')", repository, StringComparison.Ordinal);
        Assert.Contains("accountAssignment.IsPrimary=1", repository, StringComparison.Ordinal);
        Assert.Contains("CASE WHEN eligible.Priority=0 THEN N'AccountManager' ELSE N'Producer'", repository, StringComparison.Ordinal);
        Assert.Contains("COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@RequiredPermissionCode", repository, StringComparison.Ordinal);
        Assert.Contains("IN(N'ACCOUNTMANAGER',N'PRODUCER')", transitionRepository, StringComparison.Ordinal);
        Assert.Contains("accountAssignment.IsPrimary=1", transitionRepository, StringComparison.Ordinal);
        Assert.Contains("ORDER BY eligible.Priority", transitionRepository, StringComparison.Ordinal);
        Assert.Contains("COALESCE(rolePermission.PermissionCode,permission.PermissionCode)=@ApprovalPermissionCode", transitionRepository, StringComparison.Ordinal);
        Assert.DoesNotContain("policyAssignment.AccountManagerId", repository, StringComparison.Ordinal);
        Assert.DoesNotContain("policyAssignment.AccountManagerName", repository, StringComparison.Ordinal);
        Assert.Contains("CanAccessPolicyEndorsementWorkflow", controller, StringComparison.Ordinal);
        Assert.Contains("{id:guid}/route-preview", controller, StringComparison.Ordinal);
        Assert.Contains("GetPolicyEndorsementRoutePreviewAsync", client, StringComparison.Ordinal);
        Assert.Contains("await LoadRoutePreviewAsync(\"Approval\")", page, StringComparison.Ordinal);
        Assert.Contains("await LoadRoutePreviewAsync(\"InformationRequest\")", page, StringComparison.Ordinal);
        Assert.Contains("Assigned recipient", page, StringComparison.Ordinal);
        Assert.Contains("Required authority", page, StringComparison.Ordinal);
        Assert.Contains("Tenant routed", page, StringComparison.Ordinal);
        Assert.Contains("Assigned Account Manager fallback", page, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountServiceAssignments_AreDatabaseBackedEditableAndTenantValidated()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "AccountRepository.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "Ams.Api", "Controllers", "AccountsController.cs"));
        var client = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Services", "ApiClient.cs"));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Account360.razor"));

        Assert.Contains("ReplaceServiceAssignmentsAsync", repository, StringComparison.Ordinal);
        Assert.Contains("Client.AccountServiceAssignment", repository, StringComparison.Ordinal);
        Assert.Contains("appUser.TenantId=@TenantId", repository, StringComparison.Ordinal);
        Assert.Contains("appUser.IsActive=1", repository, StringComparison.Ordinal);
        Assert.Contains("SET IsDeleted=1", repository, StringComparison.Ordinal);
        Assert.Contains("N'ACCOUNT_MANAGER',@AccountManagerUserId", repository, StringComparison.Ordinal);
        Assert.Contains("N'PRODUCER',@ProducerUserId", repository, StringComparison.Ordinal);
        Assert.Contains("N'CSR',@CsrUserId", repository, StringComparison.Ordinal);
        Assert.Contains("CanManageAccounts(User, request.TenantId)", controller, StringComparison.Ordinal);
        Assert.Contains("360/service-assignments", controller, StringComparison.Ordinal);
        Assert.Contains("ReplaceAccountServiceAssignmentsAsync", client, StringComparison.Ordinal);
        Assert.Contains("Manage Service Assignments", page, StringComparison.Ordinal);
        Assert.Contains("Primary Account Manager", page, StringComparison.Ordinal);
        Assert.Contains("Primary Producer", page, StringComparison.Ordinal);
        Assert.Contains("SearchUsersAsync", page, StringComparison.Ordinal);
    }

    [Fact]
    public void PolicyDetail_ServiceAssignmentsUsePersistedLinkedAccountData()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var page = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "PolicyDetail.razor"));

        Assert.Contains("Api.GetAccount360Async(_tenantId, _policy.AccountId)", page, StringComparison.Ordinal);
        Assert.Contains("_account360?.ServiceAssignments", page, StringComparison.Ordinal);
        Assert.Contains("Api.SearchUsersAsync(_tenantId, pageSize: 1000)", page, StringComparison.Ordinal);
        Assert.Contains("Api.ReplaceAccountServiceAssignmentsAsync(_policy.AccountId", page, StringComparison.Ordinal);
        Assert.Contains("AccountId = _policy.AccountId", page, StringComparison.Ordinal);
        Assert.Contains("Primary Account Manager", page, StringComparison.Ordinal);
        Assert.Contains("Primary Producer", page, StringComparison.Ordinal);
        Assert.Contains("Changes update the persisted service team", page, StringComparison.Ordinal);
        Assert.DoesNotContain("new UserDto", page, StringComparison.Ordinal);
        Assert.DoesNotContain("AccountManagerName", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceAssignmentSelectors_FilterUsersByCorrespondingIamRole()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var accountPage = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Account360.razor"));
        var policyPage = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "PolicyDetail.razor"));

        Assert.Contains("AccountManagerUsers", accountPage, StringComparison.Ordinal);
        Assert.Contains("ProducerUsers", accountPage, StringComparison.Ordinal);
        Assert.Contains("CsrUsers", accountPage, StringComparison.Ordinal);
        Assert.Contains("user.AssignedRoleCodes", accountPage, StringComparison.Ordinal);
        Assert.Contains("PolicyAccountManagerUsers", policyPage, StringComparison.Ordinal);
        Assert.Contains("PolicyProducerUsers", policyPage, StringComparison.Ordinal);
        Assert.Contains("PolicyCsrUsers", policyPage, StringComparison.Ordinal);
        Assert.Contains("user.AssignedRoleCodes", policyPage, StringComparison.Ordinal);
        Assert.Contains("ACCOUNT_MANAGER", accountPage, StringComparison.Ordinal);
        Assert.Contains("PRODUCER", accountPage, StringComparison.Ordinal);
        Assert.Contains("CSR", accountPage, StringComparison.Ordinal);
        Assert.Contains("ReplaceAccountServiceAssignmentsAsync", accountPage, StringComparison.Ordinal);
        Assert.Contains("ReplaceAccountServiceAssignmentsAsync", policyPage, StringComparison.Ordinal);
    }

    [Fact]
    public void JobTitleCatalog_IsTenantBackedSeededValidatedAndUsedByBothEditors()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0112_EnterpriseIamJobTitleCatalog.sql"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "UserRepository.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "Ams.Api", "Controllers", "UsersController.cs"));
        var client = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Services", "ApiClient.cs"));
        var listPage = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Iam", "Users", "Users.razor"));
        var detailPage = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Iam", "Users", "UserDetail.razor"));

        Assert.Contains("CREATE TABLE IAM.JobTitle", migration, StringComparison.Ordinal);
        Assert.Contains("TenantId UNIQUEIDENTIFIER NOT NULL", migration, StringComparison.Ordinal);
        Assert.Contains("CROSS JOIN @Catalog", migration, StringComparison.Ordinal);
        Assert.Contains("COMMERCIAL_LINES_ACCOUNT_MANAGER", migration, StringComparison.Ordinal);
        Assert.Contains("CLIENT_SERVICE_REPRESENTATIVE", migration, StringComparison.Ordinal);
        Assert.Contains("Migrated from an existing IAM user profile", migration, StringComparison.Ordinal);
        Assert.Contains("FROM IAM.JobTitle", repository, StringComparison.Ordinal);
        Assert.Contains("The selected job title is not active for this tenant", repository, StringComparison.Ordinal);
        Assert.Contains("HasTenantAccess(User, tenantId)", controller, StringComparison.Ordinal);
        Assert.Contains("GetJobTitlesAsync", client, StringComparison.Ordinal);
        Assert.Contains("_jobTitles.GroupBy", listPage, StringComparison.Ordinal);
        Assert.Contains("_jobTitles.GroupBy", detailPage, StringComparison.Ordinal);
        Assert.DoesNotContain("<InputText @bind-Value=\"_form.JobTitle\"", listPage, StringComparison.Ordinal);
        Assert.DoesNotContain("<InputText @bind-Value=\"_editForm.JobTitle\"", detailPage, StringComparison.Ordinal);
    }

    [Fact]
    public void DepartmentAndJobTitleCatalogs_AreNormalizedMappedAndUsedByAllUserEditors()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var prerequisites = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0113_EnterpriseDepartmentPrerequisites.sql"));
        var migration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0113_EnterpriseDepartmentJobTitleMapping.sql"));
        var teamMigration = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Migrations", "0114_EnterpriseAgencyTeamPrerequisite.sql"));
        var repository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "UserRepository.cs"));
        var departmentRepository = File.ReadAllText(Path.Combine(root, "src", "Ams.Infrastructure", "Persistence", "Repositories", "AdminRepositories.cs"));
        var usersPage = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Iam", "Users", "Users.razor"));
        var detailPage = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Iam", "Users", "UserDetail.razor"));
        var drawer = File.ReadAllText(Path.Combine(root, "src", "Ams.Web", "Components", "Pages", "Iam", "Users", "UserEditDrawer.razor"));

        Assert.Contains("CREATE TABLE Agency.Branch", prerequisites, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE Agency.Department", prerequisites, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE Agency.Team", prerequisites, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE Agency.Team", migration, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE Agency.Team", teamMigration, StringComparison.Ordinal);
        Assert.Contains("FK_Agency_Team_Department", teamMigration, StringComparison.Ordinal);
        Assert.Contains("UX_Branch_TenantCode", prerequisites, StringComparison.Ordinal);
        Assert.Contains("UX_Department_TenantCode", prerequisites, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE Agency.DepartmentJobTitle", migration, StringComparison.Ordinal);
        Assert.Contains("FK_Agency_DepartmentJobTitle_Department", migration, StringComparison.Ordinal);
        Assert.Contains("FK_Agency_DepartmentJobTitle_JobTitle", migration, StringComparison.Ordinal);
        Assert.Contains("UX_Agency_DepartmentJobTitle", migration, StringComparison.Ordinal);
        Assert.Contains("N'ACCOUNT_MANAGEMENT',N'Account Management'", migration, StringComparison.Ordinal);
        Assert.Contains("N'CLIENT_SERVICE',N'Client Service'", migration, StringComparison.Ordinal);
        Assert.Contains("N'COMMERCIAL_LINES',N'Commercial Lines'", migration, StringComparison.Ordinal);
        Assert.Contains("N'EMPLOYEE_BENEFITS',N'Employee Benefits'", migration, StringComparison.Ordinal);
        Assert.Contains("ADD DepartmentId UNIQUEIDENTIFIER NULL", migration, StringComparison.Ordinal);
        Assert.Contains("ADD JobTitleId UNIQUEIDENTIFIER NULL", migration, StringComparison.Ordinal);
        Assert.Contains("FK_User_Department", migration, StringComparison.Ordinal);
        Assert.Contains("FK_IAM_User_JobTitle", migration, StringComparison.Ordinal);
        Assert.Contains("Migrated from an existing IAM user profile", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM Core.Department", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN Core.Department", migration, StringComparison.Ordinal);
        Assert.Contains("UPPER(LTRIM(RTRIM(existing.DepartmentCode)))", migration, StringComparison.Ordinal);
        Assert.Contains("UPPER(LTRIM(RTRIM(existing.DepartmentName)))", migration, StringComparison.Ordinal);
        Assert.Contains("SET DepartmentId=NULL", migration, StringComparison.Ordinal);
        Assert.Contains("REFERENCES Agency.Department(DepartmentId)", migration, StringComparison.Ordinal);

        Assert.Contains("mapping.DepartmentId=@DepartmentId", repository, StringComparison.Ordinal);
        Assert.Contains("The selected department is not active for this tenant", repository, StringComparison.Ordinal);
        Assert.Contains("The selected job title is not eligible for this department", repository, StringComparison.Ordinal);
        Assert.Contains("DepartmentId=@DepartmentId,Department=@DepartmentName,JobTitleId=@JobTitleId,JobTitle=@JobTitleName", repository, StringComparison.Ordinal);
        Assert.DoesNotContain("IF OBJECT_ID(N'Agency.Department', N'U') IS NOT NULL", departmentRepository, StringComparison.Ordinal);
        Assert.DoesNotContain("FROM IAM.[User]\n    WHERE TenantId = @TenantId", departmentRepository, StringComparison.Ordinal);

        foreach (var editor in new[] { usersPage, detailPage, drawer })
        {
            Assert.Contains("DepartmentId", editor, StringComparison.Ordinal);
            Assert.Contains("JobTitleId", editor, StringComparison.Ordinal);
            Assert.Contains("GetJobTitlesAsync(_tenantId", editor, StringComparison.Ordinal);
            Assert.Contains("LoadEligibleJobTitlesAsync", editor, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("@bind-Value=\"_form.Department\"", usersPage, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind-Value=\"_editForm.Department\"", detailPage, StringComparison.Ordinal);
        Assert.DoesNotContain("<AppTextBox @bind-Value=\"_form.JobTitle\"", drawer, StringComparison.Ordinal);
    }

    public static TheoryData<object> ConcurrencyRequests => new()
    {
        new SavePolicyEndorsementDraftRequest(),
        new TransitionPolicyEndorsementRequest(),
        new DecidePolicyEndorsementApprovalRequest(),
        new AssignPolicyEndorsementApprovalRequest(),
        new RequestPolicyEndorsementInformationRequest(),
        new RespondPolicyEndorsementInformationRequest(),
        new ResubmitPolicyEndorsementInformationRequest(),
        new ReversePolicyEndorsementRequest()
    };

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}
