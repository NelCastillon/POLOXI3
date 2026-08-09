using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Accounts;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AccountRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Client.Account
(
    AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
    MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
    ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue,
    CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @AccountId, @TenantId, @AccountNumber, @AccountName, @AccountTypeCode,
    @MainEmail, @MainPhone, @StatusCode, @SegmentCode, @OwnerUserId,
    @ParentAccountId, @LifecycleStageCode, @Industry, @Website, @AnnualRevenue,
    SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AccountId = id,
            request.TenantId,
            request.AccountNumber,
            request.AccountName,
            request.AccountTypeCode,
            request.MainEmail,
            request.MainPhone,
            request.StatusCode,
            request.SegmentCode,
            request.OwnerUserId,
            request.ParentAccountId,
            request.LifecycleStageCode,
            request.Industry,
            request.Website,
            request.AnnualRevenue,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
       MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
       ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue,
       CreatedDateUtc, ModifiedDateUtc
FROM Client.Account
WHERE AccountId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AccountDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AccountDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
           MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
           ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue,
           CreatedDateUtc, ModifiedDateUtc
    FROM Client.Account
    WHERE TenantId = @TenantId AND IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR AccountName LIKE '%' + @SearchTerm + '%'
           OR AccountNumber LIKE '%' + @SearchTerm + '%'
           OR MainEmail LIKE '%' + @SearchTerm + '%'
          )
)
SELECT * FROM Paged
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM Client.Account
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (
       @SearchTerm IS NULL OR @SearchTerm = ''
       OR AccountName LIKE '%' + @SearchTerm + '%'
       OR AccountNumber LIKE '%' + @SearchTerm + '%'
       OR MainEmail LIKE '%' + @SearchTerm + '%'
      );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
            PageSize = Math.Max(pageSize, 1)
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<AccountDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AccountDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Client.Account
SET AccountName = @AccountName,
    AccountTypeCode = @AccountTypeCode,
    MainEmail = @MainEmail,
    MainPhone = @MainPhone,
    StatusCode = @StatusCode,
    SegmentCode = @SegmentCode,
    OwnerUserId = @OwnerUserId,
    ParentAccountId = @ParentAccountId,
    LifecycleStageCode = @LifecycleStageCode,
    Industry = @Industry,
    Website = @Website,
    AnnualRevenue = @AnnualRevenue,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE AccountId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.AccountName,
            request.AccountTypeCode,
            request.MainEmail,
            request.MainPhone,
            request.StatusCode,
            request.SegmentCode,
            request.OwnerUserId,
            request.ParentAccountId,
            request.LifecycleStageCode,
            request.Industry,
            request.Website,
            request.AnnualRevenue,
            request.ModifiedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Client.Account
SET IsDeleted = 1,
    StatusCode = 'Inactive',
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @UserId
WHERE AccountId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ContactDto>> GetContactsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.ContactId, c.TenantId, c.AccountId, a.AccountName,
       c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
       c.ContactTypeCode, c.IsBillingContact, c.IsPortalUser,
       c.IsKeyContact, c.IsServiceContact, c.ParentContactId, c.PreferredContactMethod,
       c.StatusCode, c.CreatedDateUtc
