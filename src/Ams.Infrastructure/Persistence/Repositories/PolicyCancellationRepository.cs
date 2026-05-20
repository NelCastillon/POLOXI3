using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PolicyCancellations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyCancellationRepository : IPolicyCancellationRepository
{
    private const string CancellationColumns = @"CancellationId, TenantId, PolicyId, AccountId, CancellationNumber, PolicyNumber, AccountName, LineOfBusiness,
        Carrier, CancellationReason, CancellationType, RequestType, RequestDateUtc, EffectiveDate, CancellationDate, ReinstatementDate,
        ReturnPremium, PremiumDue, Status, Priority, RequestedByName, AssignedToName, ApprovedByName, ReinstatedByName, Notes,
        WorkflowStage, DueDate, ApprovedDateUtc, IsUrgent, IsArchived";

    private readonly ISqlConnectionFactory _connectionFactory;

    public PolicyCancellationRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PolicyCancellationCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT CancellationId, TenantId, PolicyId, AccountId, CancellationNumber, PolicyNumber, AccountName, LineOfBusiness,
       Carrier, CancellationReason, CancellationType, RequestType, RequestDateUtc, EffectiveDate, CancellationDate, ReinstatementDate,
       ReturnPremium, PremiumDue, Status, Priority, RequestedByName, AssignedToName, ApprovedByName, ReinstatedByName, Notes,
       WorkflowStage, DueDate, ApprovedDateUtc, IsUrgent, IsArchived
FROM Policy.PolicyCancellation
WHERE TenantId = @TenantId AND IsDeleted = 0 AND IsArchived = 0
ORDER BY IsUrgent DESC, DueDate, RequestDateUtc DESC;

SELECT ActivityId, CancellationId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc
FROM Policy.PolicyCancellationActivity
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new PolicyCancellationCenterDto
        {
            Cancellations = (await multi.ReadAsync<PolicyCancellationDto>()).AsList(),
            Activities = (await multi.ReadAsync<PolicyCancellationActivityDto>()).AsList()
        };
    }

    public async Task<PolicyCancellationDetailDto?> GetDetailAsync(Guid cancellationId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {CancellationColumns}
FROM Policy.PolicyCancellation
WHERE CancellationId = @CancellationId AND IsDeleted = 0;

SELECT ActivityId, CancellationId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc
FROM Policy.PolicyCancellationActivity
WHERE CancellationId = @CancellationId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { CancellationId = cancellationId }, cancellationToken: cancellationToken));
        var cancellation = await multi.ReadSingleOrDefaultAsync<PolicyCancellationDto>();
        if (cancellation is null) return null;

        return new PolicyCancellationDetailDto
        {
            Cancellation = cancellation,
            Activities = (await multi.ReadAsync<PolicyCancellationActivityDto>()).AsList()
        };
    }

    public async Task<Guid> CreateAsync(CreatePolicyCancellationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NextNumber INT = ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyCancellation WHERE TenantId = @TenantId), 1);
DECLARE @CancellationNumber NVARCHAR(50) = CONCAT(CASE WHEN @RequestType = N'Reinstatement' THEN N'REI-' ELSE N'CAN-' END, FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(@NextNumber, N'0000'));
DECLARE @Status NVARCHAR(40) = CASE WHEN @RequestType = N'Reinstatement' THEN N'Reinstatement Pending' ELSE N'Pending' END;
DECLARE @WorkflowStage NVARCHAR(80) = CASE WHEN @RequestType = N'Reinstatement' THEN N'Reinstatement Review' ELSE N'Cancellation Intake' END;

INSERT INTO Policy.PolicyCancellation
(CancellationId, TenantId, PolicyId, AccountId, CancellationNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, CancellationReason,
 CancellationType, RequestType, RequestDateUtc, EffectiveDate, CancellationDate, ReinstatementDate, ReturnPremium, PremiumDue, Status, Priority,
 RequestedByName, AssignedToName, Notes, WorkflowStage, DueDate, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@CancellationId, @TenantId, @PolicyId, @AccountId, @CancellationNumber, @PolicyNumber, @AccountName, @LineOfBusiness, @Carrier, @CancellationReason,
 @CancellationType, @RequestType, SYSUTCDATETIME(), @EffectiveDate,
 CASE WHEN @RequestType = N'Cancellation' THEN @EffectiveDate ELSE DATEADD(day, -30, @EffectiveDate) END,
 CASE WHEN @RequestType = N'Reinstatement' THEN @EffectiveDate ELSE NULL END,
 @ReturnPremium, @PremiumDue, @Status, @Priority, @RequestedByName, @AssignedToName, @Notes, @WorkflowStage, @DueDate, @IsUrgent, 0, SYSUTCDATETIME(), @CreatedByUserId, 0);

