using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.RenewalRetention;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RenewalRetentionRepository : IRenewalRetentionRepository
{
    private const string CaseColumns = @"RetentionCaseId, TenantId, PolicyId, AccountId, AccountName, PolicyNumber, LineOfBusiness, Carrier, Producer, Csr,
        ExpirationDate, CurrentPremium, ProposedPremium, RetentionProbability, RiskScore, Stage, Priority, OutreachStatus, Sentiment,
        RiskDrivers, NextBestAction, NextActionDueDate, LastTouchDateUtc, AssignedToUserId, AssignedToName, IsEscalated, IsAtRisk, IsSaved";

    private readonly ISqlConnectionFactory _connectionFactory;

    public RenewalRetentionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RenewalRetentionCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT RetentionCaseId, TenantId, PolicyId, AccountId, AccountName, PolicyNumber, LineOfBusiness, Carrier, Producer, Csr,
       ExpirationDate, CurrentPremium, ProposedPremium, RetentionProbability, RiskScore, Stage, Priority, OutreachStatus, Sentiment,
       RiskDrivers, NextBestAction, NextActionDueDate, LastTouchDateUtc, AssignedToUserId, AssignedToName, IsEscalated, IsAtRisk, IsSaved
FROM Renewal.RetentionCase
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY IsEscalated DESC, IsAtRisk DESC, ExpirationDate, RiskScore DESC;

SELECT RetentionActivityId, TenantId, RetentionCaseId, ActivityType, Subject, Outcome, Notes, ActivityDateUtc, CreatedByName
FROM Renewal.RetentionActivity
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;

SELECT RetentionOfferId, TenantId, RetentionCaseId, OfferName, OfferType, PremiumImpact, RetentionLift, Status, PresentedDateUtc, AcceptedDateUtc, Notes
FROM Renewal.RetentionOffer
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new RenewalRetentionCenterDto
        {
            Cases = (await multi.ReadAsync<RenewalRetentionCaseDto>()).AsList(),
            Activities = (await multi.ReadAsync<RenewalRetentionActivityDto>()).AsList(),
            Offers = (await multi.ReadAsync<RenewalRetentionOfferDto>()).AsList()
        };
    }

    public async Task<RenewalRetentionDetailDto?> GetDetailAsync(Guid retentionCaseId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {CaseColumns}
FROM Renewal.RetentionCase
WHERE RetentionCaseId = @RetentionCaseId AND IsDeleted = 0;

SELECT RetentionActivityId, TenantId, RetentionCaseId, ActivityType, Subject, Outcome, Notes, ActivityDateUtc, CreatedByName
FROM Renewal.RetentionActivity
WHERE RetentionCaseId = @RetentionCaseId AND IsDeleted = 0
ORDER BY ActivityDateUtc DESC;

SELECT RetentionOfferId, TenantId, RetentionCaseId, OfferName, OfferType, PremiumImpact, RetentionLift, Status, PresentedDateUtc, AcceptedDateUtc, Notes
FROM Renewal.RetentionOffer
WHERE RetentionCaseId = @RetentionCaseId AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { RetentionCaseId = retentionCaseId }, cancellationToken: cancellationToken));
        var item = await multi.ReadSingleOrDefaultAsync<RenewalRetentionCaseDto>();
        if (item is null) return null;

        return new RenewalRetentionDetailDto
        {
            Case = item,
            Activities = (await multi.ReadAsync<RenewalRetentionActivityDto>()).AsList(),
            Offers = (await multi.ReadAsync<RenewalRetentionOfferDto>()).AsList()
        };
    }

    public async Task<Guid> CreateCaseAsync(CreateRenewalRetentionCaseRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Renewal.RetentionCase
(RetentionCaseId, TenantId, PolicyId, AccountId, AccountName, PolicyNumber, LineOfBusiness, Carrier, Producer, Csr, ExpirationDate,
 CurrentPremium, ProposedPremium, RetentionProbability, RiskScore, Stage, Priority, OutreachStatus, Sentiment, RiskDrivers,
 NextBestAction, NextActionDueDate, AssignedToUserId, AssignedToName, IsEscalated, IsAtRisk, IsSaved, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@RetentionCaseId, @TenantId, @PolicyId, @AccountId, @AccountName, @PolicyNumber, @LineOfBusiness, @Carrier, @Producer, @Csr, @ExpirationDate,
 @CurrentPremium, @ProposedPremium, @RetentionProbability, @RiskScore, @Stage, @Priority, @OutreachStatus, @Sentiment, @RiskDrivers,
 @NextBestAction, @NextActionDueDate, @AssignedToUserId, @AssignedToName, @IsEscalated, @IsAtRisk, 0, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RetentionCaseId = id,
            request.TenantId,
            request.PolicyId,
            request.AccountId,
            request.AccountName,
            request.PolicyNumber,
            request.LineOfBusiness,
            request.Carrier,
            request.Producer,
            request.Csr,
            request.ExpirationDate,
            request.CurrentPremium,
            request.ProposedPremium,
            request.RetentionProbability,
            request.RiskScore,
            request.Stage,
            request.Priority,
            request.OutreachStatus,
            request.Sentiment,
            request.RiskDrivers,
            request.NextBestAction,
            request.NextActionDueDate,
            request.AssignedToUserId,
            request.AssignedToName,
            request.IsEscalated,
            request.IsAtRisk,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateStageAsync(Guid retentionCaseId, UpdateRenewalRetentionStageRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Renewal.RetentionCase
SET Stage = @Stage,
    OutreachStatus = @OutreachStatus,
    Sentiment = @Sentiment,
    NextBestAction = @NextBestAction,
    NextActionDueDate = @NextActionDueDate,
    IsEscalated = @IsEscalated,
    IsAtRisk = @IsAtRisk,
    IsSaved = @IsSaved,
    LastTouchDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE RetentionCaseId = @RetentionCaseId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RetentionCaseId = retentionCaseId,
            request.Stage,
            request.OutreachStatus,
            request.Sentiment,
            request.NextBestAction,
            request.NextActionDueDate,
            request.IsEscalated,
            request.IsAtRisk,
            request.IsSaved,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddActivityAsync(CreateRenewalRetentionActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Renewal.RetentionCase WHERE RetentionCaseId = @RetentionCaseId AND IsDeleted = 0);
INSERT INTO Renewal.RetentionActivity
(RetentionActivityId, TenantId, RetentionCaseId, ActivityType, Subject, Outcome, Notes, ActivityDateUtc, CreatedByName, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@RetentionActivityId, @TenantId, @RetentionCaseId, @ActivityType, @Subject, @Outcome, @Notes, @ActivityDateUtc, @CreatedByName, SYSUTCDATETIME(), @CreatedByUserId, 0);
UPDATE Renewal.RetentionCase
SET LastTouchDateUtc = @ActivityDateUtc,
    OutreachStatus = CASE WHEN OutreachStatus = N'Not Started' THEN N'Client Contacted' ELSE OutreachStatus END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @CreatedByUserId
WHERE RetentionCaseId = @RetentionCaseId AND IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RetentionActivityId = id,
            request.RetentionCaseId,
            request.ActivityType,
            request.Subject,
            request.Outcome,
            request.Notes,
            request.ActivityDateUtc,
            request.CreatedByName,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> AddOfferAsync(CreateRenewalRetentionOfferRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Renewal.RetentionCase WHERE RetentionCaseId = @RetentionCaseId AND IsDeleted = 0);
INSERT INTO Renewal.RetentionOffer
(RetentionOfferId, TenantId, RetentionCaseId, OfferName, OfferType, PremiumImpact, RetentionLift, Status, PresentedDateUtc, AcceptedDateUtc, Notes, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
(@RetentionOfferId, @TenantId, @RetentionCaseId, @OfferName, @OfferType, @PremiumImpact, @RetentionLift, @Status,
 CASE WHEN @Status IN (N'Presented', N'Accepted') THEN SYSUTCDATETIME() ELSE NULL END,
 CASE WHEN @Status = N'Accepted' THEN SYSUTCDATETIME() ELSE NULL END,
 @Notes, SYSUTCDATETIME(), @CreatedByUserId, 0);
UPDATE Renewal.RetentionCase
SET RetentionProbability = CASE WHEN RetentionProbability + @RetentionLift > 100 THEN 100 ELSE RetentionProbability + @RetentionLift END,
    ProposedPremium = COALESCE(ProposedPremium, CurrentPremium) + @PremiumImpact,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @CreatedByUserId
WHERE RetentionCaseId = @RetentionCaseId AND IsDeleted = 0;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            RetentionOfferId = id,
            request.RetentionCaseId,
            request.OfferName,
            request.OfferType,
            request.PremiumImpact,
            request.RetentionLift,
            request.Status,
            request.Notes,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateOfferStatusAsync(Guid retentionOfferId, UpdateRenewalRetentionOfferStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Renewal.RetentionOffer
SET Status = @Status,
    PresentedDateUtc = CASE WHEN @Status IN (N'Presented', N'Accepted') AND PresentedDateUtc IS NULL THEN SYSUTCDATETIME() ELSE PresentedDateUtc END,
    AcceptedDateUtc = CASE WHEN @Status = N'Accepted' THEN SYSUTCDATETIME() ELSE AcceptedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE RetentionOfferId = @RetentionOfferId AND IsDeleted = 0;

UPDATE rc
SET IsSaved = CASE WHEN @Status = N'Accepted' THEN 1 ELSE rc.IsSaved END,
    Stage = CASE WHEN @Status = N'Accepted' THEN N'Saved' ELSE rc.Stage END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
FROM Renewal.RetentionCase rc
INNER JOIN Renewal.RetentionOffer ro ON ro.RetentionCaseId = rc.RetentionCaseId
WHERE ro.RetentionOfferId = @RetentionOfferId AND rc.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { RetentionOfferId = retentionOfferId, request.Status, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