FROM Client.Contact c
LEFT JOIN Client.Account a ON a.AccountId = c.AccountId
WHERE c.AccountId = @AccountId AND c.IsDeleted = 0
ORDER BY c.LastName, c.FirstName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var results = await cn.QueryAsync<ContactDto>(
            new CommandDefinition(sql, new { AccountId = accountId }, cancellationToken: cancellationToken));
        return results.AsList();
    }

    public async Task<Account360Dto?> GetAccount360Async(Guid tenantId, Guid accountId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT AccountId, TenantId, AccountNumber, AccountName, DbaName, AccountTypeCode, MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
       ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue, CreatedDateUtc, ModifiedDateUtc
FROM Client.Account WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0;

SELECT c.ContactId, c.TenantId, c.AccountId, a.AccountName, c.FirstName, c.LastName, c.Email, c.Phone, c.JobTitle,
       c.ContactTypeCode, c.IsBillingContact, c.IsPortalUser, c.IsKeyContact, c.IsServiceContact, c.ParentContactId,
       c.PreferredContactMethod, c.StatusCode, c.CreatedDateUtc
FROM Client.Contact c INNER JOIN Client.Account a ON a.TenantId=c.TenantId AND a.AccountId=c.AccountId
WHERE c.TenantId=@TenantId AND c.AccountId=@AccountId AND c.IsDeleted=0 ORDER BY c.LastName,c.FirstName;

SELECT s.AccountStakeholderId,s.TenantId,s.AccountId,s.ContactId,CONCAT(c.FirstName,N' ',c.LastName) ContactName,s.StakeholderRoleCode,s.OwnershipPercentage,s.IsPrimary,s.EffectiveDate,s.ExpirationDate,s.Notes
FROM Client.AccountStakeholder s INNER JOIN Client.Contact c ON c.TenantId=s.TenantId AND c.ContactId=s.ContactId AND c.IsDeleted=0
WHERE s.TenantId=@TenantId AND s.AccountId=@AccountId AND s.IsDeleted=0 ORDER BY s.IsPrimary DESC,s.StakeholderRoleCode,c.LastName,c.FirstName;
SELECT p.AccountCommunicationPreferenceId,p.TenantId,p.AccountId,p.ContactId,CASE WHEN c.ContactId IS NULL THEN NULL ELSE CONCAT(c.FirstName,N' ',c.LastName) END ContactName,p.CommunicationPurposeCode,p.ChannelCode,p.PreferenceStatusCode,p.PreferredTimeZoneCode,p.PreferredStartTime,p.PreferredEndTime,p.ConsentSourceCode,p.ConsentDateUtc,p.Notes
FROM Client.AccountCommunicationPreference p LEFT JOIN Client.Contact c ON c.TenantId=p.TenantId AND c.ContactId=p.ContactId AND c.IsDeleted=0
WHERE p.TenantId=@TenantId AND p.AccountId=@AccountId AND p.IsDeleted=0 ORDER BY p.CommunicationPurposeCode,p.ChannelCode;
SELECT a.AccountServiceAssignmentId,a.TenantId,a.AccountId,a.UserId,COALESCE(NULLIF(u.DisplayName,N''),NULLIF(u.FullName,N''),u.UserName) UserName,a.AssignmentRoleCode,a.IsPrimary,a.EffectiveDate,a.ExpirationDate,a.Notes
FROM Client.AccountServiceAssignment a INNER JOIN IAM.[User] u ON u.TenantId=a.TenantId AND u.UserId=a.UserId AND u.IsDeleted=0
WHERE a.TenantId=@TenantId AND a.AccountId=@AccountId AND a.IsDeleted=0 ORDER BY a.AssignmentRoleCode,a.IsPrimary DESC,UserName;

SELECT AccountNamedInsuredId,TenantId,AccountId,ContactId,InsuredTypeCode,LegalName,DbaName,TaxIdentifier,RelationshipCode,IsPrimary,EffectiveDate,ExpirationDate,Notes,CreatedDateUtc,ModifiedDateUtc
FROM Client.AccountNamedInsured WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY IsPrimary DESC,LegalName;
SELECT AccountLocationId,TenantId,AccountId,LocationNumber,LocationTypeCode,LocationName,AddressLine1,AddressLine2,City,StateCode,PostalCode,CountryCode,County,IsPrimary,IsMailingAddress,Latitude,Longitude,OccupancyCode,AnnualRevenue,EmployeeCount,Notes,CreatedDateUtc,ModifiedDateUtc
FROM Client.AccountLocation WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY IsPrimary DESC,LocationNumber;
SELECT AccountVehicleId,TenantId,AccountId,AccountLocationId,VehicleNumber,Vin,ModelYear,Make,Model,VehicleTypeCode,UseTypeCode,GaragingStateCode,GaragingPostalCode,RadiusMiles,AnnualMileage,CostNew,StatedValue,IsActive,Notes,CreatedDateUtc,ModifiedDateUtc
FROM Client.AccountVehicle WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY VehicleNumber;
SELECT AccountDriverId,TenantId,AccountId,ContactId,DriverNumber,FirstName,LastName,DateOfBirth,LicenseNumber,LicenseStateCode,LicenseClassCode,LicenseExpirationDate,HireDate,YearsExperience,DriverStatusCode,IsExcluded,Notes,CreatedDateUtc,ModifiedDateUtc
FROM Client.AccountDriver WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY LastName,FirstName;
SELECT AccountPropertyId,TenantId,AccountId,AccountLocationId,PropertyNumber,PropertyTypeCode,ConstructionTypeCode,OccupancyCode,YearBuilt,SquareFeet,NumberOfStories,BuildingValue,ContentsValue,BusinessIncomeValue,ProtectionClassCode,RoofTypeCode,RoofYear,SprinkleredPercentage,IsActive,Notes,CreatedDateUtc,ModifiedDateUtc
FROM Client.AccountProperty WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY PropertyNumber;
SELECT AccountScheduleItemId,TenantId,AccountId,AccountLocationId,ScheduleTypeCode,ItemNumber,ItemDescription,Manufacturer,Model,SerialNumber,AcquisitionDate,AppraisalDate,ScheduledValue,DeductibleAmount,IsActive,Notes,CreatedDateUtc,ModifiedDateUtc
FROM Client.AccountScheduleItem WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY ScheduleTypeCode,ItemNumber;

SELECT ActivityId,ActivityType AS ActivityTypeCode,COALESCE([Subject],N'Account activity') AS Title,Notes AS Description,NULL AS RelatedEntityType,NULL AS RelatedEntityId,OccurredAtUtc AS OccurredDateUtc,CreatedByUserId
FROM Client.AccountActivity WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY OccurredAtUtc DESC;

SELECT r.RelationshipId,r.RelatedAccountId,related.AccountName AS RelatedAccountName,r.RelationshipType AS RelationshipTypeCode,r.[Description],r.IsActive
FROM Client.AccountRelationship r INNER JOIN Client.Account related ON related.TenantId=r.TenantId AND related.AccountId=r.RelatedAccountId AND related.IsDeleted=0
WHERE r.TenantId=@TenantId AND r.AccountId=@AccountId AND r.IsDeleted=0 ORDER BY related.AccountName;

SELECT AccountReferenceOptionId,TenantId,OptionGroup,OptionCode,OptionName,Description,IsDefault,IsActive,SortOrder,CreatedDateUtc
FROM Client.AccountReferenceOption WHERE TenantId=@TenantId AND IsDeleted=0 AND IsActive=1 ORDER BY OptionGroup,SortOrder,OptionName;

SELECT n.AccountNoteId,n.TenantId,n.AccountId,a.AccountName,n.NoteText,n.NoteTypeCode,n.CreatedByUserId,n.CreatedDateUtc
FROM Client.AccountNote n INNER JOIN Client.Account a ON a.TenantId=n.TenantId AND a.AccountId=n.AccountId
WHERE n.TenantId=@TenantId AND n.AccountId=@AccountId AND n.IsDeleted=0 ORDER BY n.CreatedDateUtc DESC;
SELECT TaskItemId,TenantId,TaskNumber,Title,Description,TaskTypeCode,StageCode,PriorityCode,StatusCode,RelatedEntityName,RelatedEntityId,AccountId,AssignedToUserId,DueDate,CompletedDate,CreatedDateUtc,CreatedByUserId,ModifiedDateUtc,ModifiedByUserId,IsDeleted
FROM OPS.TaskItem WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY CASE WHEN StatusCode IN (N'Completed',N'Cancelled',N'Closed') THEN 1 ELSE 0 END,DueDate,CreatedDateUtc DESC;
SELECT DocumentId,TenantId,DocumentTypeCode,CategoryCode,EntityName,EntityId,FileName,StoragePath,ContentType,FileSizeBytes,VersionNumber,StatusCode,RetentionDate,Description,Tags,UploadedByName,CreatedDateUtc,ModifiedDateUtc
FROM DMS.Document WHERE TenantId=@TenantId AND EntityName=N'Account' AND EntityId=@AccountId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;
SELECT s.SubmissionId,s.TenantId,s.AccountId,a.AccountName,s.OpportunityId,COALESCE(o.OpportunityName,s.SubmissionNumber) OpportunityName,s.SubmissionNumber,s.LineOfBusiness,s.Status,s.Priority,s.AssignedToUserId,COALESCE(u.FullName,u.DisplayName,u.UserName) AssignedToUserName,s.EffectiveDate,s.ExpirationDate,s.TargetPremium,
 (SELECT COUNT(1) FROM Submissions.SubmissionMarket sm WHERE sm.TenantId=s.TenantId AND sm.SubmissionId=s.SubmissionId AND sm.IsDeleted=0) MarketCount,
 (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.SubmissionId=s.SubmissionId AND q.IsDeleted=0) QuoteCount,s.CreatedDateUtc,s.ModifiedDateUtc
FROM Submissions.Submission s INNER JOIN Client.Account a ON a.TenantId=s.TenantId AND a.AccountId=s.AccountId LEFT JOIN CRM.Opportunity o ON o.TenantId=s.TenantId AND o.OpportunityId=s.OpportunityId LEFT JOIN IAM.[User] u ON u.TenantId=s.TenantId AND u.UserId=s.AssignedToUserId
WHERE s.TenantId=@TenantId AND s.AccountId=@AccountId AND s.IsDeleted=0 ORDER BY s.CreatedDateUtc DESC;
SELECT o.OpportunityId,o.TenantId,o.OpportunityNumber,o.AccountId,a.AccountName,o.OpportunityName,o.EstimatedAmount,o.StatusCodeId StatusCode,o.OwnerUserId,o.CloseDate,o.WinProbability,o.ForecastCategoryCode,o.LeadId,o.StageName,o.Description,o.CreatedDateUtc,o.ModifiedDateUtc
FROM CRM.Opportunity o INNER JOIN Client.Account a ON a.TenantId=o.TenantId AND a.AccountId=o.AccountId
WHERE o.TenantId=@TenantId AND o.AccountId=@AccountId AND o.IsDeleted=0 ORDER BY o.CreatedDateUtc DESC;
SELECT ClaimId,TenantId,PolicyId,AccountId,ClaimNumber,PolicyNumber,AccountName,Lob,Carrier,Status,LossType,PrimaryClaimant,DateOfLoss,DateReported,ClosedDate,CASE WHEN Status=N'Closed' THEN 0 ELSE DATEDIFF(day,DateOfLoss,CONVERT(date,SYSUTCDATETIME())) END DaysOpen,TotalIncurred,TotalReserves,TotalPaid,AssignedHandler,IsLitigation,HasSubrogation,IsCatastrophe,IsDisputed,FollowUpReason,Priority,FollowUpDueDate,IsSnoozed,CatCode,LossLocation,StateOfLoss,LossDescription,CauseOfLoss,CarrierClaimNumber,ReportedBy,CreatedDateUtc,CreatedByUserId,ModifiedDateUtc,ModifiedByUserId,IsDeleted
FROM Claims.Claim WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 ORDER BY DateReported DESC;

SELECT
 (SELECT COUNT(1) FROM Client.AccountNamedInsured WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0) NamedInsuredCount,
 (SELECT COUNT(1) FROM Client.AccountLocation WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0) LocationCount,
 (SELECT COUNT(1) FROM Client.AccountVehicle WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND IsActive=1) VehicleCount,
 (SELECT COUNT(1) FROM Client.AccountDriver WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND IsExcluded=0) DriverCount,
 (SELECT COUNT(1) FROM Client.AccountProperty WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND IsActive=1) PropertyCount,
 (SELECT COUNT(1) FROM Client.AccountScheduleItem WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND IsActive=1) ScheduleItemCount,
 (SELECT COALESCE(SUM(ScheduledValue),0) FROM Client.AccountScheduleItem WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND IsActive=1) TotalScheduledValue,
 (SELECT COALESCE(SUM(COALESCE(BuildingValue,0)+COALESCE(ContentsValue,0)+COALESCE(BusinessIncomeValue,0)),0) FROM Client.AccountProperty WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND IsActive=1) TotalPropertyValue,
 (SELECT COUNT(1) FROM OPS.TaskItem WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND StatusCode NOT IN (N'Completed',N'Cancelled',N'Closed')) OpenTaskCount,
 (SELECT COUNT(1) FROM Submissions.Submission WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND Status NOT IN (N'Bound',N'Lost',N'Cancelled',N'Closed')) ActiveSubmissionCount,
 (SELECT COUNT(1) FROM Claims.Claim WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0 AND Status NOT IN (N'Closed',N'Denied')) OpenClaimCount,
 (SELECT COUNT(1) FROM DMS.Document WHERE TenantId=@TenantId AND EntityName=N'Account' AND EntityId=@AccountId AND IsDeleted=0) DocumentCount;
";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, AccountId = accountId }, cancellationToken: cancellationToken));
        var account = await multi.ReadSingleOrDefaultAsync<AccountDto>();
        if (account is null) return null;
        var result = new Account360Dto
        {
            Account = account,
            Contacts = (await multi.ReadAsync<ContactDto>()).AsList(),
            Stakeholders = (await multi.ReadAsync<AccountStakeholderDto>()).AsList(),
            CommunicationPreferences = (await multi.ReadAsync<AccountCommunicationPreferenceDto>()).AsList(),
            ServiceAssignments = (await multi.ReadAsync<AccountServiceAssignmentDto>()).AsList(),
            NamedInsureds = (await multi.ReadAsync<AccountNamedInsuredDto>()).AsList(),
            Locations = (await multi.ReadAsync<AccountLocationDto>()).AsList(),
            Vehicles = (await multi.ReadAsync<AccountVehicleDto>()).AsList(),
            Drivers = (await multi.ReadAsync<AccountDriverDto>()).AsList(),
            Properties = (await multi.ReadAsync<AccountPropertyDto>()).AsList(),
            ScheduleItems = (await multi.ReadAsync<AccountScheduleItemDto>()).AsList(),
            Activities = (await multi.ReadAsync<Account360ActivityDto>()).AsList(),
            Relationships = (await multi.ReadAsync<Account360RelationshipDto>()).AsList(),
            ReferenceOptions = (await multi.ReadAsync<AccountReferenceOptionDto>()).AsList(),
            Notes = (await multi.ReadAsync<AccountNoteDto>()).AsList(),
            Tasks = (await multi.ReadAsync<TaskItemDto>()).AsList(),
            Documents = (await multi.ReadAsync<DocumentDto>()).AsList(),
            Submissions = (await multi.ReadAsync<SubmissionDto>()).AsList(),
            Opportunities = (await multi.ReadAsync<OpportunityDto>()).AsList(),
            Claims = (await multi.ReadAsync<ClaimDto>()).AsList(),
            Metrics = await multi.ReadSingleAsync<Account360MetricsDto>()
        };
        result.Timeline = result.Activities.Select(activity => new Account360TimelineItemDto { EventTypeCode=activity.ActivityTypeCode,Title=activity.Title,Description=activity.Description,RelatedEntityType=activity.RelatedEntityType,RelatedEntityId=activity.RelatedEntityId,EventDateUtc=activity.OccurredDateUtc }).ToList();
        return result;
    }

    public async Task ReplaceServiceAssignmentsAsync(ReplaceAccountServiceAssignmentsRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
SET XACT_ABORT ON;
BEGIN TRAN;
IF NOT EXISTS(SELECT 1 FROM Client.Account WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0)
    THROW 51040,N'Account was not found in the authenticated tenant.',1;
IF EXISTS
(
    SELECT selected.UserId
    FROM (VALUES(@AccountManagerUserId),(@ProducerUserId),(@CsrUserId)) selected(UserId)
    WHERE selected.UserId IS NOT NULL
      AND NOT EXISTS(SELECT 1 FROM IAM.[User] appUser WHERE appUser.TenantId=@TenantId AND appUser.UserId=selected.UserId AND appUser.IsActive=1 AND appUser.IsDeleted=0)
)
    THROW 51041,N'Every service assignee must be an active user in the authenticated tenant.',1;

UPDATE assignment
SET IsDeleted=1,ExpirationDate=COALESCE(ExpirationDate,CONVERT(date,SYSUTCDATETIME())),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ModifiedByUserId
FROM Client.AccountServiceAssignment assignment
WHERE assignment.TenantId=@TenantId AND assignment.AccountId=@AccountId AND assignment.IsDeleted=0
  AND UPPER(REPLACE(REPLACE(REPLACE(assignment.AssignmentRoleCode,N'_',N''),N'-',N''),N' ',N'')) IN(N'ACCOUNTMANAGER',N'PRODUCER',N'CSR');

INSERT Client.AccountServiceAssignment(AccountServiceAssignmentId,TenantId,AccountId,UserId,AssignmentRoleCode,IsPrimary,EffectiveDate,ExpirationDate,Notes,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@AccountId,selected.UserId,selected.RoleCode,1,CONVERT(date,SYSUTCDATETIME()),NULL,N'Primary account service assignment',SYSUTCDATETIME(),@ModifiedByUserId,0
FROM (VALUES(N'ACCOUNT_MANAGER',@AccountManagerUserId),(N'PRODUCER',@ProducerUserId),(N'CSR',@CsrUserId)) selected(RoleCode,UserId)
WHERE selected.UserId IS NOT NULL;

INSERT Client.AccountActivity(ActivityId,TenantId,AccountId,ActivityType,[Subject],Notes,OccurredAtUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(NEWID(),@TenantId,@AccountId,N'ServiceAssignmentChanged',N'Service assignments updated',N'Primary Account Manager, Producer, and CSR assignments were updated.',SYSUTCDATETIME(),SYSUTCDATETIME(),@ModifiedByUserId,0);
COMMIT;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public Task<Guid> UpsertNamedInsuredAsync(UpsertAccountNamedInsuredRequest request, CancellationToken cancellationToken = default) => UpsertAsync("AccountNamedInsured", "AccountNamedInsuredId", request.AccountNamedInsuredId, request.TenantId, request.AccountId, request.UserId, request, cancellationToken);
    public Task<Guid> UpsertLocationAsync(UpsertAccountLocationRequest request, CancellationToken cancellationToken = default) => UpsertAsync("AccountLocation", "AccountLocationId", request.AccountLocationId, request.TenantId, request.AccountId, request.UserId, request, cancellationToken);
    public Task<Guid> UpsertVehicleAsync(UpsertAccountVehicleRequest request, CancellationToken cancellationToken = default) => UpsertAsync("AccountVehicle", "AccountVehicleId", request.AccountVehicleId, request.TenantId, request.AccountId, request.UserId, request, cancellationToken);
    public Task<Guid> UpsertDriverAsync(UpsertAccountDriverRequest request, CancellationToken cancellationToken = default) => UpsertAsync("AccountDriver", "AccountDriverId", request.AccountDriverId, request.TenantId, request.AccountId, request.UserId, request, cancellationToken);
    public Task<Guid> UpsertPropertyAsync(UpsertAccountPropertyRequest request, CancellationToken cancellationToken = default) => UpsertAsync("AccountProperty", "AccountPropertyId", request.AccountPropertyId, request.TenantId, request.AccountId, request.UserId, request, cancellationToken);
    public Task<Guid> UpsertScheduleItemAsync(UpsertAccountScheduleItemRequest request, CancellationToken cancellationToken = default) => UpsertAsync("AccountScheduleItem", "AccountScheduleItemId", request.AccountScheduleItemId, request.TenantId, request.AccountId, request.UserId, request, cancellationToken);

    private async Task<Guid> UpsertAsync(string table, string key, Guid? requestedId, Guid tenantId, Guid accountId, Guid? userId, object values, CancellationToken cancellationToken)
    {
        var id = requestedId ?? Guid.NewGuid();
        var writable = table switch
        {
            "AccountNamedInsured" => "ContactId,InsuredTypeCode,LegalName,DbaName,TaxIdentifier,RelationshipCode,IsPrimary,EffectiveDate,ExpirationDate,Notes",
            "AccountLocation" => "LocationNumber,LocationTypeCode,LocationName,AddressLine1,AddressLine2,City,StateCode,PostalCode,CountryCode,County,IsPrimary,IsMailingAddress,Latitude,Longitude,OccupancyCode,AnnualRevenue,EmployeeCount,Notes",
            "AccountVehicle" => "AccountLocationId,VehicleNumber,Vin,ModelYear,Make,Model,VehicleTypeCode,UseTypeCode,GaragingStateCode,GaragingPostalCode,RadiusMiles,AnnualMileage,CostNew,StatedValue,IsActive,Notes",
            "AccountDriver" => "ContactId,DriverNumber,FirstName,LastName,DateOfBirth,LicenseNumber,LicenseStateCode,LicenseClassCode,LicenseExpirationDate,HireDate,YearsExperience,DriverStatusCode,IsExcluded,Notes",
            "AccountProperty" => "AccountLocationId,PropertyNumber,PropertyTypeCode,ConstructionTypeCode,OccupancyCode,YearBuilt,SquareFeet,NumberOfStories,BuildingValue,ContentsValue,BusinessIncomeValue,ProtectionClassCode,RoofTypeCode,RoofYear,SprinkleredPercentage,IsActive,Notes",
            "AccountScheduleItem" => "AccountLocationId,ScheduleTypeCode,ItemNumber,ItemDescription,Manufacturer,Model,SerialNumber,AcquisitionDate,AppraisalDate,ScheduledValue,DeductibleAmount,IsActive,Notes",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        var columns = writable.Split(',');
        var insertColumns = string.Join(',', columns);
        var insertValues = string.Join(',', columns.Select(column => "@" + column));
        var updates = string.Join(',', columns.Select(column => $"{column}=@{column}"));
        var sql = $@"IF EXISTS (SELECT 1 FROM Client.Account WHERE TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0)
BEGIN
 IF EXISTS (SELECT 1 FROM Client.{table} WHERE {key}=@Id AND TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0)
  UPDATE Client.{table} SET {updates},ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE {key}=@Id AND TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0;
 ELSE
  INSERT INTO Client.{table} ({key},TenantId,AccountId,{insertColumns},CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES (@Id,@TenantId,@AccountId,{insertValues},SYSUTCDATETIME(),@UserId,0);
END
ELSE THROW 51000,'Account not found for tenant.',1;";
        var parameters = new DynamicParameters(values);
        parameters.Add("Id", id); parameters.Add("TenantId", tenantId); parameters.Add("AccountId", accountId); parameters.Add("UserId", userId);
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return id;
    }

    public async Task DeleteAccount360ItemAsync(Guid tenantId, Guid accountId, string entityType, Guid entityId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var (table,key) = entityType switch { "NamedInsured" => ("AccountNamedInsured","AccountNamedInsuredId"), "Location" => ("AccountLocation","AccountLocationId"), "Vehicle" => ("AccountVehicle","AccountVehicleId"), "Driver" => ("AccountDriver","AccountDriverId"), "Property" => ("AccountProperty","AccountPropertyId"), "ScheduleItem" => ("AccountScheduleItem","AccountScheduleItemId"), _ => throw new ArgumentOutOfRangeException(nameof(entityType)) };
        var sql = $"UPDATE Client.{table} SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE {key}=@EntityId AND TenantId=@TenantId AND AccountId=@AccountId AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId=tenantId,AccountId=accountId,EntityId=entityId,UserId=userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AccountDto>> FindMatchCandidatesAsync(AccountMatchCriteria criteria, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 50 AccountId, TenantId, AccountNumber, AccountName, AccountTypeCode,
       MainEmail, MainPhone, StatusCode, SegmentCode, OwnerUserId,
       ParentAccountId, LifecycleStageCode, Industry, Website, AnnualRevenue,
       CreatedDateUtc, ModifiedDateUtc
FROM Client.Account
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (
        (@BusinessName IS NOT NULL AND @BusinessName <> '' AND AccountName LIKE '%' + @BusinessName + '%')
     OR (@Email IS NOT NULL AND @Email <> '' AND MainEmail = @Email)
     OR (@Phone IS NOT NULL AND @Phone <> '' AND MainPhone = @Phone)
  )
ORDER BY AccountName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var results = await cn.QueryAsync<AccountDto>(new CommandDefinition(sql, new
        {
            criteria.TenantId,
            criteria.BusinessName,
            criteria.Email,
            criteria.Phone
        }, cancellationToken: cancellationToken));
        return results.AsList();
    }
}
