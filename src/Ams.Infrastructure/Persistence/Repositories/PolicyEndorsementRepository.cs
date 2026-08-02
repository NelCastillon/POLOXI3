using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyEndorsements;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyEndorsementRepository : IPolicyEndorsementRepository
{
    private const string EndorsementColumns = @"EndorsementId, TenantId, PolicyId, AccountId, EndorsementNumber, PolicyNumber, AccountName, LineOfBusiness,
        Carrier, EndorsementType, RequestSourceCode, ChangeCategoryCode, Description, EffectiveDate, ExpirationDate, RetroactiveDate,
        DiscoveryDate, RequestedDateUtc, PremiumDelta, TaxFeeDelta, TotalCostDelta, ProratedPremiumDelta, Status, Priority,
        RequestedByName, RequestedByEmail, RequestedByPhone, ClientContactName, ClientContactEmail, ClientContactPhone,
        AssignedToName, UnderwriterName, UnderwriterEmail, CarrierSubmissionDateUtc, CarrierResponseDueDate, CarrierReferenceNumber,
        BrokerOfRecordRequired, AgentAuthorityCode, ApprovalLevelCode, ApprovedByName, IssuedByName, BillingImpactCode,
        CommissionImpactCode, BillingInstruction, DocumentDeliveryCode, CertificateRequired, FormsRequired, AcordFormNumbers,
        ExternalReferenceNumber, ComplianceReviewRequired, EoExposureNotes, InternalNotes, ClientFacingNotes, Reason,
        RequiredDocuments, WorkflowStage, DueDate, ApprovedDateUtc, IssuedDateUtc, IsUrgent, IsArchived";

    private readonly ISqlConnectionFactory _connectionFactory;

    public PolicyEndorsementRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PolicyEndorsementCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {EndorsementColumns}
FROM Policy.PolicyEndorsement
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsArchived = 0
ORDER BY IsUrgent DESC, DueDate, RequestedDateUtc DESC;

SELECT ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc
FROM Policy.PolicyEndorsementActivity
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;

SELECT DeltaId, EndorsementId, TenantId, FieldName, BeforeValue, AfterValue, NumericDelta
FROM Policy.PolicyEndorsementDelta
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY CreatedDateUtc;

SELECT OptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, IsActive, SortOrder
FROM Policy.PolicyEndorsementOption
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY OptionGroupCode, SortOrder, DisplayName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new PolicyEndorsementCenterDto
        {
            Endorsements = (await multi.ReadAsync<PolicyEndorsementDto>()).AsList(),
            Activities = (await multi.ReadAsync<PolicyEndorsementActivityDto>()).AsList(),
            Deltas = (await multi.ReadAsync<PolicyEndorsementDeltaDto>()).AsList(),
            Options = (await multi.ReadAsync<PolicyEndorsementOptionDto>()).AsList()
        };
    }

    public async Task<IReadOnlyList<PolicyEndorsementOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT OptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, IsActive, SortOrder
FROM Policy.PolicyEndorsementOption
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY OptionGroupCode, SortOrder, DisplayName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PolicyEndorsementOptionDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<PolicyEndorsementDetailDto?> GetDetailAsync(Guid tenantId, Guid endorsementId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {EndorsementColumns}
FROM Policy.PolicyEndorsement
WHERE TenantId = @TenantId AND EndorsementId = @EndorsementId AND IsDeleted = 0;

SELECT ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc
FROM Policy.PolicyEndorsementActivity
WHERE TenantId = @TenantId AND EndorsementId = @EndorsementId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;

SELECT DeltaId, EndorsementId, TenantId, FieldName, BeforeValue, AfterValue, NumericDelta
FROM Policy.PolicyEndorsementDelta
WHERE TenantId = @TenantId AND EndorsementId = @EndorsementId AND IsDeleted = 0
ORDER BY CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, EndorsementId = endorsementId }, cancellationToken: cancellationToken));
        var endorsement = await multi.ReadSingleOrDefaultAsync<PolicyEndorsementDto>();
        if (endorsement is null) return null;

        return new PolicyEndorsementDetailDto
        {
            Endorsement = endorsement,
            Activities = (await multi.ReadAsync<PolicyEndorsementActivityDto>()).AsList(),
            Deltas = (await multi.ReadAsync<PolicyEndorsementDeltaDto>()).AsList()
        };
    }

    public async Task<Guid> CreateAsync(CreatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NextNumber INT = ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyEndorsement WHERE TenantId = @TenantId), 1);
