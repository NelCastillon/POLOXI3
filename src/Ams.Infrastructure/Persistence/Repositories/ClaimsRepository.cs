using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Claims;
using Dapper;
using System.Data;
using System.Globalization;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ClaimsRepository : IClaimsRepository
{
    private const string ClaimColumns = @"ClaimId, TenantId, PolicyId, AccountId, ClaimNumber, PolicyNumber, AccountName, Lob, Carrier, Status,
        LossType, PrimaryClaimant, DateOfLoss, DateReported, ClosedDate,
        CASE WHEN Status = 'Closed' THEN 0 ELSE DATEDIFF(day, DateOfLoss, CAST(SYSUTCDATETIME() AS date)) END AS DaysOpen,
        TotalIncurred, TotalReserves, TotalPaid, AssignedHandler, IsLitigation, HasSubrogation, IsCatastrophe, IsDisputed,
        FollowUpReason, Priority, FollowUpDueDate, IsSnoozed, CatCode, LossLocation, StateOfLoss, LossDescription, CauseOfLoss,
        CarrierClaimNumber, ReportedBy, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted";

    private readonly ISqlConnectionFactory _connectionFactory;
    public ClaimsRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<ClaimDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lob, string? catCode, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var sql = $@"
;WITH Cte AS (
    SELECT {ClaimColumns}
    FROM Claims.Claim
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (@Status IS NULL OR @Status = '' OR Status = @Status)
      AND (@Lob IS NULL OR @Lob = '' OR Lob = @Lob)
      AND (@CatCode IS NULL OR @CatCode = '' OR CatCode = @CatCode)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR ClaimNumber LIKE '%' + @SearchTerm + '%' OR PolicyNumber LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%' OR PrimaryClaimant LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY DateReported DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Claims.Claim
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@Status IS NULL OR @Status = '' OR Status = @Status)
  AND (@Lob IS NULL OR @Lob = '' OR Lob = @Lob)
  AND (@CatCode IS NULL OR @CatCode = '' OR CatCode = @CatCode)
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR ClaimNumber LIKE '%' + @SearchTerm + '%' OR PolicyNumber LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%' OR PrimaryClaimant LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Status = status,
            Lob = lob,
            CatCode = catCode,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));
        return new PagedResult<ClaimDto>
        {
            Items = (await multi.ReadAsync<ClaimDto>()).AsList(),
            TotalCount = await multi.ReadSingleAsync<int>(),
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<ClaimDetailDto?> GetDetailAsync(Guid tenantId, Guid claimId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {ClaimColumns} FROM Claims.Claim WHERE TenantId=@TenantId AND ClaimId = @ClaimId AND IsDeleted = 0;
SELECT ClaimActivityId, ClaimId, ActivityType, Title, Category, Party, Notes, Amount, PriorAmount, ActivityDate, CreatedBy, IsPinned
FROM Claims.ClaimActivity WHERE TenantId=@TenantId AND ClaimId = @ClaimId AND IsDeleted = 0 ORDER BY ActivityDate DESC;
SELECT ClaimOptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,IsDefault,IsActive,SortOrder FROM Claims.ClaimOption WHERE TenantId=@TenantId AND IsActive=1 AND IsDeleted=0 ORDER BY OptionGroupCode,SortOrder;
SELECT ClaimAdjusterId,TenantId,ClaimId,AdjusterTypeCode,AdjusterName,CompanyName,EmailAddress,PhoneNumber,LicenseNumber,IsPrimary,AssignmentStatusCode,AssignedDateUtc,ReleasedDateUtc FROM Claims.ClaimAdjuster WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0 ORDER BY IsPrimary DESC,AssignedDateUtc DESC;
SELECT ClaimPartyId,TenantId,ClaimId,ContactId,PartyTypeCode,DisplayName,OrganizationName,EmailAddress,PhoneNumber,AddressJson,PreferredContactMethodCode,IsPrimary,IsActive FROM Claims.ClaimParty WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0 ORDER BY IsPrimary DESC,PartyTypeCode,DisplayName;
SELECT ClaimFinancialTransactionId,TenantId,ClaimId,TransactionTypeCode,TransactionDate,Amount,CurrencyCode,CoverageCode,PayeeClaimPartyId,ReferenceNumber,StatusCode,ReversalOfTransactionId,Description FROM Claims.ClaimFinancialTransaction WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0 ORDER BY TransactionDate DESC,CreatedDateUtc DESC;
SELECT ClaimNoteId,TenantId,ClaimId,NoteTypeCode,Subject,NoteText,IsPinned,IsConfidential,NoteDateUtc,CreatedByName FROM Claims.ClaimNote WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0 ORDER BY IsPinned DESC,NoteDateUtc DESC;
SELECT ClaimTaskId,TenantId,ClaimId,OpsTaskItemId,TaskTypeCode,Title,Description,PriorityCode,StatusCode,AssignedToUserId,AssignedToName,DueDate,CompletedDateUtc FROM Claims.ClaimTask WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0 ORDER BY CASE StatusCode WHEN N'Open' THEN 0 WHEN N'InProgress' THEN 1 ELSE 2 END,DueDate;
SELECT l.ClaimDocumentLinkId,l.TenantId,l.ClaimId,l.DocumentId,l.DocumentRoleCode,d.FileName,d.ContentType,d.FileSizeBytes,d.StatusCode,COALESCE(l.Description,d.Description) Description,l.LinkedDateUtc FROM Claims.ClaimDocumentLink l JOIN DMS.Document d ON d.TenantId=l.TenantId AND d.DocumentId=l.DocumentId AND d.IsDeleted=0 WHERE l.TenantId=@TenantId AND l.ClaimId=@ClaimId AND l.IsDeleted=0 ORDER BY l.LinkedDateUtc DESC;
SELECT ClaimStatusHistoryId,TenantId,ClaimId,OldStatusCode,NewStatusCode,ReasonCode,Notes,ChangedDateUtc,ChangedByUserId FROM Claims.ClaimStatusHistory WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0 ORDER BY ChangedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ClaimId = claimId }, cancellationToken: cancellationToken));
        var claim = await multi.ReadSingleOrDefaultAsync<ClaimDto>();
        if (claim is null) return null;
        return new ClaimDetailDto
        {
            Claim = claim,
            Activities = (await multi.ReadAsync<ClaimActivityDto>()).AsList(),
            Options = (await multi.ReadAsync<ClaimOptionDto>()).AsList(),
            Adjusters = (await multi.ReadAsync<ClaimAdjusterDto>()).AsList(),
            Parties = (await multi.ReadAsync<ClaimPartyDto>()).AsList(),
            FinancialTransactions = (await multi.ReadAsync<ClaimFinancialTransactionDto>()).AsList(),
            Notes = (await multi.ReadAsync<ClaimNoteDto>()).AsList(),
            Tasks = (await multi.ReadAsync<ClaimTaskDto>()).AsList(),
            Documents = (await multi.ReadAsync<ClaimDocumentDto>()).AsList(),
            StatusHistory = (await multi.ReadAsync<ClaimStatusHistoryDto>()).AsList()
        };
    }

    public async Task<IReadOnlyList<ClaimOptionDto>> GetOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT ClaimOptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,IsDefault,IsActive,SortOrder FROM Claims.ClaimOption WHERE TenantId=@TenantId AND IsActive=1 AND IsDeleted=0 ORDER BY OptionGroupCode,SortOrder,DisplayName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<ClaimOptionDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken = default)
    {
        ClaimRules.ValidateLossDates(DateOnly.FromDateTime(request.DateOfLoss), DateOnly.FromDateTime(request.DateReported));
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @PolicyId uniqueidentifier,@AccountId uniqueidentifier,@CarrierId uniqueidentifier,@ResolvedPolicyNumber nvarchar(50),@ResolvedAccountName nvarchar(160),@ResolvedLob nvarchar(80),@ResolvedCarrier nvarchar(120);
SELECT @PolicyId=bp.PolicyId,@AccountId=bp.AccountId,@CarrierId=bp.CarrierId,@ResolvedPolicyNumber=bp.PolicyNumber,@ResolvedAccountName=a.AccountName,@ResolvedLob=bp.LineOfBusiness,@ResolvedCarrier=cr.CarrierName
FROM Submissions.BoundPolicy bp JOIN Client.Account a ON a.TenantId=bp.TenantId AND a.AccountId=bp.AccountId AND a.IsDeleted=0 LEFT JOIN Agency.Carrier cr ON cr.TenantId=bp.TenantId AND cr.CarrierId=bp.CarrierId AND cr.IsDeleted=0
WHERE bp.TenantId=@TenantId AND bp.IsDeleted=0 AND ((@PolicyIdInput IS NOT NULL AND bp.PolicyId=@PolicyIdInput) OR (@PolicyIdInput IS NULL AND bp.PolicyNumber=@PolicyNumber));
IF @PolicyId IS NULL THROW 51000,N'An authoritative policy and account match is required for claim intake.',1;
IF @AccountIdInput IS NOT NULL AND @AccountIdInput<>@AccountId THROW 51000,N'Account does not belong to the selected policy.',1;
IF @CarrierIdInput IS NOT NULL AND @CarrierIdInput<>@CarrierId THROW 51000,N'Carrier does not match the selected policy.',1;
DECLARE @Next int = ISNULL((SELECT MAX(TRY_CONVERT(int, RIGHT(ClaimNumber, 5))) FROM Claims.Claim WHERE TenantId = @TenantId AND ClaimNumber LIKE CONCAT('CLM-', YEAR(SYSUTCDATETIME()), '-%')), 0) + 1;
DECLARE @ClaimNumber nvarchar(50) = CONCAT('CLM-', YEAR(SYSUTCDATETIME()), '-', FORMAT(@Next, '00000'));
INSERT INTO Claims.Claim
(ClaimId, TenantId, PolicyId, AccountId, CarrierId, ClaimNumber, PolicyNumber, AccountName, Lob, Carrier, Status, LossType, PrimaryClaimant,
 DateOfLoss, DateReported, ClosedDate, TotalIncurred, TotalReserves, TotalPaid, AssignedHandler, IsLitigation, HasSubrogation,
 IsCatastrophe, IsDisputed, FollowUpReason, Priority, FollowUpDueDate, IsSnoozed, CatCode, LossLocation, StateOfLoss,
 LossDescription, CauseOfLoss, CarrierClaimNumber, ReportedBy, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
(@ClaimId, @TenantId, @PolicyId, @AccountId, @CarrierId, @ClaimNumber, @ResolvedPolicyNumber, @ResolvedAccountName, COALESCE(NULLIF(@ResolvedLob,N''),@Lob), COALESCE(NULLIF(@ResolvedCarrier,N''),@Carrier), @Status, @LossType, @PrimaryClaimant,
 @DateOfLoss, @DateReported, NULL, @TotalIncurred, @TotalReserves, @TotalPaid, @AssignedHandler, @IsLitigation, @HasSubrogation,
 @IsCatastrophe, @IsDisputed, 'Initial follow-up', @Priority, DATEADD(day, 7, @DateReported), 0, @CatCode, @LossLocation, @StateOfLoss,
 @LossDescription, @CauseOfLoss, @CarrierClaimNumber, @ReportedBy, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);
UPDATE Claims.Claim SET PolicyLinkStatusCode=N'Linked',AccountLinkStatusCode=N'Linked' WHERE ClaimId=@ClaimId;
INSERT Claims.ClaimParty(ClaimPartyId,TenantId,ClaimId,PartyTypeCode,DisplayName,EmailAddress,PhoneNumber,IsPrimary,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@ClaimId,N'Claimant',@PrimaryClaimant,@ClaimantEmail,@ClaimantPhone,1,1,SYSUTCDATETIME(),@CreatedByUserId,0);
INSERT Claims.ClaimStatusHistory(ClaimStatusHistoryId,TenantId,ClaimId,NewStatusCode,ReasonCode,Notes,ChangedDateUtc,ChangedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@ClaimId,@Status,N'FNOL',N'Initial claim status recorded at intake.',SYSUTCDATETIME(),@CreatedByUserId,0);
INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'Claim',@ClaimId,N'Created',N'Claim intake created from authoritative policy and account.',JSON_OBJECT(N'ClaimNumber':@ClaimNumber,N'PolicyId':@PolicyId,N'AccountId':@AccountId),@CreatedByUserId,SYSUTCDATETIME()); COMMIT;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ClaimId = id, request.TenantId, PolicyIdInput=request.PolicyId, AccountIdInput=request.AccountId, CarrierIdInput=request.CarrierId, request.PolicyNumber, request.AccountName, request.Lob, request.Carrier, request.Status, request.LossType, request.PrimaryClaimant, request.DateOfLoss, request.DateReported, TotalIncurred=0m, TotalReserves=0m, TotalPaid=0m, request.AssignedHandler, request.IsLitigation, request.HasSubrogation, request.IsCatastrophe, request.IsDisputed, request.Priority, request.CatCode, request.LossLocation, request.StateOfLoss, request.LossDescription, request.CauseOfLoss, request.CarrierClaimNumber, request.ReportedBy, request.ClaimantEmail, request.ClaimantPhone, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateStatusAsync(Guid claimId, UpdateClaimStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"SET XACT_ABORT ON; BEGIN TRAN; DECLARE @Old nvarchar(50); SELECT @Old=Status FROM Claims.Claim WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0; IF @Old IS NULL THROW 51000,N'Claim not found.',1; IF NOT EXISTS(SELECT 1 FROM Claims.ClaimOption WHERE TenantId=@TenantId AND OptionGroupCode=N'ClaimStatus' AND (OptionCode=@Status OR DisplayName=@Status) AND IsActive=1 AND IsDeleted=0) THROW 51000,N'Claim status is not configured.',1; UPDATE Claims.Claim SET Status = @Status, ClosedDate = CASE WHEN @Status = 'Closed' THEN CAST(SYSUTCDATETIME() AS date) WHEN @Status = 'Reopened' THEN NULL ELSE ClosedDate END, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE TenantId=@TenantId AND ClaimId = @ClaimId AND IsDeleted = 0; INSERT Claims.ClaimStatusHistory(ClaimStatusHistoryId,TenantId,ClaimId,OldStatusCode,NewStatusCode,ReasonCode,ChangedDateUtc,ChangedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@ClaimId,@Old,@Status,N'UserUpdate',SYSUTCDATETIME(),@ModifiedByUserId,0); INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,OldValueJson,NewValueJson,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'Claim',@ClaimId,N'StatusChanged',N'Claim status changed.',JSON_OBJECT(N'Status':@Old),JSON_OBJECT(N'Status':@Status),@ModifiedByUserId,SYSUTCDATETIME()); COMMIT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ClaimId = claimId, request.TenantId, request.Status, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateFollowUpAsync(Guid claimId, UpdateClaimFollowUpRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE Claims.Claim SET FollowUpReason = COALESCE(@FollowUpReason, FollowUpReason), Priority = COALESCE(@Priority, Priority), FollowUpDueDate = COALESCE(@FollowUpDueDate, FollowUpDueDate), IsSnoozed = @IsSnoozed, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE TenantId=@TenantId AND ClaimId = @ClaimId AND IsDeleted = 0; IF @@ROWCOUNT<>1 THROW 51000,N'Claim not found.',1;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ClaimId = claimId, request.TenantId, request.FollowUpReason, request.Priority, request.FollowUpDueDate, request.IsSnoozed, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddActivityAsync(CreateClaimActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"INSERT INTO Claims.ClaimActivity (ClaimActivityId, TenantId, ClaimId, ActivityType, ActivityDescription, Title, Category, Party, Notes, Amount, PriorAmount, ActivityDate, CreatedBy, CreatedDateUtc, IsPinned, IsDeleted) SELECT @ClaimActivityId,@TenantId,@ClaimId,@ActivityType,@Notes,@Title,@Category,@Party,@Notes,@Amount,@PriorAmount,@ActivityDate,@CreatedBy,SYSUTCDATETIME(),@IsPinned,0 FROM Claims.Claim WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0; IF @@ROWCOUNT<>1 THROW 51000,N'Claim not found.',1;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ClaimActivityId = id, request.TenantId, request.ClaimId, request.ActivityType, request.Title, request.Category, request.Party, request.Notes, request.Amount, request.PriorAmount, request.ActivityDate, request.CreatedBy, request.IsPinned }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<PagedResult<CatEventDto>> SearchCatEventsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT CatEventId, TenantId, Name, CatCode, EventType, Severity, AffectedStates, StartDate, EndDate, Description FROM Claims.CatEvent WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY StartDate DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<CatEventDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
        return new PagedResult<CatEventDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count };
    }

    public async Task<Guid> CreateCatEventAsync(CreateCatEventRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"INSERT INTO Claims.CatEvent (CatEventId, TenantId, Name, CatCode, EventType, Severity, AffectedStates, StartDate, EndDate, Description, CreatedDateUtc, IsDeleted) VALUES (@CatEventId, @TenantId, @Name, @CatCode, @EventType, @Severity, @AffectedStates, @StartDate, @EndDate, @Description, SYSUTCDATETIME(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { CatEventId = id, request.TenantId, request.Name, request.CatCode, request.EventType, request.Severity, request.AffectedStates, request.StartDate, request.EndDate, request.Description }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<CatastrophePageDto> GetCatastrophePageAsync(Guid tenantId, Guid? catEventId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @EventId uniqueidentifier = COALESCE(@CatEventId, (SELECT TOP 1 CatEventId FROM Claims.CatEvent WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY StartDate DESC));
SELECT CatEventId, TenantId, Name, CatCode, EventType, Severity, AffectedStates, StartDate, EndDate, Description FROM Claims.CatEvent WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY StartDate DESC;
SELECT AffectedInsuredId, CatEventId, AccountId, AccountName, PolicyNumber, Lob, County, ZipCode, TivAtRisk, GeoTagged, FnolFiled, BlastSent, ContactStatus, Handler FROM Claims.CatAffectedInsured WHERE CatEventId = @EventId AND IsDeleted = 0 ORDER BY AccountName;
SELECT ClaimId, TenantId, PolicyId, AccountId, ClaimNumber, PolicyNumber, AccountName, Lob, Carrier, Status, LossType, PrimaryClaimant, DateOfLoss, DateReported, ClosedDate, CASE WHEN Status = 'Closed' THEN 0 ELSE DATEDIFF(day, DateOfLoss, CAST(SYSUTCDATETIME() AS date)) END AS DaysOpen, TotalIncurred, TotalReserves, TotalPaid, AssignedHandler, IsLitigation, HasSubrogation, IsCatastrophe, IsDisputed, FollowUpReason, Priority, FollowUpDueDate, IsSnoozed, CatCode, LossLocation, StateOfLoss, LossDescription, CauseOfLoss, CarrierClaimNumber, ReportedBy, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted FROM Claims.Claim WHERE TenantId = @TenantId AND IsDeleted = 0 AND CatCode = (SELECT CatCode FROM Claims.CatEvent WHERE CatEventId = @EventId);
SELECT ClaimActivityId, ClaimId, ActivityType, Title, Category, Party, Notes, Amount, PriorAmount, ActivityDate, CreatedBy, IsPinned FROM Claims.ClaimActivity WHERE ActivityType = 'Campaign' AND ClaimId = @EventId AND IsDeleted = 0 ORDER BY ActivityDate DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, CatEventId = catEventId }, cancellationToken: cancellationToken));
        return new CatastrophePageDto
        {
            Events = (await multi.ReadAsync<CatEventDto>()).AsList(),
            AffectedInsureds = (await multi.ReadAsync<AffectedInsuredDto>()).AsList(),
            Claims = (await multi.ReadAsync<ClaimDto>()).AsList(),
            Campaigns = (await multi.ReadAsync<ClaimActivityDto>()).AsList()
        };
    }

    public async Task MarkAffectedInsuredContactedAsync(Guid affectedInsuredId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Claims.CatAffectedInsured SET ContactStatus = 'Contacted', ModifiedDateUtc = SYSUTCDATETIME() WHERE AffectedInsuredId = @AffectedInsuredId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { AffectedInsuredId = affectedInsuredId }, cancellationToken: cancellationToken));
    }

    public async Task<int> ApplyGeoTagAsync(Guid catEventId, string? states, string? counties, string? zips, string? lob, decimal? minTiv, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE Claims.CatAffectedInsured SET GeoTagged = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE CatEventId = @CatEventId AND IsDeleted = 0 AND (@Lob IS NULL OR @Lob = '' OR @Lob = 'All' OR Lob = @Lob) AND (@MinTiv IS NULL OR TivAtRisk >= @MinTiv); SELECT @@ROWCOUNT;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { CatEventId = catEventId, Lob = lob, MinTiv = minTiv }, cancellationToken: cancellationToken));
    }

    public async Task<int> SendCatBlastAsync(CatBlastRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TargetCount int = (SELECT COUNT(1) FROM Claims.CatAffectedInsured WHERE CatEventId = @CatEventId AND GeoTagged = 1 AND IsDeleted = 0);
INSERT INTO Claims.ClaimActivity (ClaimActivityId, TenantId, ClaimId, ActivityType, ActivityDescription, Title, Category, Party, Notes, Amount, PriorAmount, ActivityDate, CreatedBy, CreatedDateUtc, IsPinned, IsDeleted)
VALUES (NEWID(), @TenantId, @CatEventId, N'Campaign', @Body, @Subject, @Channel, @Template, @Body, @TargetCount, NULL, @SentAt, @SentBy, SYSUTCDATETIME(), 0, 0);
UPDATE Claims.CatAffectedInsured SET BlastSent = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE CatEventId = @CatEventId AND GeoTagged = 1 AND IsDeleted = 0;
SELECT @TargetCount;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { request.TenantId, request.CatEventId, request.Channel, request.Template, request.Subject, request.Body, request.SentAt, request.SentBy }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateFastCatFnolAsync(FastCatFnolRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SET XACT_ABORT ON; BEGIN TRAN;
DECLARE @CatCode nvarchar(80) = (SELECT TOP 1 CatCode FROM Claims.CatEvent WHERE CatEventId = @CatEventId AND TenantId = @TenantId AND IsDeleted = 0);
DECLARE @PolicyId uniqueidentifier,@AccountId uniqueidentifier,@CarrierId uniqueidentifier,@Carrier nvarchar(120),@Lob nvarchar(80),@ResolvedInsured nvarchar(160);
SELECT @PolicyId=bp.PolicyId,@AccountId=bp.AccountId,@CarrierId=bp.CarrierId,@Carrier=cr.CarrierName,@Lob=bp.LineOfBusiness,@ResolvedInsured=a.AccountName FROM Submissions.BoundPolicy bp JOIN Client.Account a ON a.TenantId=bp.TenantId AND a.AccountId=bp.AccountId AND a.IsDeleted=0 LEFT JOIN Agency.Carrier cr ON cr.TenantId=bp.TenantId AND cr.CarrierId=bp.CarrierId AND cr.IsDeleted=0 WHERE bp.TenantId=@TenantId AND bp.PolicyNumber=@PolicyNumber AND bp.IsDeleted=0;
IF @CatCode IS NULL THROW 51000,N'Catastrophe event not found.',1;
IF @PolicyId IS NULL THROW 51000,N'An authoritative bound policy is required for CAT FNOL.',1;
IF NOT EXISTS(SELECT 1 FROM Claims.CatAffectedInsured WHERE CatEventId=@CatEventId AND AccountId=@AccountId AND PolicyNumber=@PolicyNumber AND IsDeleted=0) THROW 51000,N'Policy is not in the affected-insured population.',1;
DECLARE @Next int = ISNULL((SELECT MAX(TRY_CONVERT(int, RIGHT(ClaimNumber, 5))) FROM Claims.Claim WHERE TenantId = @TenantId AND ClaimNumber LIKE CONCAT('CLM-', YEAR(SYSUTCDATETIME()), '-%')), 0) + 1;
DECLARE @ClaimNumber nvarchar(50) = CONCAT('CLM-', YEAR(SYSUTCDATETIME()), '-', FORMAT(@Next, '00000'));

INSERT INTO Claims.Claim
(ClaimId, TenantId, PolicyId, AccountId, CarrierId, ClaimNumber, PolicyNumber, AccountName, Lob, Carrier, Status, LossType, PrimaryClaimant,
 DateOfLoss, DateReported, ClosedDate, TotalIncurred, TotalReserves, TotalPaid, AssignedHandler, IsLitigation, HasSubrogation,
 IsCatastrophe, IsDisputed, FollowUpReason, Priority, FollowUpDueDate, IsSnoozed, CatCode, LossLocation, StateOfLoss,
 LossDescription, CauseOfLoss, CarrierClaimNumber, ReportedBy, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
(@ClaimId, @TenantId, @PolicyId, @AccountId, @CarrierId, @ClaimNumber, @PolicyNumber, @ResolvedInsured, @Lob, @Carrier, N'Open', @LossType, @ResolvedInsured,
 CAST(SYSUTCDATETIME() AS date), CAST(SYSUTCDATETIME() AS date), NULL, 0, 0, 0, N'Unassigned', 0, 0,
 1, 0, N'CAT fast FNOL triage', N'High', DATEADD(day, 1, CAST(SYSUTCDATETIME() AS date)), 0, @CatCode, NULL, NULL,
 CONCAT(@Description, CASE WHEN @Description IS NULL OR @Description = N'' THEN N'' ELSE N' ' END, N'Contact phone: ', @Phone, N'. Estimated damage: ', @EstimatedRange), @LossType, NULL, @CreatedByName, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);

INSERT INTO Claims.ClaimActivity (ClaimActivityId, TenantId, ClaimId, ActivityType, ActivityDescription, Title, Category, Party, Notes, ActivityDate, CreatedBy, CreatedDateUtc, IsPinned, IsDeleted)
VALUES (NEWID(), @TenantId, @ClaimId, N'FNOL', @Description, N'Fast CAT FNOL received', @LossType, @ResolvedInsured, CONCAT(N'Phone: ', @Phone, N'. Estimated damage: ', @EstimatedRange, N'. ', @Description), SYSUTCDATETIME(), @CreatedByName, SYSUTCDATETIME(), 1, 0);

INSERT Claims.ClaimParty(ClaimPartyId,TenantId,ClaimId,PartyTypeCode,DisplayName,PhoneNumber,IsPrimary,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@ClaimId,N'Insured',@ResolvedInsured,@Phone,1,1,SYSUTCDATETIME(),@CreatedByUserId,0);
INSERT Claims.ClaimStatusHistory(ClaimStatusHistoryId,TenantId,ClaimId,NewStatusCode,ReasonCode,Notes,ChangedDateUtc,ChangedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@ClaimId,N'Open',N'CATFNOL',N'CAT FNOL created from affected bound policy.',SYSUTCDATETIME(),@CreatedByUserId,0);

UPDATE Claims.CatAffectedInsured SET FnolFiled = 1, ContactStatus = N'FNOL Filed', ModifiedDateUtc = SYSUTCDATETIME() WHERE CatEventId = @CatEventId AND PolicyNumber = @PolicyNumber AND IsDeleted = 0;
COMMIT; SELECT @ClaimId;";
        var claimId = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { ClaimId = claimId, request.TenantId, request.CatEventId, request.PolicyNumber, request.InsuredName, request.Phone, request.LossType, request.EstimatedRange, request.Description, request.CreatedByUserId, request.CreatedByName }, cancellationToken: cancellationToken));
    }

    public Task<Guid> AssignAdjusterAsync(AssignClaimAdjusterRequest request, CancellationToken cancellationToken = default)
        => ExecuteGuidAsync("""SET XACT_ABORT ON; BEGIN TRAN; DECLARE @Id uniqueidentifier=NEWID(); IF NOT EXISTS(SELECT 1 FROM Claims.Claim WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0) THROW 51000,N'Claim not found.',1; IF @IsPrimary=1 UPDATE Claims.ClaimAdjuster SET IsPrimary=0,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsPrimary=1 AND IsDeleted=0; INSERT Claims.ClaimAdjuster(ClaimAdjusterId,TenantId,ClaimId,AdjusterTypeCode,AdjusterName,CompanyName,EmailAddress,PhoneNumber,LicenseNumber,IsPrimary,AssignmentStatusCode,AssignedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@ClaimId,@AdjusterTypeCode,@AdjusterName,@CompanyName,@EmailAddress,@PhoneNumber,@LicenseNumber,@IsPrimary,N'Active',SYSUTCDATETIME(),SYSUTCDATETIME(),@UserId,0); IF @IsPrimary=1 UPDATE Claims.Claim SET PrimaryAdjusterId=@Id,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND ClaimId=@ClaimId; INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'Adjuster',@Id,N'Assigned',N'Claim adjuster assigned.',JSON_OBJECT(N'Name':@AdjusterName,N'Type':@AdjusterTypeCode,N'Primary':@IsPrimary),@UserId,SYSUTCDATETIME()); COMMIT; SELECT @Id;""", request, cancellationToken);

    public Task<Guid> UpsertPartyAsync(UpsertClaimPartyRequest request, CancellationToken cancellationToken = default)
        => ExecuteGuidAsync("""SET XACT_ABORT ON; BEGIN TRAN; DECLARE @Id uniqueidentifier=COALESCE(@ClaimPartyId,NEWID()); IF NOT EXISTS(SELECT 1 FROM Claims.Claim WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0) THROW 51000,N'Claim not found.',1; IF @ContactId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Client.Contact WHERE TenantId=@TenantId AND ContactId=@ContactId AND IsDeleted=0) THROW 51000,N'Contact not found.',1; IF @IsPrimary=1 UPDATE Claims.ClaimParty SET IsPrimary=0,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND PartyTypeCode=@PartyTypeCode AND IsPrimary=1 AND IsDeleted=0; IF EXISTS(SELECT 1 FROM Claims.ClaimParty WHERE TenantId=@TenantId AND ClaimPartyId=@Id AND IsDeleted=0) UPDATE Claims.ClaimParty SET ContactId=@ContactId,PartyTypeCode=@PartyTypeCode,DisplayName=@DisplayName,OrganizationName=@OrganizationName,EmailAddress=@EmailAddress,PhoneNumber=@PhoneNumber,AddressJson=@AddressJson,PreferredContactMethodCode=@PreferredContactMethodCode,IsPrimary=@IsPrimary,IsActive=@IsActive,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE ClaimPartyId=@Id; ELSE INSERT Claims.ClaimParty(ClaimPartyId,TenantId,ClaimId,ContactId,PartyTypeCode,DisplayName,OrganizationName,EmailAddress,PhoneNumber,AddressJson,PreferredContactMethodCode,IsPrimary,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@ClaimId,@ContactId,@PartyTypeCode,@DisplayName,@OrganizationName,@EmailAddress,@PhoneNumber,@AddressJson,@PreferredContactMethodCode,@IsPrimary,@IsActive,SYSUTCDATETIME(),@UserId,0); INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'ClaimParty',@Id,N'Upserted',N'Claim party saved.',@UserId,SYSUTCDATETIME()); COMMIT; SELECT @Id;""", request, cancellationToken);

    public Task<Guid> CreateFinancialTransactionAsync(CreateClaimFinancialTransactionRequest request, CancellationToken cancellationToken = default)
        => ExecuteGuidAsync(FinancialInsertSql, request, cancellationToken);

    public Task<Guid> ReverseFinancialTransactionAsync(ReverseClaimFinancialTransactionRequest request, CancellationToken cancellationToken = default)
        => ExecuteGuidAsync(FinancialReversalSql, new { request.TenantId, Id=request.ClaimFinancialTransactionId, request.Reason, request.UserId }, cancellationToken);

    public Task<Guid> CreateNoteAsync(CreateClaimNoteRequest request, CancellationToken cancellationToken = default)
        => ExecuteGuidAsync("""SET XACT_ABORT ON; BEGIN TRAN; DECLARE @Id uniqueidentifier=NEWID(); INSERT Claims.ClaimNote(ClaimNoteId,TenantId,ClaimId,NoteTypeCode,Subject,NoteText,IsPinned,IsConfidential,NoteDateUtc,CreatedDateUtc,CreatedByUserId,CreatedByName,IsDeleted) SELECT @Id,@TenantId,@ClaimId,@NoteTypeCode,@Subject,@NoteText,@IsPinned,@IsConfidential,SYSUTCDATETIME(),SYSUTCDATETIME(),@UserId,@UserName,0 FROM Claims.Claim WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0; IF @@ROWCOUNT<>1 THROW 51000,N'Claim not found.',1; INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'ClaimNote',@Id,N'Created',N'Claim note created.',@UserId,SYSUTCDATETIME()); COMMIT; SELECT @Id;""", request, cancellationToken);

    public Task<Guid> CreateTaskAsync(CreateClaimTaskRequest request, CancellationToken cancellationToken = default)
        => ExecuteGuidAsync("""SET XACT_ABORT ON; BEGIN TRAN; DECLARE @Id uniqueidentifier=NEWID(),@OpsId uniqueidentifier=NEWID(),@TaskNumber nvarchar(50)=CONCAT(N'CLMT-',FORMAT(SYSUTCDATETIME(),N'yyyyMMddHHmmss'),N'-',LEFT(REPLACE(CONVERT(nvarchar(36),@Id),N'-',N''),6)); IF NOT EXISTS(SELECT 1 FROM Claims.Claim WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0) THROW 51000,N'Claim not found.',1; INSERT OPS.TaskItem(TaskItemId,TenantId,TaskNumber,Title,Description,TaskTypeCode,StageCode,PriorityCode,StatusCode,RelatedEntityName,RelatedEntityId,AssignedToUserId,DueDate,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@OpsId,@TenantId,@TaskNumber,@Title,@Description,@TaskTypeCode,N'Claims',@PriorityCode,N'Open',N'Claim',@ClaimId,@AssignedToUserId,@DueDate,SYSUTCDATETIME(),@UserId,0); INSERT Claims.ClaimTask(ClaimTaskId,TenantId,ClaimId,OpsTaskItemId,TaskTypeCode,Title,Description,PriorityCode,StatusCode,AssignedToUserId,AssignedToName,DueDate,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@ClaimId,@OpsId,@TaskTypeCode,@Title,@Description,@PriorityCode,N'Open',@AssignedToUserId,@AssignedToName,@DueDate,SYSUTCDATETIME(),@UserId,0); INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'ClaimTask',@Id,N'Created',N'Claim follow-up task created.',@UserId,SYSUTCDATETIME()); COMMIT; SELECT @Id;""", request, cancellationToken);

    public async Task CompleteTaskAsync(CompleteClaimTaskRequest request, CancellationToken cancellationToken = default)
    {
        const string sql="""SET XACT_ABORT ON; BEGIN TRAN; DECLARE @ClaimId uniqueidentifier,@OpsId uniqueidentifier; SELECT @ClaimId=ClaimId,@OpsId=OpsTaskItemId FROM Claims.ClaimTask WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND ClaimTaskId=@Id AND StatusCode<>N'Completed' AND IsDeleted=0; IF @ClaimId IS NULL THROW 51000,N'Open claim task not found.',1; UPDATE Claims.ClaimTask SET StatusCode=N'Completed',CompletedDateUtc=SYSUTCDATETIME(),Description=CASE WHEN NULLIF(@CompletionNotes,N'') IS NULL THEN Description ELSE CONCAT(COALESCE(Description,N''),NCHAR(13)+NCHAR(10),@CompletionNotes) END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE ClaimTaskId=@Id; UPDATE OPS.TaskItem SET StatusCode=N'Completed',CompletedDate=CONVERT(date,SYSUTCDATETIME()),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND TaskItemId=@OpsId AND IsDeleted=0; INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'ClaimTask',@Id,N'Completed',N'Claim follow-up task completed.',@UserId,SYSUTCDATETIME()); COMMIT;""";
        await ExecuteAsync(sql,new {request.TenantId,Id=request.ClaimTaskId,request.CompletionNotes,request.UserId},cancellationToken);
    }

    public Task<Guid> LinkDocumentAsync(LinkClaimDocumentRequest request, CancellationToken cancellationToken = default)
        => ExecuteGuidAsync("""SET XACT_ABORT ON; BEGIN TRAN; DECLARE @Id uniqueidentifier=NEWID(); INSERT Claims.ClaimDocumentLink(ClaimDocumentLinkId,TenantId,ClaimId,DocumentId,DocumentRoleCode,Description,LinkedDateUtc,LinkedByUserId,IsDeleted) SELECT @Id,@TenantId,c.ClaimId,d.DocumentId,@DocumentRoleCode,@Description,SYSUTCDATETIME(),@UserId,0 FROM Claims.Claim c JOIN DMS.Document d ON d.TenantId=c.TenantId AND d.DocumentId=@DocumentId AND d.IsDeleted=0 WHERE c.TenantId=@TenantId AND c.ClaimId=@ClaimId AND c.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM Claims.ClaimDocumentLink x WHERE x.TenantId=@TenantId AND x.ClaimId=@ClaimId AND x.DocumentId=@DocumentId AND x.IsDeleted=0); IF @@ROWCOUNT<>1 THROW 51000,N'Claim/document not found or already linked.',1; UPDATE DMS.Document SET EntityName=N'Claim',EntityId=@ClaimId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND DocumentId=@DocumentId; INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'ClaimDocument',@Id,N'Linked',N'Document linked to claim.',@UserId,SYSUTCDATETIME()); COMMIT; SELECT @Id;""", request, cancellationToken);

    public async Task<LossRunImportResultDto> ImportLossRunAsync(ImportLossRunRequest request, CancellationToken cancellationToken = default)
    {
        var rows=ParseLossRun(request.CsvContent); if(rows.Count==0) throw new InvalidOperationException("Loss run contains no data rows.");
        var hash=ClaimRules.ComputeImportHash(request.CsvContent); using var cn=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken); using var tx=cn.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            if(await cn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM Claims.LossRun WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND SourceFileHash=@Hash AND IsDeleted=0;",new{request.TenantId,Hash=hash},tx,cancellationToken:cancellationToken))>0) throw new InvalidOperationException("Loss run file has already been imported.");
            var id=Guid.NewGuid(); var errors=rows.Count(x=>x.Error is not null); var number=$"LR-{request.AsOfDate:yyyyMMdd}-{id:N}"[..21];
            await cn.ExecuteAsync(new CommandDefinition("INSERT Claims.LossRun(LossRunId,TenantId,AccountId,PolicyId,CarrierId,LossRunNumber,AsOfDate,PeriodStartDate,PeriodEndDate,SourceDocumentId,SourceFileName,SourceFileHash,ImportStatusCode,TotalClaimCount,TotalIncurred,TotalReserved,TotalPaid,CreatedDateUtc,CreatedByUserId,IsDeleted) SELECT @Id,@TenantId,a.AccountId,@PolicyId,@CarrierId,@Number,@AsOfDate,@PeriodStartDate,@PeriodEndDate,@SourceDocumentId,@SourceFileName,@Hash,@Status,@Count,@Incurred,@Reserved,@Paid,SYSUTCDATETIME(),@UserId,0 FROM Client.Account a WHERE a.TenantId=@TenantId AND a.AccountId=@AccountId AND a.IsDeleted=0; IF @@ROWCOUNT<>1 THROW 51000,N'Account not found.',1;",new{Id=id,request.TenantId,request.AccountId,request.PolicyId,request.CarrierId,Number=number,request.AsOfDate,request.PeriodStartDate,request.PeriodEndDate,request.SourceDocumentId,request.SourceFileName,Hash=hash,Status=errors==0?"Validated":"Failed",Count=rows.Count,Incurred=rows.Sum(x=>x.Incurred),Reserved=rows.Sum(x=>x.Reserve),Paid=rows.Sum(x=>x.Paid),request.UserId},tx,cancellationToken:cancellationToken));
            const string insert="INSERT Claims.LossRunLine(LossRunLineId,TenantId,LossRunId,LineNumber,ClaimId,CarrierClaimNumber,PolicyNumber,ClaimantName,DateOfLoss,ClaimStatusCode,LossDescription,IncurredAmount,ReserveAmount,PaidAmount,MatchStatusCode,ValidationErrorsJson,CreatedDateUtc,IsDeleted) SELECT NEWID(),@TenantId,@LossRunId,@LineNumber,c.ClaimId,@CarrierClaimNumber,@PolicyNumber,@ClaimantName,@DateOfLoss,@Status,@Description,@Incurred,@Reserve,@Paid,CASE WHEN c.ClaimId IS NULL THEN N'Unmatched' ELSE N'Matched' END,@Error,SYSUTCDATETIME(),0 FROM(SELECT 1 x)s OUTER APPLY(SELECT TOP 1 ClaimId FROM Claims.Claim WHERE TenantId=@TenantId AND IsDeleted=0 AND ((@CarrierClaimNumber IS NOT NULL AND CarrierClaimNumber=@CarrierClaimNumber) OR (@CarrierClaimNumber IS NULL AND PolicyNumber=@PolicyNumber AND DateOfLoss=@DateOfLoss)) ORDER BY CreatedDateUtc)c;";
            foreach(var row in rows) await cn.ExecuteAsync(new CommandDefinition(insert,new{request.TenantId,LossRunId=id,row.LineNumber,row.CarrierClaimNumber,row.PolicyNumber,row.ClaimantName,row.DateOfLoss,row.Status,row.Description,row.Incurred,row.Reserve,row.Paid,row.Error},tx,cancellationToken:cancellationToken));
            var matched=await cn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM Claims.LossRunLine WHERE LossRunId=@Id AND MatchStatusCode=N'Matched' AND IsDeleted=0;",new{Id=id},tx,cancellationToken:cancellationToken)); tx.Commit(); return new(id,rows.Count,matched,errors,errors==0?"Validated":"Failed");
        } catch {tx.Rollback();throw;}
    }

    public async Task<IReadOnlyList<LossRunDto>> GetLossRunsAsync(Guid tenantId, Guid? accountId, CancellationToken cancellationToken = default)
    { using var cn=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken); return (await cn.QueryAsync<LossRunDto>(new CommandDefinition("SELECT LossRunId,TenantId,AccountId,PolicyId,CarrierId,LossRunNumber,AsOfDate,PeriodStartDate,PeriodEndDate,SourceDocumentId,SourceFileName,ImportStatusCode,TotalClaimCount,TotalIncurred,TotalReserved,TotalPaid,CreatedDateUtc FROM Claims.LossRun WHERE TenantId=@TenantId AND IsDeleted=0 AND (@AccountId IS NULL OR AccountId=@AccountId) ORDER BY AsOfDate DESC;",new{TenantId=tenantId,AccountId=accountId},cancellationToken:cancellationToken))).AsList(); }

    private async Task<Guid> ExecuteGuidAsync(string sql, object parameters, CancellationToken cancellationToken)
    { using var cn=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken); return await cn.QuerySingleAsync<Guid>(new CommandDefinition(sql,parameters,cancellationToken:cancellationToken)); }

    private async Task ExecuteAsync(string sql, object parameters, CancellationToken cancellationToken)
    { using var cn=await _connectionFactory.CreateOpenConnectionAsync(cancellationToken); await cn.ExecuteAsync(new CommandDefinition(sql,parameters,cancellationToken:cancellationToken)); }

    private static List<LossRunRow> ParseLossRun(string content)
    {
        var lines=content.Replace("\r\n","\n",StringComparison.Ordinal).Split('\n',StringSplitOptions.RemoveEmptyEntries); if(lines.Length<2)return [];
        var headers=SplitCsv(lines[0]).Select((x,i)=>(Name:Normalize(x),Index:i)).ToDictionary(x=>x.Name,x=>x.Index,StringComparer.OrdinalIgnoreCase);
        string Cell(string[] cells,params string[] names)=>names.Select(n=>headers.TryGetValue(Normalize(n),out var i)&&i<cells.Length?cells[i].Trim():string.Empty).FirstOrDefault(x=>x.Length>0)??string.Empty;
        var result=new List<LossRunRow>(); for(var index=1;index<lines.Length;index++){var cells=SplitCsv(lines[index]);var carrier=Cell(cells,"carrierclaimnumber","claimnumber");var policy=Cell(cells,"policynumber","policy");var claimant=Cell(cells,"claimant","claimantname");var description=Cell(cells,"lossdescription","description");DateOnly? loss=DateOnly.TryParse(Cell(cells,"dateofloss","lossdate"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)?d:null;decimal.TryParse(Cell(cells,"incurred","totalincurred"),NumberStyles.Currency,CultureInfo.InvariantCulture,out var incurred);decimal.TryParse(Cell(cells,"reserve","reserved"),NumberStyles.Currency,CultureInfo.InvariantCulture,out var reserve);decimal.TryParse(Cell(cells,"paid","totalpaid"),NumberStyles.Currency,CultureInfo.InvariantCulture,out var paid);var error=string.IsNullOrWhiteSpace(carrier)&&string.IsNullOrWhiteSpace(policy)?"Claim or policy number is required.":loss is null?"Date of loss is required.":incurred<0||reserve<0||paid<0?"Financial amounts cannot be negative.":null;result.Add(new(index,carrier.Length==0?null:carrier,policy.Length==0?null:policy,claimant.Length==0?null:claimant,loss,Cell(cells,"status","claimstatus"),description,incurred,reserve,paid,error));}return result;
    }

    private static string[] SplitCsv(string line)
    { var values=new List<string>();var value=new System.Text.StringBuilder();var quoted=false;for(var i=0;i<line.Length;i++){var ch=line[i];if(ch=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){value.Append('"');i++;}else quoted=!quoted;}else if(ch==','&&!quoted){values.Add(value.ToString());value.Clear();}else value.Append(ch);}values.Add(value.ToString());return values.ToArray(); }
    private static string Normalize(string value)=>new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private sealed record LossRunRow(int LineNumber,string? CarrierClaimNumber,string? PolicyNumber,string? ClaimantName,DateOnly? DateOfLoss,string Status,string Description,decimal Incurred,decimal Reserve,decimal Paid,string? Error);

    private const string FinancialInsertSql="""
SET XACT_ABORT ON; BEGIN TRAN; DECLARE @Id uniqueidentifier=NEWID(); IF NOT EXISTS(SELECT 1 FROM Claims.Claim WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND IsDeleted=0) THROW 51000,N'Claim not found.',1; IF @TransactionTypeCode NOT IN(N'ReserveSet',N'ReserveRelease',N'Payment',N'Recovery') OR @Amount<=0 THROW 51000,N'Invalid financial transaction.',1; IF @PayeeClaimPartyId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Claims.ClaimParty WHERE TenantId=@TenantId AND ClaimId=@ClaimId AND ClaimPartyId=@PayeeClaimPartyId AND IsDeleted=0) THROW 51000,N'Payee is not a party to this claim.',1; INSERT Claims.ClaimFinancialTransaction(ClaimFinancialTransactionId,TenantId,ClaimId,TransactionTypeCode,TransactionDate,Amount,CurrencyCode,CoverageCode,PayeeClaimPartyId,ReferenceNumber,StatusCode,Description,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@ClaimId,@TransactionTypeCode,@TransactionDate,@Amount,@CurrencyCode,@CoverageCode,@PayeeClaimPartyId,@ReferenceNumber,N'Posted',@Description,SYSUTCDATETIME(),@UserId,0); EXEC Claims.RecalculateClaimFinancials @TenantId,@ClaimId,@UserId; INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'ClaimFinancialTransaction',@Id,N'Posted',N'Claim financial transaction posted.',JSON_OBJECT(N'Type':@TransactionTypeCode,N'Amount':@Amount),@UserId,SYSUTCDATETIME()); COMMIT; SELECT @Id;
""";
    private const string FinancialReversalSql="""
SET XACT_ABORT ON; BEGIN TRAN; DECLARE @NewId uniqueidentifier=NEWID(),@ClaimId uniqueidentifier,@Type nvarchar(50),@Amount decimal(18,2),@Currency nvarchar(3),@Coverage nvarchar(80),@Payee uniqueidentifier,@Reference nvarchar(100); SELECT @ClaimId=ClaimId,@Type=TransactionTypeCode,@Amount=Amount,@Currency=CurrencyCode,@Coverage=CoverageCode,@Payee=PayeeClaimPartyId,@Reference=ReferenceNumber FROM Claims.ClaimFinancialTransaction WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND ClaimFinancialTransactionId=@Id AND StatusCode=N'Posted' AND ReversalOfTransactionId IS NULL AND IsDeleted=0; IF @ClaimId IS NULL OR EXISTS(SELECT 1 FROM Claims.ClaimFinancialTransaction WHERE ReversalOfTransactionId=@Id AND IsDeleted=0) THROW 51000,N'Posted transaction not found or already reversed.',1; UPDATE Claims.ClaimFinancialTransaction SET StatusCode=N'Reversed' WHERE ClaimFinancialTransactionId=@Id; INSERT Claims.ClaimFinancialTransaction(ClaimFinancialTransactionId,TenantId,ClaimId,TransactionTypeCode,TransactionDate,Amount,CurrencyCode,CoverageCode,PayeeClaimPartyId,ReferenceNumber,StatusCode,ReversalOfTransactionId,Description,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@NewId,@TenantId,@ClaimId,CASE @Type WHEN N'ReserveSet' THEN N'ReserveRelease' WHEN N'ReserveRelease' THEN N'ReserveSet' WHEN N'Payment' THEN N'Recovery' ELSE N'Payment' END,CONVERT(date,SYSUTCDATETIME()),@Amount,@Currency,@Coverage,@Payee,@Reference,N'Posted',@Id,@Reason,SYSUTCDATETIME(),@UserId,0); EXEC Claims.RecalculateClaimFinancials @TenantId,@ClaimId,@UserId; INSERT Claims.ClaimAuditEvent(ClaimAuditEventId,TenantId,ClaimId,EntityTypeCode,EntityId,EventTypeCode,EventDescription,NewValueJson,ActorUserId,CreatedDateUtc) VALUES(NEWID(),@TenantId,@ClaimId,N'ClaimFinancialTransaction',@NewId,N'Reversed',N'Claim financial transaction reversed.',JSON_OBJECT(N'OriginalId':@Id,N'Reason':@Reason),@UserId,SYSUTCDATETIME()); COMMIT; SELECT @NewId;
""";
}
