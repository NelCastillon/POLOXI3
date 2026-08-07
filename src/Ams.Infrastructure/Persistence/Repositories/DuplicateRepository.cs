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
DECLARE @Inserted TABLE(GroupId UNIQUEIDENTIFIER,MatchExecutionId UNIQUEIDENTIFIER,PrimaryRecordId UNIQUEIDENTIFIER);
;WITH evidence AS
(
    SELECT execution.MatchExecutionId,execution.EntityTypeCode,execution.SourceEntityId,candidate.CandidateEntityId,candidate.DisplayName,candidate.OverallScore,
           STRING_AGG(reason.Explanation,N'; ') WITHIN GROUP(ORDER BY reason.WeightedScore DESC) MatchReasons,
           ROW_NUMBER() OVER(PARTITION BY execution.MatchExecutionId ORDER BY candidate.RankOrder) CandidateRank
    FROM Search.MatchExecution execution
    JOIN Search.MatchCandidate candidate ON candidate.MatchExecutionId=execution.MatchExecutionId AND candidate.IsDeleted=0
    JOIN Search.MatchReasonEvidence reason ON reason.MatchCandidateId=candidate.MatchCandidateId AND reason.IsDeleted=0
    WHERE execution.TenantId=@TenantId AND execution.StatusCode=N'COMPLETED' AND execution.IsDeleted=0
      AND execution.EntityTypeCode IN(N'Account',N'Contact',N'Lead') AND candidate.ConfidenceBandCode IN(N'EXACT',N'STRONG',N'POSSIBLE')
    GROUP BY execution.MatchExecutionId,execution.EntityTypeCode,execution.SourceEntityId,candidate.CandidateEntityId,candidate.DisplayName,candidate.OverallScore,candidate.RankOrder
), newGroups AS
(
    SELECT evidence.*,NEWID() GroupId,CONCAT(N'SearchMatch:',CONVERT(NVARCHAR(36),evidence.MatchExecutionId)) MatchKey
    FROM evidence WHERE CandidateRank=1 AND evidence.SourceEntityId IS NOT NULL AND evidence.SourceEntityId<>evidence.CandidateEntityId
      AND NOT EXISTS(SELECT 1 FROM CRM.DuplicateGroup existing WHERE existing.TenantId=@TenantId AND existing.MatchKey=CONCAT(N'SearchMatch:',CONVERT(NVARCHAR(36),evidence.MatchExecutionId)) AND existing.IsDeleted=0)
)
INSERT CRM.DuplicateGroup(GroupId,TenantId,EntityType,MatchKey,MatchReasons,ConfidenceScore,StatusCode,PrimaryRecordId,PrimaryName,DetectedDateUtc,CreatedByUserId,IsDeleted)
OUTPUT inserted.GroupId,newGroups.MatchExecutionId,newGroups.PrimaryRecordId INTO @Inserted
SELECT GroupId,@TenantId,EntityTypeCode,MatchKey,LEFT(MatchReasons,500),CONVERT(INT,ROUND(OverallScore,0)),N'Under Review',CandidateEntityId,DisplayName,SYSUTCDATETIME(),@ScannedByUserId,0 FROM newGroups;

INSERT CRM.DuplicateRecord(DuplicateRecordId,GroupId,RecordId,RecordName,IsPrimary,SourceSystem,CreatedDateUtc,FieldValuesJson,IsDeleted)
SELECT NEWID(),inserted.GroupId,projection.EntityId,projection.DisplayName,CASE WHEN projection.EntityId=inserted.PrimaryRecordId THEN 1 ELSE 0 END,N'SearchMatching',SYSUTCDATETIME(),
       JSON_QUERY((SELECT projection.EntityTypeCode,projection.SecondaryText,projection.NavigationRoute FOR JSON PATH,WITHOUT_ARRAY_WRAPPER)),0
FROM @Inserted inserted JOIN Search.MatchExecution execution ON execution.MatchExecutionId=inserted.MatchExecutionId
JOIN Search.EntityProjection projection ON projection.TenantId=@TenantId AND projection.EntityTypeCode=execution.EntityTypeCode AND projection.EntityId IN(execution.SourceEntityId,inserted.PrimaryRecordId) AND projection.IsDeleted=0;

SELECT COUNT(1) FROM @Inserted;
";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { request.TenantId, request.ScannedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DuplicateScanSource>> GetScanSourcesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT EntityId,EntityTypeCode,NormalizedFieldsJson
FROM Search.EntityProjection
WHERE TenantId=@TenantId AND EntityTypeCode IN(N'Account',N'Contact',N'Lead') AND IsActive=1 AND IsDeleted=0
ORDER BY EntityTypeCode,EntityId;
""";
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<DuplicateScanSourceRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return rows.Select(row => new DuplicateScanSource(row.EntityId, row.EntityTypeCode, JsonSerializer.Deserialize<Dictionary<string, string?>>(row.NormalizedFieldsJson, JsonOptions) ?? [])).ToList();
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

    private sealed record DuplicateScanSourceRow(Guid EntityId, string EntityTypeCode, string NormalizedFieldsJson);
}
