using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Claims;
using Dapper;

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

    public async Task<ClaimDetailDto?> GetDetailAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {ClaimColumns} FROM Claims.Claim WHERE ClaimId = @ClaimId AND IsDeleted = 0;
SELECT ClaimActivityId, ClaimId, ActivityType, Title, Category, Party, Notes, Amount, PriorAmount, ActivityDate, CreatedBy, IsPinned
FROM Claims.ClaimActivity WHERE ClaimId = @ClaimId AND IsDeleted = 0 ORDER BY ActivityDate DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { ClaimId = claimId }, cancellationToken: cancellationToken));
        var claim = await multi.ReadSingleOrDefaultAsync<ClaimDto>();
        if (claim is null) return null;
        return new ClaimDetailDto { Claim = claim, Activities = (await multi.ReadAsync<ClaimActivityDto>()).AsList() };
    }

    public async Task<Guid> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @Next int = ISNULL((SELECT MAX(TRY_CONVERT(int, RIGHT(ClaimNumber, 5))) FROM Claims.Claim WHERE TenantId = @TenantId AND ClaimNumber LIKE CONCAT('CLM-', YEAR(SYSUTCDATETIME()), '-%')), 0) + 1;
DECLARE @ClaimNumber nvarchar(50) = CONCAT('CLM-', YEAR(SYSUTCDATETIME()), '-', FORMAT(@Next, '00000'));
INSERT INTO Claims.Claim
(ClaimId, TenantId, PolicyId, AccountId, ClaimNumber, PolicyNumber, AccountName, Lob, Carrier, Status, LossType, PrimaryClaimant,
 DateOfLoss, DateReported, ClosedDate, TotalIncurred, TotalReserves, TotalPaid, AssignedHandler, IsLitigation, HasSubrogation,
 IsCatastrophe, IsDisputed, FollowUpReason, Priority, FollowUpDueDate, IsSnoozed, CatCode, LossLocation, StateOfLoss,
 LossDescription, CauseOfLoss, CarrierClaimNumber, ReportedBy, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
(@ClaimId, @TenantId, NEWID(), NEWID(), @ClaimNumber, @PolicyNumber, @AccountName, @Lob, @Carrier, @Status, @LossType, @PrimaryClaimant,
 @DateOfLoss, @DateReported, NULL, @TotalIncurred, @TotalReserves, @TotalPaid, @AssignedHandler, @IsLitigation, @HasSubrogation,
 @IsCatastrophe, @IsDisputed, 'Initial follow-up', @Priority, DATEADD(day, 7, @DateReported), 0, @CatCode, @LossLocation, @StateOfLoss,
 @LossDescription, @CauseOfLoss, NULL, NULL, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ClaimId = id, request.TenantId, request.PolicyNumber, request.AccountName, request.Lob, request.Carrier, request.Status, request.LossType, request.PrimaryClaimant, request.DateOfLoss, request.DateReported, request.TotalIncurred, request.TotalReserves, request.TotalPaid, request.AssignedHandler, request.IsLitigation, request.HasSubrogation, request.IsCatastrophe, request.IsDisputed, request.Priority, request.CatCode, request.LossLocation, request.StateOfLoss, request.LossDescription, request.CauseOfLoss, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateStatusAsync(Guid claimId, UpdateClaimStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE Claims.Claim SET Status = @Status, ClosedDate = CASE WHEN @Status = 'Closed' THEN CAST(SYSUTCDATETIME() AS date) WHEN @Status = 'Reopened' THEN NULL ELSE ClosedDate END, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE ClaimId = @ClaimId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ClaimId = claimId, request.Status, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task UpdateFollowUpAsync(Guid claimId, UpdateClaimFollowUpRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"UPDATE Claims.Claim SET FollowUpReason = COALESCE(@FollowUpReason, FollowUpReason), Priority = COALESCE(@Priority, Priority), FollowUpDueDate = COALESCE(@FollowUpDueDate, FollowUpDueDate), IsSnoozed = @IsSnoozed, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE ClaimId = @ClaimId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ClaimId = claimId, request.FollowUpReason, request.Priority, request.FollowUpDueDate, request.IsSnoozed, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddActivityAsync(CreateClaimActivityRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"INSERT INTO Claims.ClaimActivity (ClaimActivityId, TenantId, ClaimId, ActivityType, ActivityDescription, Title, Category, Party, Notes, Amount, PriorAmount, ActivityDate, CreatedBy, CreatedDateUtc, IsPinned, IsDeleted) VALUES (@ClaimActivityId, (SELECT TenantId FROM Claims.Claim WHERE ClaimId = @ClaimId), @ClaimId, @ActivityType, @Notes, @Title, @Category, @Party, @Notes, @Amount, @PriorAmount, @ActivityDate, @CreatedBy, SYSUTCDATETIME(), @IsPinned, 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ClaimActivityId = id, request.ClaimId, request.ActivityType, request.Title, request.Category, request.Party, request.Notes, request.Amount, request.PriorAmount, request.ActivityDate, request.CreatedBy, request.IsPinned }, cancellationToken: cancellationToken));
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
VALUES (NEWID(), @TenantId, @CatEventId, N'Campaign', @Body, @Subject, @Channel, @Template, @Body, @TargetCount, NULL, @SentAt, N'Tenant Admin', SYSUTCDATETIME(), 0, 0);
UPDATE Claims.CatAffectedInsured SET BlastSent = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE CatEventId = @CatEventId AND GeoTagged = 1 AND IsDeleted = 0;
SELECT @TargetCount;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { request.TenantId, request.CatEventId, request.Channel, request.Template, request.Subject, request.Body, request.SentAt }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateFastCatFnolAsync(FastCatFnolRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @CatCode nvarchar(80) = (SELECT TOP 1 CatCode FROM Claims.CatEvent WHERE CatEventId = @CatEventId AND TenantId = @TenantId AND IsDeleted = 0);
DECLARE @AccountId uniqueidentifier = COALESCE((SELECT TOP 1 AccountId FROM Claims.CatAffectedInsured WHERE CatEventId = @CatEventId AND PolicyNumber = @PolicyNumber AND IsDeleted = 0), NEWID());
DECLARE @Carrier nvarchar(120) = COALESCE((SELECT TOP 1 Carrier FROM Claims.Claim WHERE TenantId = @TenantId AND PolicyNumber = @PolicyNumber AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC), N'CAT Intake');
DECLARE @Lob nvarchar(80) = COALESCE((SELECT TOP 1 Lob FROM Claims.CatAffectedInsured WHERE CatEventId = @CatEventId AND PolicyNumber = @PolicyNumber AND IsDeleted = 0), N'Commercial Property');
DECLARE @Next int = ISNULL((SELECT MAX(TRY_CONVERT(int, RIGHT(ClaimNumber, 5))) FROM Claims.Claim WHERE TenantId = @TenantId AND ClaimNumber LIKE CONCAT('CLM-', YEAR(SYSUTCDATETIME()), '-%')), 0) + 1;
DECLARE @ClaimNumber nvarchar(50) = CONCAT('CLM-', YEAR(SYSUTCDATETIME()), '-', FORMAT(@Next, '00000'));

INSERT INTO Claims.Claim
(ClaimId, TenantId, PolicyId, AccountId, ClaimNumber, PolicyNumber, AccountName, Lob, Carrier, Status, LossType, PrimaryClaimant,
 DateOfLoss, DateReported, ClosedDate, TotalIncurred, TotalReserves, TotalPaid, AssignedHandler, IsLitigation, HasSubrogation,
 IsCatastrophe, IsDisputed, FollowUpReason, Priority, FollowUpDueDate, IsSnoozed, CatCode, LossLocation, StateOfLoss,
 LossDescription, CauseOfLoss, CarrierClaimNumber, ReportedBy, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
(@ClaimId, @TenantId, NEWID(), @AccountId, @ClaimNumber, @PolicyNumber, @InsuredName, @Lob, @Carrier, N'Open', @LossType, @InsuredName,
 CAST(SYSUTCDATETIME() AS date), CAST(SYSUTCDATETIME() AS date), NULL, 0, 0, 0, N'Sarah Kim', 0, 0,
 1, 0, N'CAT fast FNOL triage', N'High', DATEADD(day, 1, CAST(SYSUTCDATETIME() AS date)), 0, @CatCode, NULL, NULL,
 CONCAT(@Description, CASE WHEN @Description IS NULL OR @Description = N'' THEN N'' ELSE N' ' END, N'Contact phone: ', @Phone, N'. Estimated damage: ', @EstimatedRange), @LossType, NULL, N'Tenant Admin', SYSUTCDATETIME(), NULL, NULL, NULL, 0);

INSERT INTO Claims.ClaimActivity (ClaimActivityId, TenantId, ClaimId, ActivityType, ActivityDescription, Title, Category, Party, Notes, ActivityDate, CreatedBy, CreatedDateUtc, IsPinned, IsDeleted)
VALUES (NEWID(), @TenantId, @ClaimId, N'FNOL', @Description, N'Fast CAT FNOL received', @LossType, @InsuredName, CONCAT(N'Phone: ', @Phone, N'. Estimated damage: ', @EstimatedRange, N'. ', @Description), SYSUTCDATETIME(), N'Tenant Admin', SYSUTCDATETIME(), 1, 0);

UPDATE Claims.CatAffectedInsured SET FnolFiled = 1, ContactStatus = N'FNOL Filed', ModifiedDateUtc = SYSUTCDATETIME() WHERE CatEventId = @CatEventId AND PolicyNumber = @PolicyNumber AND IsDeleted = 0;
SELECT @ClaimId;";
        var claimId = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { ClaimId = claimId, request.TenantId, request.CatEventId, request.PolicyNumber, request.InsuredName, request.Phone, request.LossType, request.EstimatedRange, request.Description }, cancellationToken: cancellationToken));
    }
}
