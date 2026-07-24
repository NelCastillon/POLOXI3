using Ams.Application;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Infrastructure.Configuration;
using Ams.Infrastructure.Persistence;
using Ams.Infrastructure.Persistence.ConnectionFactory;
using Ams.Infrastructure.Persistence.Repositories;
using Ams.Infrastructure.Persistence.TypeHandlers;
using Ams.Infrastructure.Payments;
using Ams.Infrastructure.Services;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ams.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Register Dapper custom type handlers ──────────────────────
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

        services.Configure<SqlOptions>(options =>
        {
            options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        });

        services.Configure<DocumentStorageOptions>(options =>
        {
            var section = configuration.GetSection("DocumentStorage");
            options.ConnectionString = section[nameof(DocumentStorageOptions.ConnectionString)] ?? string.Empty;
            options.AccountUri = section[nameof(DocumentStorageOptions.AccountUri)] ?? string.Empty;
            options.ContainerName = section[nameof(DocumentStorageOptions.ContainerName)] ?? "documents";
        });

        services.AddDataProtection();
        services.AddScoped<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddTransient<DatabaseMigrator>();
        services.AddScoped<IDocumentStorageService, AzureBlobDocumentStorageService>();

        // ── Existing repositories ────────────────────────────────────
        services.AddScoped<ILeadRepository, LeadRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IOpportunityRepository, OpportunityRepository>();
        services.AddScoped<IAgreementRepository, AgreementRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<ICommissionPlanRepository, CommissionPlanRepository>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IEnterpriseAuditRepository, EnterpriseAuditRepository>();
        services.AddScoped<IAssistantRepository, AssistantRepository>();
        services.AddScoped<IClaimsRepository, ClaimsRepository>();

        // ── New repositories ─────────────────────────────────────────
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IAgencyBusinessHoursRepository, AgencyBusinessHoursRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserAuditTrailRepository, UserAuditTrailRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IContactRepository, ContactRepository>();
        services.AddScoped<IDuplicateRepository, DuplicateRepository>();
        services.AddScoped<IEnrichmentRepository, EnrichmentRepository>();
        services.AddScoped<IMyWorkbenchRepository, MyWorkbenchRepository>();
        services.AddScoped<IEngagementRepository, EngagementRepository>();
        services.AddScoped<ITimeEntryRepository, TimeEntryRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IBillingAccountRepository, BillingAccountRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentPlatformRepository, PaymentPlatformRepository>();
        services.AddScoped<IGLAccountRepository, GLAccountRepository>();
        services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
        services.AddScoped<ICommissionPayeeRepository, CommissionPayeeRepository>();
        services.AddScoped<ICommissionTransactionRepository, CommissionTransactionRepository>();
        services.AddScoped<ICommissionPayoutRepository, CommissionPayoutRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentWorkflowRepository, DocumentWorkflowRepository>();
        services.AddScoped<IAcordFormRepository, AcordFormRepository>();
        services.AddScoped<IDocumentExceptionRepository, DocumentExceptionRepository>();
        services.AddScoped<IDocumentPacketRepository, DocumentPacketRepository>();
        services.AddScoped<IContactIntakeRepository, ContactIntakeRepository>();
        services.AddScoped<IAssistantMessageRepository, AssistantMessageRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IAgencyDashboardRepository, AgencyDashboardRepository>();
        services.AddScoped<IBusinessRuleRepository, BusinessRuleRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IDepartmentTeamRepository, DepartmentTeamRepository>();
        services.AddScoped<IProducerStaffRepository, ProducerStaffRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<INotificationPolicyRepository, NotificationPolicyRepository>();
        services.AddScoped<IQueueRoutingRepository, QueueRoutingRepository>();
        services.AddScoped<IDataQualityRepository, DataQualityRepository>();
        services.AddScoped<IDataCenterRepository, DataCenterRepository>();
        services.AddScoped<ISlaPolicyRepository, SlaPolicyRepository>();
        services.AddScoped<IAmsCapabilityRepository, AmsCapabilityRepository>();

        // ── Existing services ────────────────────────────────────────
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IOpportunityService, OpportunityService>();
        services.AddScoped<IAgreementService, AgreementService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<ICommissionPlanService, CommissionPlanService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IEnterpriseAuditService, EnterpriseAuditService>();
        services.AddSingleton<EnterpriseAuditQueue>();
        services.AddSingleton<IEnterpriseAuditQueue>(sp => sp.GetRequiredService<EnterpriseAuditQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<EnterpriseAuditQueue>());
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddScoped<IClaimsService, ClaimsService>();

        // ── New services ─────────────────────────────────────────────
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IAgencyBusinessHoursService, AgencyBusinessHoursService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserAuditTrailService, UserAuditTrailService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<IDuplicateService, DuplicateService>();
        services.AddScoped<IEnrichmentService, EnrichmentService>();
        services.AddScoped<IMyWorkbenchService, MyWorkbenchService>();
        services.AddScoped<IEngagementService, EngagementService>();
        services.AddScoped<ITimeEntryService, TimeEntryService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IBillingAccountService, BillingAccountService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentPlatformService, PaymentPlatformService>();
        services.AddScoped<IPaymentProcessorGateway, StripePaymentProcessorGateway>();
        services.AddScoped<IPaymentProcessorGateway, AuthorizeNetPaymentProcessorGateway>();
        services.AddScoped<IPaymentProcessorGateway, AchPaymentProcessorGateway>();
        services.AddScoped<IFinanceService, FinanceService>();
        services.AddScoped<ICommissionService, CommissionService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IAcordFormService, AcordFormService>();
        services.AddScoped<IDocumentExceptionService, DocumentExceptionService>();
        services.AddScoped<IDocumentPacketService, DocumentPacketService>();
        services.AddScoped<IContactIntakeService, ContactIntakeService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAgencyDashboardService, AgencyDashboardService>();
        services.AddScoped<AdminPagesService>();
        services.AddScoped<IAmsCapabilityService, AmsCapabilityService>();

        // ── Platform Core engines ────────────────────────────────────
        services.AddScoped<ITenantDomainRepository, TenantDomainRepository>();
        services.AddScoped<ITenantDomainService, TenantDomainService>();

        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IPlanService, PlanService>();

        services.AddScoped<IPlanSubEntityRepository, PlanSubEntityRepository>();
        services.AddScoped<IPlanSubEntityService, PlanSubEntityService>();

        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        services.AddScoped<IUsageRepository, UsageRepository>();
        services.AddScoped<IUsageService, UsageService>();

        services.AddScoped<IFeatureCatalogRepository, FeatureCatalogRepository>();
        services.AddScoped<IFeatureCatalogService, FeatureCatalogService>();

        services.AddScoped<ITenantFeatureRepository, TenantFeatureRepository>();
        services.AddScoped<ITenantFeatureService, TenantFeatureService>();

        services.AddScoped<IRegionRepository, RegionRepository>();
        services.AddScoped<IRegionService, RegionService>();

        services.AddScoped<IDeploymentBindingRepository, DeploymentBindingRepository>();
        services.AddScoped<IDeploymentBindingService, DeploymentBindingService>();

        services.AddScoped<IDeploymentStampRepository, DeploymentStampRepository>();
        services.AddScoped<IDeploymentStampService, DeploymentStampService>();

        services.AddScoped<ITenantDeploymentAssignmentRepository, TenantDeploymentAssignmentRepository>();
        services.AddScoped<ITenantDeploymentAssignmentService, TenantDeploymentAssignmentService>();

        services.AddScoped<ITenantBrandingRepository, TenantBrandingRepository>();
        services.AddScoped<ITenantSettingsWorkflowRepository, TenantSettingsWorkflowRepository>();
        services.AddScoped<ISubscriptionSettingsWorkflowRepository, SubscriptionSettingsWorkflowRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<ISupportedLocaleRepository, SupportedLocaleRepository>();
        services.AddScoped<IWorkflowDefinitionRepository, WorkflowDefinitionRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<ISecurityAuditRepository, SecurityAuditRepository>();

        services.AddScoped<ITenantBrandingService, TenantBrandingService>();
        services.AddScoped<ITenantSettingsWorkflowService, TenantSettingsWorkflowService>();
        services.AddScoped<ISubscriptionSettingsWorkflowService, SubscriptionSettingsWorkflowService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<ISupportedLocaleService, SupportedLocaleService>();
        services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        services.AddScoped<ITwoFactorSmsSender, LoggingTwoFactorSmsSender>();

        // ── IAM extended engines ─────────────────────────────────────
        services.AddScoped<IUserGroupRepository, UserGroupRepository>();
        services.AddScoped<IExternalUserProfileRepository, ExternalUserProfileRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<ISsoConfigurationRepository, SsoConfigurationRepository>();
        services.AddScoped<IMfaDeviceRepository, MfaDeviceRepository>();
        services.AddScoped<ITrustedDeviceRepository, TrustedDeviceRepository>();
        services.AddScoped<IIamPolicyRepository, IamPolicyRepository>();
        services.AddScoped<IPrivilegedAccessRepository, PrivilegedAccessRepository>();
        services.AddScoped<ISodRuleRepository, SodRuleRepository>();
        services.AddScoped<IAccessReviewRepository, AccessReviewRepository>();

        services.AddScoped<IUserGroupService, UserGroupService>();
        services.AddScoped<IExternalUserProfileService, ExternalUserProfileService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<ISsoConfigurationService, SsoConfigurationService>();
        services.AddScoped<IMfaDeviceService, MfaDeviceService>();
        services.AddScoped<ITrustedDeviceService, TrustedDeviceService>();
        services.AddScoped<IIamPolicyService, IamPolicyService>();
        services.AddScoped<IPrivilegedAccessService, PrivilegedAccessService>();
        services.AddScoped<ISodRuleService, SodRuleService>();
        services.AddScoped<ISodConflictRepository, SodConflictRepository>();
        services.AddScoped<ISodConflictService, SodConflictService>();
        services.AddScoped<IAccessReviewService, AccessReviewService>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IUserScopeRepository, UserScopeRepository>();
        services.AddScoped<ISecurityPolicyRepository, SecurityPolicyRepository>();
        services.AddScoped<IRoleBundleRepository, RoleBundleRepository>();
        services.AddScoped<IUserPermissionRepository, UserPermissionRepository>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IUserScopeService, UserScopeService>();
        services.AddScoped<ISecurityPolicyService, SecurityPolicyService>();
        services.AddScoped<IRoleBundleService, RoleBundleService>();
        services.AddScoped<IUserPermissionService, UserPermissionService>();

        // ── Access Governance ────────────────────────────────────────
        services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();
        services.AddScoped<IAccessRequestService, AccessRequestService>();

        // ── CRM and Sales engines ────────────────────────────────────
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ILeadActivityRepository, LeadActivityRepository>();
        services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();
        services.AddScoped<IForecastRepository, ForecastRepository>();

        services.AddScoped<IQuoteService, QuoteService>();
        services.AddScoped<ILeadActivityService, LeadActivityService>();
        services.AddScoped<IPricingRuleService, PricingRuleService>();
        services.AddScoped<IForecastService, ForecastService>();
        services.AddScoped<IProducerWorkbenchRepository, ProducerWorkbenchRepository>();
        services.AddScoped<IProducerWorkbenchService, ProducerWorkbenchService>();
        services.AddScoped<ICsrWorkbenchRepository, CsrWorkbenchRepository>();
        services.AddScoped<ICsrWorkbenchService, CsrWorkbenchService>();
        services.AddScoped<IServiceManagerWorkbenchRepository, ServiceManagerWorkbenchRepository>();
        services.AddScoped<IServiceManagerWorkbenchService, ServiceManagerWorkbenchService>();
        services.AddScoped<IAccountingWorkbenchRepository, AccountingWorkbenchRepository>();
        services.AddScoped<IAccountingWorkbenchService, AccountingWorkbenchService>();
        services.AddScoped<IMarketingWorkbenchRepository, MarketingWorkbenchRepository>();
        services.AddScoped<IMarketingWorkbenchService, MarketingWorkbenchService>();
        services.AddScoped<IOperationsWorkbenchRepository, OperationsWorkbenchRepository>();
        services.AddScoped<IOperationsWorkbenchService, OperationsWorkbenchService>();
        services.AddScoped<IRenewalRetentionRepository, RenewalRetentionRepository>();
        services.AddScoped<IRenewalRetentionService, RenewalRetentionService>();

        // ── Client and Account engines ───────────────────────────────
        services.AddScoped<IAccountNoteRepository, AccountNoteRepository>();
        services.AddScoped<IAccountSegmentRepository, AccountSegmentRepository>();
        services.AddScoped<IAccountSegmentRuleRepository, AccountSegmentRuleRepository>();
        services.AddScoped<IPortalInviteRepository, PortalInviteRepository>();
        services.AddScoped<IAccountOwnerHistoryRepository, AccountOwnerHistoryRepository>();

        services.AddScoped<IAccountNoteService, AccountNoteService>();
        services.AddScoped<IAccountSegmentService, AccountSegmentService>();
        services.AddScoped<IAccountSegmentRuleService, AccountSegmentRuleService>();
        services.AddScoped<IPortalInviteService, PortalInviteService>();
        services.AddScoped<IAccountOwnerHistoryService, AccountOwnerHistoryService>();

        // ── Operations extended engines ──────────────────────────────
        services.AddScoped<IEngagementMilestoneRepository, EngagementMilestoneRepository>();
        services.AddScoped<ITaskItemRepository, TaskItemRepository>();
        services.AddScoped<ITaskTypeRepository, TaskTypeRepository>();
        services.AddScoped<IServiceIssueRepository, ServiceIssueRepository>();
        services.AddScoped<IAgreementAmendmentRepository, AgreementAmendmentRepository>();
        services.AddScoped<IAgreementRenewalRepository, AgreementRenewalRepository>();
        services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
        services.AddScoped<IOperationalActivityRepository, OperationalActivityRepository>();
        services.AddScoped<ICalendarEventRepository, CalendarEventRepository>();

        services.AddScoped<IEngagementMilestoneService, EngagementMilestoneService>();
        services.AddScoped<ITaskItemService, TaskItemService>();
        services.AddScoped<ITaskTypeService, TaskTypeService>();
        services.AddScoped<IServiceIssueService, ServiceIssueService>();
        services.AddScoped<IAgreementAmendmentService, AgreementAmendmentService>();
        services.AddScoped<IAgreementRenewalService, AgreementRenewalService>();
        services.AddScoped<IServiceRequestService, ServiceRequestService>();
        services.AddScoped<IOperationalActivityService, OperationalActivityService>();
        services.AddScoped<ICalendarEventService, CalendarEventService>();

        // ── Billing extended engines ─────────────────────────────────
        services.AddScoped<IRateCardRepository, RateCardRepository>();
        services.AddScoped<IRateCardLineRepository, RateCardLineRepository>();
        services.AddScoped<IPrebillBatchRepository, PrebillBatchRepository>();
        services.AddScoped<IInvoiceLineRepository, InvoiceLineRepository>();
        services.AddScoped<IRecurringBillingScheduleRepository, RecurringBillingScheduleRepository>();
        services.AddScoped<IMilestoneBillingLinkRepository, MilestoneBillingLinkRepository>();
        services.AddScoped<IRetainerAccountRepository, RetainerAccountRepository>();
        services.AddScoped<IRetainerDrawdownRepository, RetainerDrawdownRepository>();
        services.AddScoped<IBillingAdjustmentRepository, BillingAdjustmentRepository>();
        services.AddScoped<IArAgingSnapshotRepository, ArAgingSnapshotRepository>();
        services.AddScoped<IDelinquencyFlagRepository, DelinquencyFlagRepository>();
        services.AddScoped<ICollectionsNoteRepository, CollectionsNoteRepository>();

        services.AddScoped<IRateCardService, RateCardService>();
        services.AddScoped<IRateCardLineService, RateCardLineService>();
        services.AddScoped<IPrebillBatchService, PrebillBatchService>();
        services.AddScoped<IInvoiceLineService, InvoiceLineService>();
        services.AddScoped<IRecurringBillingScheduleService, RecurringBillingScheduleService>();
        services.AddScoped<IMilestoneBillingLinkService, MilestoneBillingLinkService>();
        services.AddScoped<IRetainerAccountService, RetainerAccountService>();
        services.AddScoped<IRetainerDrawdownService, RetainerDrawdownService>();
        services.AddScoped<IBillingAdjustmentService, BillingAdjustmentService>();
        services.AddScoped<IArAgingSnapshotService, ArAgingSnapshotService>();
        services.AddScoped<IDelinquencyFlagService, DelinquencyFlagService>();
        services.AddScoped<ICollectionsNoteService, CollectionsNoteService>();

        // ── Finance extended engines ──────────────────────────
        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddScoped<IApInvoiceRepository, ApInvoiceRepository>();
        services.AddScoped<IApInvoiceLineRepository, ApInvoiceLineRepository>();
        services.AddScoped<IApPaymentRepository, ApPaymentRepository>();
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IPeriodCloseEntryRepository, PeriodCloseEntryRepository>();
        services.AddScoped<IDeferredRevenueScheduleRepository, DeferredRevenueScheduleRepository>();
        services.AddScoped<IDeferredRevenueRecognitionRepository, DeferredRevenueRecognitionRepository>();
        services.AddScoped<IBadDebtEntryRepository, BadDebtEntryRepository>();
        services.AddScoped<ICashReceiptEntryRepository, CashReceiptEntryRepository>();
        services.AddScoped<ITrialBalanceSnapshotRepository, TrialBalanceSnapshotRepository>();
        services.AddScoped<IBankReconciliationRepository, BankReconciliationRepository>();
        services.AddScoped<IJournalEntryLineRepository, JournalEntryLineRepository>();
        services.AddScoped<IVendorService, VendorService>();
        services.AddScoped<IApInvoiceService, ApInvoiceService>();
        services.AddScoped<IApInvoiceLineService, ApInvoiceLineService>();
        services.AddScoped<IApPaymentService, ApPaymentService>();
        services.AddScoped<IAccountingPeriodService, AccountingPeriodService>();
        services.AddScoped<IPeriodCloseEntryService, PeriodCloseEntryService>();
        services.AddScoped<IDeferredRevenueScheduleService, DeferredRevenueScheduleService>();
        services.AddScoped<IDeferredRevenueRecognitionService, DeferredRevenueRecognitionService>();
        services.AddScoped<IBadDebtEntryService, BadDebtEntryService>();
        services.AddScoped<ICashReceiptEntryService, CashReceiptEntryService>();
        services.AddScoped<ITrialBalanceSnapshotService, TrialBalanceSnapshotService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();
        services.AddScoped<IJournalEntryLineService, JournalEntryLineService>();

        // ── Commission extended engines ──────────────────────────
        services.AddScoped<ICommissionPlanVersionRepository, CommissionPlanVersionRepository>();
        services.AddScoped<ICommissionSplitRuleRepository, CommissionSplitRuleRepository>();
        services.AddScoped<ICommissionCalculationResultRepository, CommissionCalculationResultRepository>();
        services.AddScoped<ICommissionClawbackRepository, CommissionClawbackRepository>();
        services.AddScoped<ICommissionPayoutBatchRepository, CommissionPayoutBatchRepository>();
        services.AddScoped<ICommissionExceptionRepository, CommissionExceptionRepository>();
        services.AddScoped<ICommissionForecastRepository, CommissionForecastRepository>();
        services.AddScoped<ICommissionPlannerScenarioRepository, CommissionPlannerScenarioRepository>();
        services.AddScoped<ICommissionDisputeRepository, CommissionDisputeRepository>();
        services.AddScoped<ICommissionPayoutStatementRepository, CommissionPayoutStatementRepository>();
        services.AddScoped<ICommissionAccrualEntryRepository, CommissionAccrualEntryRepository>();

        services.AddScoped<ICommissionPlanVersionService, CommissionPlanVersionService>();
        services.AddScoped<ICommissionSplitRuleService, CommissionSplitRuleService>();
        services.AddScoped<ICommissionCalculationResultService, CommissionCalculationResultService>();
        services.AddScoped<ICommissionClawbackService, CommissionClawbackService>();
        services.AddScoped<ICommissionPayoutBatchService, CommissionPayoutBatchService>();
        services.AddScoped<ICommissionExceptionService, CommissionExceptionService>();
        services.AddScoped<ICommissionForecastService, CommissionForecastService>();
        services.AddScoped<ICommissionPlannerScenarioService, CommissionPlannerScenarioService>();
        services.AddScoped<ICommissionDisputeService, CommissionDisputeService>();
        services.AddScoped<ICommissionPayoutStatementService, CommissionPayoutStatementService>();
        services.AddScoped<ICommissionAccrualEntryService, CommissionAccrualEntryService>();

        // ── Compliance engines ───────────────────────────────────────────────
        services.AddScoped<IPolicyDocumentRepository, PolicyDocumentRepository>();
        services.AddScoped<IPolicyDocumentService, PolicyDocumentService>();
        services.AddScoped<IAcknowledgementRepository, AcknowledgementRepository>();
        services.AddScoped<IAcknowledgementService, AcknowledgementService>();

        // ── Quotas ───────────────────────────────────────────────
        services.AddScoped<IQuotaRuleRepository, QuotaRuleRepository>();
        services.AddScoped<IQuotaRuleService, QuotaRuleService>();
        services.AddScoped<ITenantQuotaRepository, TenantQuotaRepository>();
        services.AddScoped<ITenantQuotaService, TenantQuotaService>();
        services.AddScoped<IQuotaViolationRepository, QuotaViolationRepository>();
        services.AddScoped<IQuotaViolationService, QuotaViolationService>();

        // ── Monitoring ───────────────────────────────────────────
        services.AddScoped<IHealthCheckRepository, HealthCheckRepository>();
        services.AddScoped<IHealthCheckService, HealthCheckService>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<ISlaDefinitionRepository, SlaDefinitionRepository>();
        services.AddScoped<ISlaDefinitionService, SlaDefinitionService>();
        services.AddScoped<IPlatformEventRepository, PlatformEventRepository>();
        services.AddScoped<IPlatformEventService, PlatformEventService>();
        services.AddScoped<IBackgroundJobRepository, BackgroundJobRepository>();
        services.AddScoped<IBackgroundJobService, BackgroundJobService>();
        services.AddScoped<IAutomationJobRepository, AutomationJobRepository>();
        services.AddScoped<IAutomationRuntimeRepository, AutomationRuntimeRepository>();
        services.AddScoped<IAutomationJobService, AutomationJobService>();

        // ── Agency Configuration (Epic 3) ────────────────────────────
        services.AddScoped<IAgencyProfileRepository, AgencyProfileRepository>();
        services.AddScoped<IAgencyProfileService, AgencyProfileService>();
        services.AddScoped<ICarrierRepository, CarrierRepository>();
        services.AddScoped<ICarrierService, CarrierService>();
        services.AddScoped<ILineOfBusinessRepository, LineOfBusinessRepository>();
        services.AddScoped<ILineOfBusinessService, LineOfBusinessService>();
        services.AddScoped<IAppetiteRuleRepository, AppetiteRuleRepository>();
        services.AddScoped<IAppetiteRuleService, AppetiteRuleService>();

        // ── AI Engine (Epic 11) ──────────────────────────────────────
        services.AddScoped<IAiRepository, AiRepository>();
        services.AddScoped<IAiService, AiService>();

        // ── Carrier Integrations (Epic 12) ───────────────────────────
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<IIntegrationService, IntegrationService>();

        // ── Submissions & Quoting Engine ─────────────────────────────
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IPolicyCreationRepository, PolicyCreationRepository>();
        services.AddScoped<IPolicyCreationService, PolicyCreationService>();
        services.AddScoped<ISubmissionWorkflowConfigurationRepository, SubmissionWorkflowConfigurationRepository>();
        services.AddScoped<ISubmissionWorkflowConfigurationService, SubmissionWorkflowConfigurationService>();
        services.AddScoped<ISubmissionReferenceOptionRepository, SubmissionReferenceOptionRepository>();
        services.AddScoped<ISubmissionReferenceOptionService, SubmissionReferenceOptionService>();

        // ── Direct Submission Intake (normalize into Account -> Opportunity -> Submission) ─
        services.AddScoped<ISubmissionIntakeRepository, SubmissionIntakeRepository>();
        services.AddScoped<IAccountMatchingService, AccountMatchingService>();
        services.AddScoped<ISubmissionIntakeService, SubmissionIntakeService>();

        // ── Policy Endorsements Workflow ─────────────────────────────
        services.AddScoped<IPolicyEndorsementRepository, PolicyEndorsementRepository>();
        services.AddScoped<IPolicyEndorsementService, PolicyEndorsementService>();

        // ── Policy Lifecycle Servicing ───────────────────────────────
        services.AddScoped<IPolicyLifecycleRepository, PolicyLifecycleRepository>();
        services.AddScoped<IPolicyLifecycleService, PolicyLifecycleService>();

        // ── Policy Cancellations Workflow ────────────────────────────
        services.AddScoped<IPolicyCancellationRepository, PolicyCancellationRepository>();
        services.AddScoped<IPolicyCancellationService, PolicyCancellationService>();

        // ── Policy Certificates Workflow ─────────────────────────────
        services.AddScoped<IPolicyCertificateRepository, PolicyCertificateRepository>();
        services.AddScoped<IPolicyCertificateService, PolicyCertificateService>();

        // ── Documents — E-Sign (Epic 11) ─────────────────────────────
        services.AddScoped<IESignRepository, ESignRepository>();
        services.AddScoped<IESignService, ESignService>();

        // ── Communications (Epic 10) ──────────────────────────────────
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<ICommTemplateRepository, CommTemplateRepository>();
        services.AddScoped<ICommTemplateService, CommTemplateService>();

        // ── Audit
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IFieldChangeLogRepository, FieldChangeLogRepository>();
        services.AddScoped<IFieldChangeLogService, FieldChangeLogService>();
        services.AddScoped<ISecurityEventLogRepository, SecurityEventLogRepository>();
        services.AddScoped<ISecurityEventLogService, SecurityEventLogService>();
        services.AddScoped<ISystemLogRepository, SystemLogRepository>();
        services.AddScoped<ISystemLogService, SystemLogService>();

        // ── CRM Config repositories & services ──────────────────────
        services.AddScoped<ILeadSourceRepository, LeadSourceRepository>();
        services.AddScoped<ILeadSourceService, LeadSourceService>();
        services.AddScoped<ILeadStatusRepository, LeadStatusRepository>();
        services.AddScoped<ILeadStatusService, LeadStatusService>();
        services.AddScoped<IOpportunityStageRepository, OpportunityStageRepository>();
        services.AddScoped<IOpportunityStageService, OpportunityStageService>();
        services.AddScoped<IOpportunityForecastCategoryRepository, OpportunityForecastCategoryRepository>();
        services.AddScoped<IOpportunityForecastCategoryService, OpportunityForecastCategoryService>();
        services.AddScoped<IPipelineSettingRepository, PipelineSettingRepository>();
        services.AddScoped<IPipelineSettingService, PipelineSettingService>();
        services.AddScoped<IDuplicateRuleRepository, DuplicateRuleRepository>();
        services.AddScoped<IDuplicateRuleService, DuplicateRuleService>();
        services.AddScoped<IAssignmentRuleRepository, AssignmentRuleRepository>();
        services.AddScoped<IAssignmentRuleService, AssignmentRuleService>();
        services.AddScoped<ILeadActivityOutcomeRepository, LeadActivityOutcomeRepository>();
        services.AddScoped<ILeadActivityOutcomeService, LeadActivityOutcomeService>();
        services.AddScoped<ILeadActivityTypeRepository, LeadActivityTypeRepository>();
        services.AddScoped<ILeadActivityTypeService, LeadActivityTypeService>();
        services.AddScoped<ICrmCustomFieldRepository, CrmCustomFieldRepository>();
        services.AddScoped<ICrmCustomFieldService, CrmCustomFieldService>();
        services.AddScoped<IPricingMarketRulesRepository, PricingMarketRulesRepository>();
        services.AddScoped<IPricingMarketRulesService, PricingMarketRulesService>();

        // ── Account Config repositories & services ───────────────────
        services.AddScoped<IAccountTypeRepository, AccountTypeRepository>();
        services.AddScoped<IAccountTypeService, AccountTypeService>();
        services.AddScoped<IRelationshipTypeRepository, RelationshipTypeRepository>();
        services.AddScoped<IRelationshipTypeService, RelationshipTypeService>();
        services.AddScoped<IAccountReferenceOptionRepository, AccountReferenceOptionRepository>();
        services.AddScoped<IAccountReferenceOptionService, AccountReferenceOptionService>();
        services.AddScoped<IHouseholdSettingRepository, HouseholdSettingRepository>();
        services.AddScoped<IHouseholdSettingService, HouseholdSettingService>();
        services.AddScoped<ICommercialEntitySettingRepository, CommercialEntitySettingRepository>();
        services.AddScoped<ICommercialEntitySettingService, CommercialEntitySettingService>();
        services.AddScoped<IContactTypeRepository, ContactTypeRepository>();
        services.AddScoped<IContactTypeService, ContactTypeService>();
        services.AddScoped<IAccountCustomFieldRepository, AccountCustomFieldRepository>();
        services.AddScoped<IAccountCustomFieldService, AccountCustomFieldService>();

        // ── Policy Config repositories & services ────────────────────
        services.AddScoped<ICoverageTypeRepository, CoverageTypeRepository>();
        services.AddScoped<ICoverageTypeService, CoverageTypeService>();
        services.AddScoped<IPolicyCoverageRepository, PolicyCoverageRepository>();
        services.AddScoped<IPolicyCoverageService, PolicyCoverageService>();
        services.AddScoped<IPolicyStatusRepository, PolicyStatusRepository>();
        services.AddScoped<IPolicyStatusService, PolicyStatusService>();
        services.AddScoped<IEndorsementTypeRepository, EndorsementTypeRepository>();
        services.AddScoped<IEndorsementTypeService, EndorsementTypeService>();
        services.AddScoped<ICancellationReasonRepository, CancellationReasonRepository>();
        services.AddScoped<ICancellationReasonService, CancellationReasonService>();
        services.AddScoped<ICertificateSettingRepository, CertificateSettingRepository>();
        services.AddScoped<ICertificateSettingService, CertificateSettingService>();
        services.AddScoped<IIdCardSettingRepository, IdCardSettingRepository>();
        services.AddScoped<IIdCardSettingService, IdCardSettingService>();
        services.AddScoped<IPolicyCustomFieldRepository, PolicyCustomFieldRepository>();
        services.AddScoped<IPolicyCustomFieldService, PolicyCustomFieldService>();

        // ── Carrier Config repositories & services ───────────────────
        services.AddScoped<IMgaWholesalerRepository, MgaWholesalerRepository>();
        services.AddScoped<IMgaWholesalerService, MgaWholesalerService>();
        services.AddScoped<ICarrierContactRepository, CarrierContactRepository>();
        services.AddScoped<ICarrierContactService, CarrierContactService>();
        services.AddScoped<ICarrierAppointmentRepository, CarrierAppointmentRepository>();
        services.AddScoped<ICarrierAppointmentService, CarrierAppointmentService>();
        services.AddScoped<ICarrierPerformanceRepository, CarrierPerformanceRepository>();
        services.AddScoped<ICarrierPerformanceService, CarrierPerformanceService>();
        services.AddScoped<ICarrierSettingRepository, CarrierSettingRepository>();
        services.AddScoped<ICarrierSettingService, CarrierSettingService>();
        services.AddScoped<IMarketAccessRuleRepository, MarketAccessRuleRepository>();
        services.AddScoped<IMarketAccessRuleService, MarketAccessRuleService>();
        services.AddScoped<ICarrierDownloadMappingRepository, CarrierDownloadMappingRepository>();
        services.AddScoped<ICarrierDownloadMappingService, CarrierDownloadMappingService>();
        services.AddScoped<IWorkflowConfigRepository, WorkflowConfigRepository>();
        services.AddScoped<IWorkflowConfigService, WorkflowConfigService>();
        services.AddScoped<ICommunicationConfigRepository, CommunicationConfigRepository>();
        services.AddScoped<ICommunicationConfigService, CommunicationConfigService>();
        services.AddScoped<IDocumentConfigRepository, DocumentConfigRepository>();
        services.AddScoped<IDocumentConfigService, DocumentConfigService>();
        services.AddScoped<IBillingConfigRepository, BillingConfigRepository>();
        services.AddScoped<IBillingConfigService, BillingConfigService>();
        services.AddScoped<ICommissionConfigRepository, CommissionConfigRepository>();
        services.AddScoped<ICommissionConfigService, CommissionConfigService>();
        services.AddScoped<IMarketingConfigRepository, MarketingConfigRepository>();
        services.AddScoped<IMarketingConfigService, MarketingConfigService>();
        services.AddScoped<IPortalConfigRepository, PortalConfigRepository>();
        services.AddScoped<IPortalConfigService, PortalConfigService>();
        services.AddScoped<IIntegrationConfigRepository, IntegrationConfigRepository>();
        services.AddScoped<IIntegrationConfigService, IntegrationConfigService>();
        services.AddScoped<IAiConfigRepository, AiConfigRepository>();
        services.AddScoped<IAiConfigService, AiConfigService>();
        services.AddScoped<IDataConfigRepository, DataConfigRepository>();
        services.AddScoped<IDataConfigService, DataConfigService>();
        services.AddScoped<ISubscriptionConfigRepository, SubscriptionConfigRepository>();
        services.AddScoped<ISubscriptionConfigService, SubscriptionConfigService>();
        services.AddScoped<ITenantConfigRepository, TenantConfigRepository>();
        services.AddScoped<ITenantConfigService, TenantConfigService>();

        return services;
    }
}
