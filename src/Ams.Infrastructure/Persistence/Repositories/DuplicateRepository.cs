using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Duplicates;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DuplicateRepository : IDuplicateRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqlConnectionFactory _connectionFactory;

    public DuplicateRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PagedResult<DuplicateGroupDto>> SearchAsync(DuplicateSearchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT GroupId, TenantId, EntityType, MatchKey, MatchReasons, ConfidenceScore, StatusCode,
           PrimaryRecordId, PrimaryName, DetectedDateUtc, ResolvedDateUtc, ResolvedByUserId, ResolutionNotes
    FROM CRM.DuplicateGroup
    WHERE TenantId = @TenantId
      AND IsDeleted = 0
      AND (@EntityType IS NULL OR @EntityType = '' OR EntityType = @EntityType)
      AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
      AND (
          @ConfidenceBand IS NULL OR @ConfidenceBand = ''
          OR (@ConfidenceBand = 'High' AND ConfidenceScore >= 90)
          OR (@ConfidenceBand = 'Medium' AND ConfidenceScore >= 70 AND ConfidenceScore < 90)
          OR (@ConfidenceBand = 'Low' AND ConfidenceScore < 70)
      )
      AND (
          @SearchTerm IS NULL OR @SearchTerm = ''
          OR PrimaryName LIKE '%' + @SearchTerm + '%'
          OR MatchReasons LIKE '%' + @SearchTerm + '%'
          OR MatchKey LIKE '%' + @SearchTerm + '%'
      )
)
SELECT * FROM Paged
ORDER BY CASE WHEN StatusCode = 'Open' THEN 0 WHEN StatusCode = 'Under Review' THEN 1 ELSE 2 END,
         ConfidenceScore DESC, DetectedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(*)