DECLARE @EndorsementNumber NVARCHAR(50) = CONCAT(N'END-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(@NextNumber, N'0000'));

INSERT INTO Policy.PolicyEndorsement
(EndorsementId, TenantId, PolicyId, AccountId, EndorsementNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, EndorsementType,
 RequestSourceCode, ChangeCategoryCode, Description, EffectiveDate, ExpirationDate, RetroactiveDate, DiscoveryDate, RequestedDateUtc,
 PremiumDelta, TaxFeeDelta, TotalCostDelta, ProratedPremiumDelta, Status, Priority, RequestedByName, RequestedByEmail, RequestedByPhone,
 ClientContactName, ClientContactEmail, ClientContactPhone, AssignedToName, UnderwriterName, UnderwriterEmail, CarrierSubmissionDateUtc,
 CarrierResponseDueDate, CarrierReferenceNumber, BrokerOfRecordRequired, AgentAuthorityCode, ApprovalLevelCode, ApprovedByName,
 IssuedByName, BillingImpactCode, CommissionImpactCode, BillingInstruction, DocumentDeliveryCode, CertificateRequired, FormsRequired,
 AcordFormNumbers, ExternalReferenceNumber, ComplianceReviewRequired, EoExposureNotes, InternalNotes, ClientFacingNotes, Reason,
 RequiredDocuments, WorkflowStage, DueDate, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@EndorsementId, @TenantId, @PolicyId, @AccountId, @EndorsementNumber, @PolicyNumber, @AccountName, @LineOfBusiness, @Carrier, @EndorsementType,
 COALESCE(NULLIF(@RequestSourceCode, N''), N'AgencyRequest'), COALESCE(NULLIF(@ChangeCategoryCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NonPremium' ELSE N'PremiumBearing' END),
 @Description, @EffectiveDate, @ExpirationDate, @RetroactiveDate, @DiscoveryDate, SYSUTCDATETIME(), @PremiumDelta, @TaxFeeDelta,
 CASE WHEN @TotalCostDelta = 0 THEN @PremiumDelta + @TaxFeeDelta ELSE @TotalCostDelta END,
 CASE WHEN @ProratedPremiumDelta = 0 THEN @PremiumDelta ELSE @ProratedPremiumDelta END,
 N'Pending', @Priority, @RequestedByName, @RequestedByEmail, @RequestedByPhone, @ClientContactName, @ClientContactEmail,
 @ClientContactPhone, @AssignedToName, @UnderwriterName, @UnderwriterEmail, @CarrierSubmissionDateUtc, @CarrierResponseDueDate,
 @CarrierReferenceNumber, @BrokerOfRecordRequired, COALESCE(NULLIF(@AgentAuthorityCode, N''), N'CarrierApprovalRequired'),
 COALESCE(NULLIF(@ApprovalLevelCode, N''), CASE WHEN ABS(@PremiumDelta) >= 5000 THEN N'ManagerApproval' ELSE N'StandardAuthority' END),
 @ApprovedByName, @IssuedByName, COALESCE(NULLIF(@BillingImpactCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NoBillingImpact' ELSE N'BillInstallment' END),
 COALESCE(NULLIF(@CommissionImpactCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NoCommissionImpact' ELSE N'RecalculateCommission' END),
 @BillingInstruction, COALESCE(NULLIF(@DocumentDeliveryCode, N''), N'PortalEmail'), @CertificateRequired, @FormsRequired,
 @AcordFormNumbers, @ExternalReferenceNumber, @ComplianceReviewRequired, @EoExposureNotes, @InternalNotes, @ClientFacingNotes,
 @Reason, @RequiredDocuments, N'Intake', @DueDate, @IsUrgent, 0, SYSUTCDATETIME(), @CreatedByUserId, 0);

INSERT INTO Policy.PolicyEndorsementActivity
(ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @EndorsementId, @TenantId, N'Created', N'Endorsement request created', @Description, @RequestedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);

INSERT INTO Policy.PolicyEndorsementDelta
(DeltaId, EndorsementId, TenantId, FieldName, BeforeValue, AfterValue, NumericDelta, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @EndorsementId, @TenantId, N'Annual Premium', N'Current policy premium', FORMAT(@PremiumDelta, N'+$#,##0;-$#,##0;$0'), @PremiumDelta, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            EndorsementId = id,
            request.TenantId,
            request.PolicyId,
            request.AccountId,
            request.PolicyNumber,
            request.AccountName,
            request.LineOfBusiness,
            request.Carrier,
            request.EndorsementType,
            request.RequestSourceCode,
            request.ChangeCategoryCode,
            request.Description,
            request.EffectiveDate,
            request.ExpirationDate,
            request.RetroactiveDate,
            request.DiscoveryDate,
            request.PremiumDelta,
            request.TaxFeeDelta,
            request.TotalCostDelta,
            request.ProratedPremiumDelta,
            request.Priority,
            request.RequestedByName,
            request.RequestedByEmail,
            request.RequestedByPhone,
            request.ClientContactName,
            request.ClientContactEmail,
            request.ClientContactPhone,
            request.AssignedToName,
            request.UnderwriterName,
            request.UnderwriterEmail,
            request.CarrierSubmissionDateUtc,
            request.CarrierResponseDueDate,
            request.CarrierReferenceNumber,
            request.BrokerOfRecordRequired,
            request.AgentAuthorityCode,
            request.ApprovalLevelCode,
            request.ApprovedByName,
            request.IssuedByName,
            request.BillingImpactCode,
            request.CommissionImpactCode,
            request.BillingInstruction,
            request.DocumentDeliveryCode,
            request.CertificateRequired,
            request.FormsRequired,
            request.AcordFormNumbers,
            request.ExternalReferenceNumber,
            request.ComplianceReviewRequired,
            request.EoExposureNotes,
            request.InternalNotes,
            request.ClientFacingNotes,
            request.Reason,
            request.RequiredDocuments,
            request.DueDate,
            request.IsUrgent,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid endorsementId, UpdatePolicyEndorsementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyEndorsement
SET EndorsementType = @EndorsementType,
    RequestSourceCode = COALESCE(NULLIF(@RequestSourceCode, N''), RequestSourceCode),
    ChangeCategoryCode = COALESCE(NULLIF(@ChangeCategoryCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NonPremium' ELSE N'PremiumBearing' END),
    Description = @Description,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    RetroactiveDate = @RetroactiveDate,
    DiscoveryDate = @DiscoveryDate,
    PremiumDelta = @PremiumDelta,
    TaxFeeDelta = @TaxFeeDelta,
    TotalCostDelta = CASE WHEN @TotalCostDelta = 0 THEN @PremiumDelta + @TaxFeeDelta ELSE @TotalCostDelta END,
    ProratedPremiumDelta = CASE WHEN @ProratedPremiumDelta = 0 THEN @PremiumDelta ELSE @ProratedPremiumDelta END,
    Priority = @Priority,
    AssignedToName = @AssignedToName,
    UnderwriterName = @UnderwriterName,
    UnderwriterEmail = @UnderwriterEmail,
    CarrierSubmissionDateUtc = @CarrierSubmissionDateUtc,
    CarrierResponseDueDate = @CarrierResponseDueDate,
    CarrierReferenceNumber = @CarrierReferenceNumber,
    BrokerOfRecordRequired = @BrokerOfRecordRequired,
    AgentAuthorityCode = COALESCE(NULLIF(@AgentAuthorityCode, N''), AgentAuthorityCode),
    ApprovalLevelCode = COALESCE(NULLIF(@ApprovalLevelCode, N''), CASE WHEN ABS(@PremiumDelta) >= 5000 THEN N'ManagerApproval' ELSE N'StandardAuthority' END),
    ApprovedByName = @ApprovedByName,
    IssuedByName = @IssuedByName,
    BillingImpactCode = COALESCE(NULLIF(@BillingImpactCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NoBillingImpact' ELSE N'BillInstallment' END),
    CommissionImpactCode = COALESCE(NULLIF(@CommissionImpactCode, N''), CASE WHEN @PremiumDelta = 0 THEN N'NoCommissionImpact' ELSE N'RecalculateCommission' END),
    BillingInstruction = @BillingInstruction,
    DocumentDeliveryCode = COALESCE(NULLIF(@DocumentDeliveryCode, N''), DocumentDeliveryCode),
    CertificateRequired = @CertificateRequired,
    FormsRequired = @FormsRequired,
    AcordFormNumbers = @AcordFormNumbers,
    ExternalReferenceNumber = @ExternalReferenceNumber,
    ComplianceReviewRequired = @ComplianceReviewRequired,
    EoExposureNotes = @EoExposureNotes,
    InternalNotes = @InternalNotes,
    ClientFacingNotes = @ClientFacingNotes,
    Reason = @Reason,
    RequiredDocuments = @RequiredDocuments,
    DueDate = @DueDate,
    IsUrgent = @IsUrgent,
    WorkflowStage = CASE WHEN Status = N'Pending' THEN N'Intake' ELSE WorkflowStage END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EndorsementId = @EndorsementId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            EndorsementId = endorsementId,
            request.EndorsementType,
            request.RequestSourceCode,
            request.ChangeCategoryCode,
            request.Description,
            request.EffectiveDate,
            request.ExpirationDate,
            request.RetroactiveDate,
            request.DiscoveryDate,
            request.PremiumDelta,
            request.TaxFeeDelta,
            request.TotalCostDelta,
            request.ProratedPremiumDelta,
            request.Priority,
            request.AssignedToName,
            request.UnderwriterName,
            request.UnderwriterEmail,
            request.CarrierSubmissionDateUtc,
            request.CarrierResponseDueDate,
            request.CarrierReferenceNumber,
            request.BrokerOfRecordRequired,
            request.AgentAuthorityCode,
            request.ApprovalLevelCode,
            request.ApprovedByName,
            request.IssuedByName,
            request.BillingImpactCode,
            request.CommissionImpactCode,
            request.BillingInstruction,
            request.DocumentDeliveryCode,
            request.CertificateRequired,
            request.FormsRequired,
            request.AcordFormNumbers,
            request.ExternalReferenceNumber,
            request.ComplianceReviewRequired,
            request.EoExposureNotes,
            request.InternalNotes,
            request.ClientFacingNotes,
            request.Reason,
            request.RequiredDocuments,
            request.DueDate,
            request.IsUrgent,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid endorsementId, UpdatePolicyEndorsementStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.PolicyEndorsement WHERE EndorsementId = @EndorsementId AND IsDeleted = 0);

UPDATE Policy.PolicyEndorsement
SET Status = @Status,
    WorkflowStage = CASE @Status
        WHEN N'Pending' THEN N'Intake'
        WHEN N'In Review' THEN N'Underwriting Review'
        WHEN N'Approved' THEN N'Approved Pending Issue'
        WHEN N'Declined' THEN N'Closed Declined'
        WHEN N'Issued' THEN N'Issued to Policy'
        WHEN N'Info Needed' THEN N'Awaiting Information'
        ELSE WorkflowStage
    END,
    ApprovedDateUtc = CASE WHEN @Status = N'Approved' THEN SYSUTCDATETIME() ELSE ApprovedDateUtc END,
    IssuedDateUtc = CASE WHEN @Status = N'Issued' THEN SYSUTCDATETIME() ELSE IssuedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EndorsementId = @EndorsementId AND IsDeleted = 0;

INSERT INTO Policy.PolicyEndorsementActivity
(ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @EndorsementId, @TenantId, N'Status', CONCAT(N'Status changed to ', @Status), @Notes, @CreatedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            EndorsementId = endorsementId,
            request.Status,
            request.Notes,
            request.CreatedByName,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddActivityAsync(AddPolicyEndorsementActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.PolicyEndorsement WHERE EndorsementId = @EndorsementId AND IsDeleted = 0);
INSERT INTO Policy.PolicyEndorsementActivity
(ActivityId, EndorsementId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@ActivityId, @EndorsementId, @TenantId, @ActivityType, @Subject, @Notes, @CreatedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);
UPDATE Policy.PolicyEndorsement
SET ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @CreatedByUserId
WHERE EndorsementId = @EndorsementId AND IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ActivityId = id,
            request.EndorsementId,
            request.ActivityType,
            request.Subject,
            request.Notes,
            request.CreatedByName,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> UpsertDeltaAsync(UpsertPolicyEndorsementDeltaRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.PolicyEndorsement WHERE EndorsementId = @EndorsementId AND IsDeleted = 0);
INSERT INTO Policy.PolicyEndorsementDelta
(DeltaId, EndorsementId, TenantId, FieldName, BeforeValue, AfterValue, NumericDelta, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@DeltaId, @EndorsementId, @TenantId, @FieldName, @BeforeValue, @AfterValue, @NumericDelta, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            DeltaId = id,
            request.EndorsementId,
            request.FieldName,
            request.BeforeValue,
            request.AfterValue,
            request.NumericDelta,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ArchiveAsync(Guid endorsementId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyEndorsement
SET IsArchived = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE EndorsementId = @EndorsementId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { EndorsementId = endorsementId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