INSERT INTO Policy.PolicyCancellationActivity
(ActivityId, CancellationId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @CancellationId, @TenantId, N'Created', CONCAT(@RequestType, N' request created'), @Notes, @RequestedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CancellationId = id,
            request.TenantId,
            request.PolicyId,
            request.AccountId,
            request.PolicyNumber,
            request.AccountName,
            request.LineOfBusiness,
            request.Carrier,
            request.CancellationReason,
            request.CancellationType,
            request.RequestType,
            request.EffectiveDate,
            request.ReturnPremium,
            request.PremiumDue,
            request.Priority,
            request.RequestedByName,
            request.AssignedToName,
            request.Notes,
            request.DueDate,
            request.IsUrgent,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid cancellationId, UpdatePolicyCancellationRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCancellation
SET CancellationReason = @CancellationReason,
    CancellationType = @CancellationType,
    EffectiveDate = @EffectiveDate,
    ReturnPremium = @ReturnPremium,
    PremiumDue = @PremiumDue,
    Priority = @Priority,
    AssignedToName = @AssignedToName,
    Notes = @Notes,
    DueDate = @DueDate,
    IsUrgent = @IsUrgent,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CancellationId = @CancellationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CancellationId = cancellationId,
            request.CancellationReason,
            request.CancellationType,
            request.EffectiveDate,
            request.ReturnPremium,
            request.PremiumDue,
            request.Priority,
            request.AssignedToName,
            request.Notes,
            request.DueDate,
            request.IsUrgent,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(Guid cancellationId, UpdatePolicyCancellationStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.PolicyCancellation WHERE CancellationId = @CancellationId AND IsDeleted = 0);

UPDATE Policy.PolicyCancellation
SET Status = @Status,
    WorkflowStage = CASE @Status
        WHEN N'Pending' THEN N'Cancellation Intake'
        WHEN N'Under Review' THEN N'Carrier / Service Review'
        WHEN N'Approved' THEN N'Approved Pending Cancellation'
        WHEN N'Cancelled' THEN N'Cancelled Policy'
        WHEN N'Denied' THEN N'Closed Denied'
        WHEN N'Rescinded' THEN N'Rescinded by Client'
        WHEN N'Reinstatement Pending' THEN N'Reinstatement Review'
        WHEN N'Reinstated' THEN N'Policy Reinstated'
        ELSE WorkflowStage
    END,
    ApprovedByName = CASE WHEN @Status IN (N'Approved', N'Cancelled', N'Reinstated') THEN @CreatedByName ELSE ApprovedByName END,
    ReinstatedByName = CASE WHEN @Status = N'Reinstated' THEN @CreatedByName ELSE ReinstatedByName END,
    ApprovedDateUtc = CASE WHEN @Status IN (N'Approved', N'Cancelled', N'Reinstated') THEN SYSUTCDATETIME() ELSE ApprovedDateUtc END,
    CancellationDate = CASE WHEN @Status = N'Cancelled' THEN EffectiveDate ELSE CancellationDate END,
    ReinstatementDate = CASE WHEN @Status = N'Reinstated' THEN COALESCE(ReinstatementDate, SYSUTCDATETIME()) ELSE ReinstatementDate END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CancellationId = @CancellationId AND IsDeleted = 0;

INSERT INTO Policy.PolicyCancellationActivity
(ActivityId, CancellationId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(NEWID(), @CancellationId, @TenantId, N'Status', CONCAT(N'Status changed to ', @Status), @Notes, @CreatedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            CancellationId = cancellationId,
            request.Status,
            request.Notes,
            request.CreatedByName,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddActivityAsync(AddPolicyCancellationActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Policy.PolicyCancellation WHERE CancellationId = @CancellationId AND IsDeleted = 0);
INSERT INTO Policy.PolicyCancellationActivity
(ActivityId, CancellationId, TenantId, ActivityType, Subject, Notes, CreatedByName, ActivityDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@ActivityId, @CancellationId, @TenantId, @ActivityType, @Subject, @Notes, @CreatedByName, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);
UPDATE Policy.PolicyCancellation
SET ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @CreatedByUserId
WHERE CancellationId = @CancellationId AND IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ActivityId = id,
            request.CancellationId,
            request.ActivityType,
            request.Subject,
            request.Notes,
            request.CreatedByName,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ArchiveAsync(Guid cancellationId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Policy.PolicyCancellation
SET IsArchived = 1,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE CancellationId = @CancellationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CancellationId = cancellationId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }
}