FROM CRM.DuplicateGroup
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@EntityType IS NULL OR @EntityType = '' OR EntityType = @EntityType)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR StatusCode = @StatusCode)
  AND (
      @ConfidenceBand IS NULL OR @ConfidenceBand = ''
      OR (@ConfidenceBand = 'High' AND ConfidenceScore >= 90)
      OR (@ConfidenceBand = 'Medium' AND ConfidenceScore >= 70 AND ConfidenceScore < 90)
      OR (@ConfidenceBand = 'Low' AND ConfidenceScore < 70)
  )
  AND (
      @SearchTerm IS NULL OR @SearchTerm = ''
      OR PrimaryName LIKE '%' + @SearchTerm + '%'
      OR MatchReasons LIKE '%' + @SearchTerm + '%'
      OR MatchKey LIKE '%' + @SearchTerm + '%'
  );";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.EntityType,
            request.StatusCode,
            request.ConfidenceBand,
            request.SearchTerm,
            Offset = (Math.Max(request.PageNumber, 1) - 1) * Math.Max(request.PageSize, 1),
            PageSize = Math.Max(request.PageSize, 1)
        }, cancellationToken: cancellationToken));

        var groups = (await multi.ReadAsync<DuplicateGroupDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        if (groups.Count > 0)
        {
            const string recordsSql = @"
SELECT DuplicateRecordId, GroupId, RecordId, RecordName, IsPrimary, SourceSystem, CreatedDateUtc, FieldValuesJson
FROM CRM.DuplicateRecord
WHERE GroupId IN @GroupIds AND IsDeleted = 0
ORDER BY IsPrimary DESC, RecordName;";

            var records = (await cn.QueryAsync<DuplicateRecordRow>(new CommandDefinition(recordsSql, new { GroupIds = groups.Select(g => g.GroupId).ToArray() }, cancellationToken: cancellationToken))).AsList();
            foreach (var group in groups)
            {
                group.Records = records.Where(r => r.GroupId == group.GroupId).Select(ToDto).ToList();
            }
        }

        return new PagedResult<DuplicateGroupDto>
        {
            Items = groups,
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public async Task<int> ScanAsync(DuplicateScanRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'dbo.AMS_DigitsOnly', N'FN') IS NULL
BEGIN
    EXEC(N'CREATE FUNCTION dbo.AMS_DigitsOnly(@value NVARCHAR(4000)) RETURNS NVARCHAR(4000) AS BEGIN RETURN @value END');
END

DECLARE @Inserted TABLE (GroupId UNIQUEIDENTIFIER);

;WITH AccountCandidates AS (
    SELECT TenantId,
           MatchType,
           MatchValue,
           ConfidenceScore,
           MatchReasons,
           COUNT(1) AS RecordCount,
           MIN(CreatedDateUtc) AS FirstCreatedDateUtc
    FROM (
        SELECT TenantId, 'Email' AS MatchType, LOWER(LTRIM(RTRIM(MainEmail))) AS MatchValue, 96 AS ConfidenceScore, 'Same account email' AS MatchReasons, CreatedDateUtc
        FROM Client.Account
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND NULLIF(LTRIM(RTRIM(MainEmail)), '') IS NOT NULL
        UNION ALL
        SELECT TenantId, 'Phone', dbo.AMS_DigitsOnly(MainPhone), 90, 'Same account phone', CreatedDateUtc
        FROM Client.Account
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND NULLIF(LTRIM(RTRIM(MainPhone)), '') IS NOT NULL
        UNION ALL
        SELECT TenantId, 'Name', LOWER(LTRIM(RTRIM(AccountName))), 82, 'Similar account name', CreatedDateUtc
        FROM Client.Account
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND NULLIF(LTRIM(RTRIM(AccountName)), '') IS NOT NULL
    ) x
    WHERE NULLIF(MatchValue, '') IS NOT NULL
    GROUP BY TenantId, MatchType, MatchValue, ConfidenceScore, MatchReasons
    HAVING COUNT(1) > 1
), NewAccountGroups AS (
    SELECT NEWID() AS GroupId, TenantId, 'Account' AS EntityType,
           CONCAT('Account:', MatchType, ':', MatchValue) AS MatchKey,
           MatchReasons, ConfidenceScore, FirstCreatedDateUtc
    FROM AccountCandidates c
    WHERE NOT EXISTS (
        SELECT 1 FROM CRM.DuplicateGroup g
        WHERE g.TenantId = c.TenantId AND g.EntityType = 'Account'
          AND g.MatchKey = CONCAT('Account:', c.MatchType, ':', c.MatchValue)
          AND g.IsDeleted = 0 AND g.StatusCode IN ('Open', 'Under Review')
    )
)
INSERT INTO CRM.DuplicateGroup (GroupId, TenantId, EntityType, MatchKey, MatchReasons, ConfidenceScore, StatusCode, PrimaryRecordId, PrimaryName, DetectedDateUtc, CreatedByUserId, IsDeleted)
OUTPUT INSERTED.GroupId INTO @Inserted
SELECT g.GroupId, g.TenantId, g.EntityType, g.MatchKey, g.MatchReasons, g.ConfidenceScore, 'Open', p.AccountId, p.AccountName, SYSUTCDATETIME(), @ScannedByUserId, 0
FROM NewAccountGroups g
CROSS APPLY (
    SELECT TOP 1 a.AccountId, a.AccountName
    FROM Client.Account a
    WHERE a.TenantId = g.TenantId AND a.IsDeleted = 0
      AND (
        (g.MatchKey LIKE 'Account:Email:%' AND LOWER(LTRIM(RTRIM(a.MainEmail))) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Account:Email:')))
        OR (g.MatchKey LIKE 'Account:Phone:%' AND dbo.AMS_DigitsOnly(a.MainPhone) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Account:Phone:')))
        OR (g.MatchKey LIKE 'Account:Name:%' AND LOWER(LTRIM(RTRIM(a.AccountName))) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Account:Name:')))
      )
    ORDER BY a.CreatedDateUtc
) p;

INSERT INTO CRM.DuplicateRecord (DuplicateRecordId, GroupId, RecordId, RecordName, IsPrimary, SourceSystem, CreatedDateUtc, FieldValuesJson, IsDeleted)
SELECT NEWID(), g.GroupId, a.AccountId, a.AccountName, CASE WHEN a.AccountId = g.PrimaryRecordId THEN 1 ELSE 0 END, 'CRM', a.CreatedDateUtc,
       CONCAT('{""Account Name"":""', STRING_ESCAPE(COALESCE(a.AccountName, ''), 'json'),
              '"",""Email"":""', STRING_ESCAPE(COALESCE(a.MainEmail, ''), 'json'),
              '"",""Phone"":""', STRING_ESCAPE(COALESCE(a.MainPhone, ''), 'json'),
              '"",""Status"":""', STRING_ESCAPE(COALESCE(a.StatusCode, ''), 'json'),
              '"",""Segment"":""', STRING_ESCAPE(COALESCE(a.SegmentCode, ''), 'json'), '""}') , 0
FROM CRM.DuplicateGroup g
JOIN @Inserted i ON i.GroupId = g.GroupId
JOIN Client.Account a ON a.TenantId = g.TenantId AND a.IsDeleted = 0
WHERE g.EntityType = 'Account'
  AND (
    (g.MatchKey LIKE 'Account:Email:%' AND LOWER(LTRIM(RTRIM(a.MainEmail))) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Account:Email:')))
    OR (g.MatchKey LIKE 'Account:Phone:%' AND dbo.AMS_DigitsOnly(a.MainPhone) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Account:Phone:')))
    OR (g.MatchKey LIKE 'Account:Name:%' AND LOWER(LTRIM(RTRIM(a.AccountName))) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Account:Name:')))
  );

;WITH ContactCandidates AS (
    SELECT TenantId,
           MatchType,
           MatchValue,
           ConfidenceScore,
           MatchReasons,
           COUNT(1) AS RecordCount,
           MIN(CreatedDateUtc) AS FirstCreatedDateUtc
    FROM (
        SELECT TenantId, 'Email' AS MatchType, LOWER(LTRIM(RTRIM(Email))) AS MatchValue, 97 AS ConfidenceScore, 'Same contact email' AS MatchReasons, CreatedDateUtc
        FROM Client.Contact
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND NULLIF(LTRIM(RTRIM(Email)), '') IS NOT NULL
        UNION ALL
        SELECT TenantId, 'Phone', dbo.AMS_DigitsOnly(Phone), 90, 'Same contact phone', CreatedDateUtc
        FROM Client.Contact
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND NULLIF(LTRIM(RTRIM(Phone)), '') IS NOT NULL
        UNION ALL
        SELECT TenantId, 'NameAccount', LOWER(CONCAT(LTRIM(RTRIM(FirstName)), '|', LTRIM(RTRIM(LastName)), '|', CONVERT(NVARCHAR(36), AccountId))), 84, 'Same contact name and account', CreatedDateUtc
        FROM Client.Contact
        WHERE TenantId = @TenantId AND IsDeleted = 0 AND NULLIF(LTRIM(RTRIM(FirstName)), '') IS NOT NULL AND NULLIF(LTRIM(RTRIM(LastName)), '') IS NOT NULL
    ) x
    WHERE NULLIF(MatchValue, '') IS NOT NULL
    GROUP BY TenantId, MatchType, MatchValue, ConfidenceScore, MatchReasons
    HAVING COUNT(1) > 1
), NewContactGroups AS (
    SELECT NEWID() AS GroupId, TenantId, 'Contact' AS EntityType,
           CONCAT('Contact:', MatchType, ':', MatchValue) AS MatchKey,
           MatchReasons, ConfidenceScore, FirstCreatedDateUtc
    FROM ContactCandidates c
    WHERE NOT EXISTS (
        SELECT 1 FROM CRM.DuplicateGroup g
        WHERE g.TenantId = c.TenantId AND g.EntityType = 'Contact'
          AND g.MatchKey = CONCAT('Contact:', c.MatchType, ':', c.MatchValue)
          AND g.IsDeleted = 0 AND g.StatusCode IN ('Open', 'Under Review')
    )
)
INSERT INTO CRM.DuplicateGroup (GroupId, TenantId, EntityType, MatchKey, MatchReasons, ConfidenceScore, StatusCode, PrimaryRecordId, PrimaryName, DetectedDateUtc, CreatedByUserId, IsDeleted)
OUTPUT INSERTED.GroupId INTO @Inserted
SELECT g.GroupId, g.TenantId, g.EntityType, g.MatchKey, g.MatchReasons, g.ConfidenceScore, 'Open', p.ContactId, p.ContactName, SYSUTCDATETIME(), @ScannedByUserId, 0
FROM NewContactGroups g
CROSS APPLY (
    SELECT TOP 1 c.ContactId, CONCAT(c.FirstName, ' ', c.LastName) AS ContactName
    FROM Client.Contact c
    WHERE c.TenantId = g.TenantId AND c.IsDeleted = 0
      AND (
        (g.MatchKey LIKE 'Contact:Email:%' AND LOWER(LTRIM(RTRIM(c.Email))) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Contact:Email:')))
        OR (g.MatchKey LIKE 'Contact:Phone:%' AND dbo.AMS_DigitsOnly(c.Phone) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Contact:Phone:')))
        OR (g.MatchKey LIKE 'Contact:NameAccount:%' AND LOWER(CONCAT(LTRIM(RTRIM(c.FirstName)), '|', LTRIM(RTRIM(c.LastName)), '|', CONVERT(NVARCHAR(36), c.AccountId))) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Contact:NameAccount:')))
      )
    ORDER BY c.CreatedDateUtc
) p;

INSERT INTO CRM.DuplicateRecord (DuplicateRecordId, GroupId, RecordId, RecordName, IsPrimary, SourceSystem, CreatedDateUtc, FieldValuesJson, IsDeleted)
SELECT NEWID(), g.GroupId, c.ContactId, CONCAT(c.FirstName, ' ', c.LastName), CASE WHEN c.ContactId = g.PrimaryRecordId THEN 1 ELSE 0 END, 'CRM', c.CreatedDateUtc,
       CONCAT('{""First Name"":""', STRING_ESCAPE(COALESCE(c.FirstName, ''), 'json'),
              '"",""Last Name"":""', STRING_ESCAPE(COALESCE(c.LastName, ''), 'json'),
              '"",""Email"":""', STRING_ESCAPE(COALESCE(c.Email, ''), 'json'),
              '"",""Phone"":""', STRING_ESCAPE(COALESCE(c.Phone, ''), 'json'),
              '"",""Job Title"":""', STRING_ESCAPE(COALESCE(c.JobTitle, ''), 'json'), '""}') , 0
FROM CRM.DuplicateGroup g
JOIN @Inserted i ON i.GroupId = g.GroupId
JOIN Client.Contact c ON c.TenantId = g.TenantId AND c.IsDeleted = 0
WHERE g.EntityType = 'Contact'
  AND (
    (g.MatchKey LIKE 'Contact:Email:%' AND LOWER(LTRIM(RTRIM(c.Email))) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Contact:Email:')))
    OR (g.MatchKey LIKE 'Contact:Phone:%' AND dbo.AMS_DigitsOnly(c.Phone) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Contact:Phone:')))
    OR (g.MatchKey LIKE 'Contact:NameAccount:%' AND LOWER(CONCAT(LTRIM(RTRIM(c.FirstName)), '|', LTRIM(RTRIM(c.LastName)), '|', CONVERT(NVARCHAR(36), c.AccountId))) = RIGHT(g.MatchKey, LEN(g.MatchKey) - LEN('Contact:NameAccount:')))
  );

SELECT COUNT(1) FROM @Inserted;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { request.TenantId, request.ScannedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task SetPrimaryAsync(Guid groupId, DuplicateSetPrimaryRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.DuplicateRecord SET IsPrimary = CASE WHEN RecordId = @PrimaryRecordId THEN 1 ELSE 0 END WHERE GroupId = @GroupId AND IsDeleted = 0;
UPDATE CRM.DuplicateGroup
SET PrimaryRecordId = @PrimaryRecordId,
    PrimaryName = COALESCE((SELECT TOP 1 RecordName FROM CRM.DuplicateRecord WHERE GroupId = @GroupId AND RecordId = @PrimaryRecordId AND IsDeleted = 0), PrimaryName),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE GroupId = @GroupId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { GroupId = groupId, request.PrimaryRecordId, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }

    public Task MergeAsync(Guid groupId, DuplicateResolveRequest request, CancellationToken cancellationToken = default)
        => ResolveAsync(groupId, "Merged", request, cancellationToken);

    public Task DismissAsync(Guid groupId, DuplicateResolveRequest request, CancellationToken cancellationToken = default)
        => ResolveAsync(groupId, "Dismissed", request, cancellationToken);

    public async Task BulkMergeAsync(DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var id in request.GroupIds.Distinct())
        {
            await ResolveAsync(id, "Merged", new DuplicateResolveRequest { ResolvedByUserId = request.ResolvedByUserId, Notes = request.Notes }, cancellationToken);
        }
    }

    public async Task BulkDismissAsync(DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default)
    {
        foreach (var id in request.GroupIds.Distinct())
        {
            await ResolveAsync(id, "Dismissed", new DuplicateResolveRequest { ResolvedByUserId = request.ResolvedByUserId, Notes = request.Notes }, cancellationToken);
        }
    }

    private async Task ResolveAsync(Guid groupId, string statusCode, DuplicateResolveRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @EntityType NVARCHAR(40);
DECLARE @PrimaryRecordId UNIQUEIDENTIFIER;
SELECT @EntityType = EntityType, @PrimaryRecordId = PrimaryRecordId FROM CRM.DuplicateGroup WHERE GroupId = @GroupId AND IsDeleted = 0;

IF @EntityType = 'Account' AND @StatusCode = 'Merged'
BEGIN
    UPDATE a
    SET IsDeleted = 1,
        StatusCode = 'Inactive',
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ResolvedByUserId
    FROM Client.Account a
    INNER JOIN CRM.DuplicateRecord r ON r.RecordId = a.AccountId AND r.GroupId = @GroupId AND r.IsDeleted = 0
    WHERE a.AccountId <> @PrimaryRecordId AND a.IsDeleted = 0;
END

IF @EntityType = 'Contact' AND @StatusCode = 'Merged'
BEGIN
    UPDATE c
    SET IsDeleted = 1,
        StatusCode = 'Inactive'
    FROM Client.Contact c
    INNER JOIN CRM.DuplicateRecord r ON r.RecordId = c.ContactId AND r.GroupId = @GroupId AND r.IsDeleted = 0
    WHERE c.ContactId <> @PrimaryRecordId AND c.IsDeleted = 0;
END

UPDATE CRM.DuplicateGroup
SET StatusCode = @StatusCode,
    ResolvedDateUtc = SYSUTCDATETIME(),
    ResolvedByUserId = @ResolvedByUserId,
    ResolutionNotes = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ResolvedByUserId
WHERE GroupId = @GroupId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { GroupId = groupId, StatusCode = statusCode, request.ResolvedByUserId, request.Notes }, cancellationToken: cancellationToken));
    }

    private static DuplicateRecordDto ToDto(DuplicateRecordRow row)
    {
        var fields = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(row.FieldValuesJson))
        {
            fields = JsonSerializer.Deserialize<Dictionary<string, string>>(row.FieldValuesJson, JsonOptions) ?? [];
        }

        return new DuplicateRecordDto
        {
            DuplicateRecordId = row.DuplicateRecordId,
            GroupId = row.GroupId,
            RecordId = row.RecordId,
            RecordName = row.RecordName,
            IsPrimary = row.IsPrimary,
            SourceSystem = row.SourceSystem,
            CreatedDateUtc = row.CreatedDateUtc,
            FieldValues = fields
        };
    }

    private sealed class DuplicateRecordRow
    {
        public Guid DuplicateRecordId { get; set; }
        public Guid GroupId { get; set; }
        public Guid RecordId { get; set; }
        public string RecordName { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public string SourceSystem { get; set; } = string.Empty;
        public DateTime? CreatedDateUtc { get; set; }
        public string? FieldValuesJson { get; set; }
    }
}
