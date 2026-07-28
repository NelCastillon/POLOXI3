using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.RenewalRetention;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RenewalRetentionRepository : IRenewalRetentionRepository
{
    private const string CaseColumns = @"RetentionCaseId, TenantId, PolicyId, AccountId, SourcePolicyTermId, RenewalOpportunityId, RenewalSubmissionId, RenewalPolicyBindTransactionId, ResultPolicyId, ResultPolicyTermId, InitiationSourceCode, InitiatedDateUtc, CompletedDateUtc, AccountName, PolicyNumber, LineOfBusiness, Carrier, Producer, Csr,
        ExpirationDate, CurrentPremium, ProposedPremium, RetentionProbability, RiskScore, Stage, Priority, OutreachStatus, Sentiment,
        RiskDrivers, NextBestAction, NextActionDueDate, LastTouchDateUtc, AssignedToUserId, AssignedToName, IsEscalated, IsAtRisk, IsSaved";

    private readonly ISqlConnectionFactory _connectionFactory;

    public RenewalRetentionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<RenewalRetentionCenterDto> GetCenterAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT RetentionCaseId, TenantId, PolicyId, AccountId, SourcePolicyTermId, RenewalOpportunityId, RenewalSubmissionId, RenewalPolicyBindTransactionId, ResultPolicyId, ResultPolicyTermId, InitiationSourceCode, InitiatedDateUtc, CompletedDateUtc, AccountName, PolicyNumber, LineOfBusiness, Carrier, Producer, Csr,
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
ORDER BY CreatedDateUtc DESC;

SELECT WorkflowOptionId, OptionGroupCode, OptionCode, DisplayName, Description, IsDefault, SortOrder
FROM Renewal.WorkflowOption
WHERE TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0
ORDER BY OptionGroupCode, SortOrder, DisplayName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new RenewalRetentionCenterDto
        {
            Cases = (await multi.ReadAsync<RenewalRetentionCaseDto>()).AsList(),
            Activities = (await multi.ReadAsync<RenewalRetentionActivityDto>()).AsList(),
            Offers = (await multi.ReadAsync<RenewalRetentionOfferDto>()).AsList(),
            Options = (await multi.ReadAsync<RenewalWorkflowOptionDto>()).AsList()
        };
    }

    public async Task<RenewalInitiationResultDto> InitiateEligibleAsync(InitiateEligibleRenewalsRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Eligible INT = 0;
DECLARE @Created INT = 0;
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @Candidates TABLE
(
    TenantId UNIQUEIDENTIFIER, PolicyId UNIQUEIDENTIFIER, PolicyTermId UNIQUEIDENTIFIER, AccountId UNIQUEIDENTIFIER,
    AccountName NVARCHAR(200), PolicyNumber NVARCHAR(80), LineOfBusiness NVARCHAR(100), CarrierName NVARCHAR(200),
    ExpirationDate DATE, CurrentPremium DECIMAL(18,2), AssignedToUserId UNIQUEIDENTIFIER, AssignedToName NVARCHAR(160),
    StageCode NVARCHAR(40), PriorityCode NVARCHAR(20)
);

INSERT INTO @Candidates
SELECT TOP (COALESCE(@MaxCases, 500)) bp.TenantId, bp.PolicyId, pt.PolicyTermId, bp.AccountId,
       a.AccountName, bp.PolicyNumber, COALESCE(pl.LineOfBusinessName, pl.LineOfBusinessCode, s.LineOfBusiness, N'Package'),
       COALESCE(c.CarrierName, N'Carrier'), pt.ExpirationDate, COALESCE(pt.AnnualizedPremium, pt.WrittenPremium, bp.AnnualPremium, 0),
       pa.ProducerId, COALESCE(pa.ProducerName, u.DisplayName, N'Unassigned'), setting.DefaultStageCode, setting.DefaultPriorityCode
FROM Policy.PolicyTerm pt
INNER JOIN Submissions.BoundPolicy bp ON bp.PolicyId = pt.PolicyId AND bp.TenantId = pt.TenantId AND bp.IsDeleted = 0
INNER JOIN Renewal.AutomationSetting setting ON setting.TenantId = pt.TenantId AND setting.IsEnabled = 1 AND setting.IsDeleted = 0
INNER JOIN Client.Account a ON a.AccountId = bp.AccountId AND a.TenantId = bp.TenantId AND a.IsDeleted = 0
LEFT JOIN Submissions.Submission s ON s.SubmissionId = bp.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Core.Carrier c ON c.CarrierId = bp.CarrierId AND c.IsDeleted = 0
OUTER APPLY (SELECT TOP 1 LineOfBusinessCode, LineOfBusinessName FROM Policy.PolicyLine WHERE PolicyId=bp.PolicyId AND IsDeleted=0 ORDER BY SortOrder) pl
OUTER APPLY (SELECT TOP 1 ProducerId, ProducerName FROM Policy.PolicyAssignment WHERE PolicyId=bp.PolicyId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC) pa
LEFT JOIN IAM.[User] u ON u.UserId = pa.ProducerId AND u.IsDeleted = 0
WHERE pt.IsDeleted = 0
  AND (@TenantId IS NULL OR pt.TenantId = @TenantId)
  AND pt.ExpirationDate >= CONVERT(date, @Now)
  AND pt.ExpirationDate <= DATEADD(day, setting.InitiationLeadDays, CONVERT(date, @Now))
  AND NOT EXISTS (SELECT 1 FROM Policy.PolicyTerm newer WHERE newer.PolicyId=pt.PolicyId AND newer.TermNumber>pt.TermNumber AND newer.IsDeleted=0)
  AND NOT EXISTS (SELECT 1 FROM Renewal.RetentionCase existing WHERE existing.TenantId=pt.TenantId AND existing.SourcePolicyTermId=pt.PolicyTermId AND existing.IsDeleted=0)
ORDER BY pt.ExpirationDate;

SELECT @Eligible = COUNT(1) FROM @Candidates;

INSERT INTO Renewal.RetentionCase
    (RetentionCaseId, TenantId, PolicyId, SourcePolicyTermId, AccountId, AccountName, PolicyNumber, LineOfBusiness, Carrier, Producer, Csr,
     ExpirationDate, CurrentPremium, RetentionProbability, RiskScore, Stage, Priority, OutreachStatus, Sentiment, NextBestAction,
     NextActionDueDate, AssignedToUserId, AssignedToName, IsEscalated, IsAtRisk, IsSaved, InitiationSourceCode, InitiatedDateUtc,
     CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), c.TenantId, c.PolicyId, c.PolicyTermId, c.AccountId, c.AccountName, c.PolicyNumber, c.LineOfBusiness, c.CarrierName,
       c.AssignedToName, N'Unassigned', c.ExpirationDate, c.CurrentPremium, 75, 25, c.StageCode, c.PriorityCode, N'NotStarted', N'Neutral',
       N'Review renewal exposure and confirm incumbent or remarketing strategy.', DATEADD(day, 7, CONVERT(date, @Now)), c.AssignedToUserId, c.AssignedToName,
       0, 0, 0, @InitiationSourceCode, @Now, @Now, @InitiatedByUserId, 0
FROM @Candidates c
WHERE NOT EXISTS (SELECT 1 FROM Renewal.RetentionCase existing WITH (UPDLOCK, HOLDLOCK) WHERE existing.TenantId=c.TenantId AND existing.SourcePolicyTermId=c.PolicyTermId AND existing.IsDeleted=0);

SET @Created = @@ROWCOUNT;
COMMIT TRANSACTION;
SELECT @Eligible AS EligiblePolicyTerms, @Created AS CreatedCases;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleAsync<RenewalInitiationResultDto>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task LaunchPlacementAsync(Guid retentionCaseId, LaunchRenewalPlacementRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @OpportunityId UNIQUEIDENTIFIER;
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @AccountId UNIQUEIDENTIFIER;
DECLARE @AccountName NVARCHAR(200);
DECLARE @LineOfBusiness NVARCHAR(100);
DECLARE @CurrentPremium DECIMAL(18,2);
DECLARE @ExpirationDate DATE;
DECLARE @AssignedToUserId UNIQUEIDENTIFIER;
DECLARE @StageId UNIQUEIDENTIFIER;
DECLARE @StageName NVARCHAR(50);
DECLARE @OpportunityNumber NVARCHAR(50);
DECLARE @SubmissionNumber NVARCHAR(50);

SELECT @OpportunityId = RenewalOpportunityId, @SubmissionId = RenewalSubmissionId, @AccountId = AccountId,
       @AccountName = AccountName, @LineOfBusiness = LineOfBusiness, @CurrentPremium = CurrentPremium,
       @ExpirationDate = ExpirationDate, @AssignedToUserId = AssignedToUserId
FROM Renewal.RetentionCase WITH (UPDLOCK, HOLDLOCK)
WHERE RetentionCaseId = @RetentionCaseId AND TenantId = @TenantId AND IsDeleted = 0;

IF @AccountId IS NULL THROW 53001, 'Renewal retention case was not found or has no account.', 1;

IF @OpportunityId IS NULL
BEGIN
    SELECT TOP 1 @StageId=OpportunityStageId, @StageName=StageName
    FROM CRM.OpportunityStage
    WHERE TenantId=@TenantId AND IsActive=1
    ORDER BY CASE WHEN StageCode IN (N'QUALIFICATION',N'DISCOVERY') OR StageName IN (N'Qualification',N'Discovery') THEN 0 ELSE 1 END, SortOrder;
    IF @StageId IS NULL THROW 53002, 'No active opportunity stage is configured for this tenant.', 1;

    DECLARE @NextOpportunityNumber INT = ISNULL((SELECT COUNT(1) FROM CRM.Opportunity WITH (UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IsDeleted=0),0)+1;
    SET @OpportunityNumber=CONCAT(N'OPP-',FORMAT(SYSUTCDATETIME(),N'yyyyMMdd'),N'-',FORMAT(@NextOpportunityNumber,N'00000'));
    WHILE EXISTS(SELECT 1 FROM CRM.Opportunity WHERE TenantId=@TenantId AND OpportunityNumber=@OpportunityNumber AND IsDeleted=0)
    BEGIN SET @NextOpportunityNumber+=1; SET @OpportunityNumber=CONCAT(N'OPP-',FORMAT(SYSUTCDATETIME(),N'yyyyMMdd'),N'-',FORMAT(@NextOpportunityNumber,N'00000')); END;

    SET @OpportunityId=NEWID();
    INSERT INTO CRM.Opportunity
        (OpportunityId,TenantId,OpportunityNumber,AccountId,OpportunityName,EstimatedAmount,OwnerUserId,CloseDate,WinProbability,ForecastCategoryCode,StageName,OpportunityStageId,StatusCodeId,Description,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES
        (@OpportunityId,@TenantId,@OpportunityNumber,@AccountId,CONCAT(@AccountName,N' - ',@LineOfBusiness,N' Renewal'),@CurrentPremium,@AssignedToUserId,@ExpirationDate,75,N'Pipeline',@StageName,@StageId,1,N'Automatically created from an expiring policy term.',SYSUTCDATETIME(),@LaunchedByUserId,0);

    INSERT INTO CRM.OpportunityLine
        (OpportunityLineId,TenantId,OpportunityId,LineOfBusiness,EstPremium,Priority,Status,IsPrimary,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES
        (NEWID(),@TenantId,@OpportunityId,@LineOfBusiness,@CurrentPremium,N'Medium',N'Draft',1,SYSUTCDATETIME(),@LaunchedByUserId,0);

    INSERT INTO CRM.OpportunityWorkflowEvent
        (WorkflowEventId,TenantId,OpportunityId,EventType,EventTitle,EventDetail,RelatedEntityName,RelatedEntityId,EventDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES
        (NEWID(),@TenantId,@OpportunityId,N'Renewal',N'Renewal opportunity created',N'Opportunity created from an expiring policy term.',N'RenewalRetentionCase',@RetentionCaseId,SYSUTCDATETIME(),SYSUTCDATETIME(),@LaunchedByUserId,0);
END;

IF @SubmissionId IS NULL
BEGIN
    SET @SubmissionId=NEWID();
    SET @SubmissionNumber=N'SUB-'+CONVERT(NVARCHAR(8),SYSUTCDATETIME(),112)+N'-'+RIGHT(N'0000'+CAST(NEXT VALUE FOR Submissions.SubmissionSeq AS NVARCHAR(20)),4);
    INSERT INTO Submissions.Submission
        (SubmissionId,TenantId,AccountId,OpportunityId,SubmissionNumber,LineOfBusiness,Status,Priority,AssignedToUserId,EffectiveDate,ExpirationDate,TargetPremium,MarketCount,QuoteCount,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES
        (@SubmissionId,@TenantId,@AccountId,@OpportunityId,@SubmissionNumber,@LineOfBusiness,N'Draft',N'Normal',@AssignedToUserId,@ExpirationDate,DATEADD(year,1,@ExpirationDate),@CurrentPremium,0,0,SYSUTCDATETIME(),@LaunchedByUserId,0);

    INSERT INTO CRM.OpportunitySubmission
        (SubmissionId,TenantId,OpportunityId,SubmissionNumber,LineOfBusiness,Status,TargetPremium,Priority,AssignedToUserId,EffectiveDate,ExpirationDate,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT @SubmissionId,@TenantId,@OpportunityId,@SubmissionNumber,@LineOfBusiness,N'Draft',@CurrentPremium,N'Normal',@AssignedToUserId,@ExpirationDate,DATEADD(year,1,@ExpirationDate),SYSUTCDATETIME(),@LaunchedByUserId,0
    WHERE NOT EXISTS(SELECT 1 FROM CRM.OpportunitySubmission WHERE SubmissionId=@SubmissionId AND IsDeleted=0);
END;

UPDATE Renewal.RetentionCase
SET RenewalOpportunityId=@OpportunityId, RenewalSubmissionId=@SubmissionId, Stage=N'Remarket', NextBestAction=N'Complete underwriting updates and request renewal terms.',
    ModifiedDateUtc=SYSUTCDATETIME(), ModifiedByUserId=@LaunchedByUserId
WHERE RetentionCaseId=@RetentionCaseId AND TenantId=@TenantId AND IsDeleted=0;

INSERT INTO Renewal.RetentionActivity
    (RetentionActivityId,TenantId,RetentionCaseId,ActivityType,Subject,Outcome,Notes,ActivityDateUtc,CreatedByName,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@RetentionCaseId,N'Remarket',N'Renewal placement launched',N'Completed',CONCAT(N'Opportunity ',@OpportunityNumber,N' and submission ',@SubmissionNumber,N' are ready.'),SYSUTCDATETIME(),N'System',SYSUTCDATETIME(),@LaunchedByUserId,0
WHERE NOT EXISTS(SELECT 1 FROM Renewal.RetentionActivity WHERE RetentionCaseId=@RetentionCaseId AND Subject=N'Renewal placement launched' AND IsDeleted=0);

COMMIT TRANSACTION;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { RetentionCaseId = retentionCaseId, request.TenantId, request.LaunchedByUserId }, cancellationToken: cancellationToken));
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
