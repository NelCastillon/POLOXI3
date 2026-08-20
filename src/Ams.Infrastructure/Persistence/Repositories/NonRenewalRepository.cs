using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.NonRenewals;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class NonRenewalRepository : INonRenewalRepository
{
    private const string NonRenewalColumns = @"NonRenewalId, TenantId, PolicyId, AccountId, NonRenewalNumber, PolicyNumber, AccountName, CarrierName,
        LineOfBusiness, StateCode, PolicyExpirationDate, StatusCode, ReasonCode, InitiatedByCode,
        CarrierNoticeDate, CarrierNoticeMethodCode, CarrierNoticeReference, CarrierNoticeSummary,
        RequiredNoticeDays, NoticeDeadlineDate, IsNoticeCompliant,
        InsuredNotifiedDate, InsuredNotificationMethodCode, InsuredNotificationProofReference, InsuredNotificationSentByName,
        RemarketRecommended, RemarketSubmissionId, ResolutionSummary, AssignedToUserId, AssignedToName,
        CompletedDateUtc, Notes, IsUrgent, IsArchived, CreatedDateUtc";

    private readonly ISqlConnectionFactory _connectionFactory;

    public NonRenewalRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<NonRenewalCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {NonRenewalColumns}
FROM Policy.NonRenewal
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsArchived = 0
ORDER BY IsUrgent DESC, NoticeDeadlineDate, PolicyExpirationDate;

SELECT NonRenewalStatusId, TenantId, StatusCode, StatusName, Description, ColorHex, IsTerminal, IsDefault, SortOrder
FROM Policy.NonRenewalStatus
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY SortOrder;

SELECT NonRenewalReasonId, TenantId, ReasonCode, ReasonName, Description, CategoryCode, IsRemarketRecommended, SortOrder
FROM Policy.NonRenewalReason
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY SortOrder;

SELECT NonRenewalStateRequirementId, TenantId, StateCode, StateName, LineCategoryCode, MinimumNoticeDays, InsuredNoticeDays, Notes
FROM Policy.NonRenewalStateRequirement
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsActive = 1
ORDER BY StateCode, LineCategoryCode;

SELECT bp.PolicyId, bp.AccountId, ISNULL(bp.PolicyNumber, N'') AS PolicyNumber,
       ISNULL(a.AccountName, N'Unknown Account') AS AccountName, c.CarrierName, bp.LineOfBusiness,
       bp.EffectiveDate, bp.ExpirationDate
FROM Submissions.BoundPolicy bp
LEFT JOIN Client.Account a ON a.AccountId = bp.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = bp.CarrierId
WHERE bp.TenantId = @TenantId AND bp.IsDeleted = 0
ORDER BY bp.ExpirationDate, bp.PolicyNumber;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new NonRenewalCenterDto
        {
            NonRenewals = (await multi.ReadAsync<NonRenewalDto>()).AsList(),
            Statuses = (await multi.ReadAsync<NonRenewalStatusDto>()).AsList(),
            Reasons = (await multi.ReadAsync<NonRenewalReasonDto>()).AsList(),
            StateRequirements = (await multi.ReadAsync<NonRenewalStateRequirementDto>()).AsList(),
            EligiblePolicies = (await multi.ReadAsync<NonRenewalEligiblePolicyDto>()).AsList()
        };
    }

    public async Task<NonRenewalDetailDto?> GetDetailAsync(Guid nonRenewalId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {NonRenewalColumns}
FROM Policy.NonRenewal
WHERE NonRenewalId = @NonRenewalId AND IsDeleted = 0;

SELECT ActivityId, TenantId, NonRenewalId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc
FROM Policy.NonRenewalActivity
WHERE NonRenewalId = @NonRenewalId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { NonRenewalId = nonRenewalId }, cancellationToken: cancellationToken));
        var nonRenewal = await multi.ReadSingleOrDefaultAsync<NonRenewalDto>();
        if (nonRenewal is null) return null;

        return new NonRenewalDetailDto
        {
            NonRenewal = nonRenewal,
            Activities = (await multi.ReadAsync<NonRenewalActivityDto>()).AsList()
        };
    }

    public async Task<Guid> CreateAsync(CreateNonRenewalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NextNumber INT = ISNULL((SELECT COUNT(1) + 1 FROM Policy.NonRenewal WHERE TenantId = @TenantId), 1);
DECLARE @NonRenewalNumber NVARCHAR(50) = CONCAT(N'NRN-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(@NextNumber, N'0000'));

-- State-mandated notice requirement drives deadline tracking (DB-backed, tenant scoped).
DECLARE @RequiredNoticeDays INT = (SELECT TOP 1 MinimumNoticeDays
                                   FROM Policy.NonRenewalStateRequirement
                                   WHERE TenantId = @TenantId AND StateCode = @StateCode AND IsDeleted = 0 AND IsActive = 1
                                   ORDER BY CASE LineCategoryCode WHEN N'All' THEN 1 ELSE 0 END);
DECLARE @NoticeDeadlineDate DATE = CASE WHEN @RequiredNoticeDays IS NOT NULL AND @PolicyExpirationDate IS NOT NULL
                                        THEN DATEADD(DAY, -@RequiredNoticeDays, @PolicyExpirationDate) END;
DECLARE @IsNoticeCompliant BIT = CASE WHEN @NoticeDeadlineDate IS NULL OR @CarrierNoticeDate IS NULL THEN NULL
                                      WHEN @CarrierNoticeDate <= @NoticeDeadlineDate THEN 1 ELSE 0 END;
DECLARE @RemarketRecommended BIT = ISNULL((SELECT TOP 1 IsRemarketRecommended
                                           FROM Policy.NonRenewalReason
                                           WHERE TenantId = @TenantId AND ReasonCode = @ReasonCode AND IsDeleted = 0), 0);

INSERT INTO Policy.NonRenewal
(NonRenewalId, TenantId, PolicyId, AccountId, NonRenewalNumber, PolicyNumber, AccountName, CarrierName, LineOfBusiness, StateCode,
 PolicyExpirationDate, StatusCode, ReasonCode, InitiatedByCode,
 CarrierNoticeDate, CarrierNoticeMethodCode, CarrierNoticeReference, CarrierNoticeSummary,
 RequiredNoticeDays, NoticeDeadlineDate, IsNoticeCompliant, RemarketRecommended,
 AssignedToUserId, AssignedToName, Notes, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@NonRenewalId, @TenantId, @PolicyId, @AccountId, @NonRenewalNumber, @PolicyNumber, @AccountName, @CarrierName, @LineOfBusiness, @StateCode,
 @PolicyExpirationDate, N'NoticeReceived', @ReasonCode, @InitiatedByCode,
 @CarrierNoticeDate, @CarrierNoticeMethodCode, @CarrierNoticeReference, @CarrierNoticeSummary,
 @RequiredNoticeDays, @NoticeDeadlineDate, @IsNoticeCompliant, @RemarketRecommended,
 @AssignedToUserId, @AssignedToName, @Notes, @IsUrgent, 0, SYSUTCDATETIME(), @CreatedByUserId, 0);

INSERT INTO Policy.NonRenewalActivity
(ActivityId, TenantId, NonRenewalId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @TenantId, @NonRenewalId, N'NoticeReceived', CONCAT(N'Carrier non-renewal notice logged for policy ', @PolicyNumber),
 @CarrierNoticeSummary, ISNULL(@AssignedToName, N'System'), SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            NonRenewalId = id,
            request.TenantId,
            request.PolicyId,
            request.AccountId,
            request.PolicyNumber,
            request.AccountName,
            request.CarrierName,
            request.LineOfBusiness,
            request.StateCode,
            request.PolicyExpirationDate,
            request.ReasonCode,
            request.InitiatedByCode,
            request.CarrierNoticeDate,
            request.CarrierNoticeMethodCode,
            request.CarrierNoticeReference,
            request.CarrierNoticeSummary,
            request.AssignedToUserId,
            request.AssignedToName,
            request.Notes,
            request.IsUrgent,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid nonRenewalId, UpdateNonRenewalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE nr SET
    ReasonCode = @ReasonCode,
    InitiatedByCode = @InitiatedByCode,
    CarrierNoticeDate = @CarrierNoticeDate,
    CarrierNoticeMethodCode = @CarrierNoticeMethodCode,
    CarrierNoticeReference = @CarrierNoticeReference,
    CarrierNoticeSummary = @CarrierNoticeSummary,
    IsNoticeCompliant = CASE WHEN nr.NoticeDeadlineDate IS NULL OR @CarrierNoticeDate IS NULL THEN NULL
                             WHEN @CarrierNoticeDate <= nr.NoticeDeadlineDate THEN 1 ELSE 0 END,
    RemarketRecommended = @RemarketRecommended,
    RemarketSubmissionId = @RemarketSubmissionId,
    ResolutionSummary = @ResolutionSummary,
    AssignedToUserId = @AssignedToUserId,
    AssignedToName = @AssignedToName,
    Notes = @Notes,
    IsUrgent = @IsUrgent,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
FROM Policy.NonRenewal nr
WHERE nr.NonRenewalId = @NonRenewalId AND nr.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            NonRenewalId = nonRenewalId,
            request.ReasonCode,
            request.InitiatedByCode,
            request.CarrierNoticeDate,
            request.CarrierNoticeMethodCode,
            request.CarrierNoticeReference,
            request.CarrierNoticeSummary,
            request.RemarketRecommended,
            request.RemarketSubmissionId,
            request.ResolutionSummary,
            request.AssignedToUserId,
            request.AssignedToName,
            request.Notes,
            request.IsUrgent,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid nonRenewalId, UpdateNonRenewalStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.NonRenewal WHERE NonRenewalId = @NonRenewalId AND IsDeleted = 0);
DECLARE @IsTerminal BIT = ISNULL((SELECT TOP 1 IsTerminal FROM Policy.NonRenewalStatus
                                  WHERE TenantId = @TenantId AND StatusCode = @StatusCode AND IsDeleted = 0), 0);

UPDATE Policy.NonRenewal SET
    StatusCode = @StatusCode,
    CompletedDateUtc = CASE WHEN @IsTerminal = 1 THEN SYSUTCDATETIME() ELSE NULL END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE NonRenewalId = @NonRenewalId AND IsDeleted = 0;

INSERT INTO Policy.NonRenewalActivity
(ActivityId, TenantId, NonRenewalId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @TenantId, @NonRenewalId, N'StatusChange', CONCAT(N'Status changed to ', @StatusCode), @Notes, @CreatedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            NonRenewalId = nonRenewalId,
            request.StatusCode,
            request.Notes,
            request.CreatedByName,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task RecordInsuredNotificationAsync(Guid nonRenewalId, RecordInsuredNotificationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.NonRenewal WHERE NonRenewalId = @NonRenewalId AND IsDeleted = 0);

UPDATE Policy.NonRenewal SET
    InsuredNotifiedDate = @InsuredNotifiedDate,
    InsuredNotificationMethodCode = @InsuredNotificationMethodCode,
    InsuredNotificationProofReference = @InsuredNotificationProofReference,
    InsuredNotificationSentByName = @InsuredNotificationSentByName,
    StatusCode = CASE WHEN StatusCode IN (N'NoticeReceived', N'UnderReview', N'InsuredNotification') THEN N'InsuredNotified' ELSE StatusCode END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE NonRenewalId = @NonRenewalId AND IsDeleted = 0;

INSERT INTO Policy.NonRenewalActivity
(ActivityId, TenantId, NonRenewalId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @TenantId, @NonRenewalId, N'InsuredNotified',
 CONCAT(N'Insured notified via ', @InsuredNotificationMethodCode, N'; proof: ', @InsuredNotificationProofReference),
 @Notes, @InsuredNotificationSentByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            NonRenewalId = nonRenewalId,
            request.InsuredNotifiedDate,
            request.InsuredNotificationMethodCode,
            request.InsuredNotificationProofReference,
            request.InsuredNotificationSentByName,
            request.Notes,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddActivityAsync(AddNonRenewalActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Policy.NonRenewalActivity
(ActivityId, TenantId, NonRenewalId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@ActivityId, @TenantId, @NonRenewalId, @ActivityType, @Subject, @Notes, @CreatedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ActivityId = id,
            request.TenantId,
            request.NonRenewalId,
            request.ActivityType,
            request.Subject,
            request.Notes,
            request.CreatedByName,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ArchiveAsync(Guid nonRenewalId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.NonRenewal SET
    IsArchived = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE NonRenewalId = @NonRenewalId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { NonRenewalId = nonRenewalId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
