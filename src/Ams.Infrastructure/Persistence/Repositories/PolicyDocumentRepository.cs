using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Compliance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PolicyDocumentRepository : IPolicyDocumentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public PolicyDocumentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = @"
        p.PolicyDocumentId, p.TenantId, p.PolicyCode, p.PolicyTitle, p.PolicyTypeCode, p.Version,
        p.EffectiveDateUtc, p.IsActive, p.StatusCode, p.Description, p.Content,
        p.OwnedByUserId, u.FullName AS OwnedByFullName,
        p.ParentPolicyDocumentId, p.PublishedDateUtc, p.RetiredDateUtc,
        (SELECT COUNT(1) FROM Compliance.PolicyAcknowledgement a WHERE a.PolicyDocumentId = p.PolicyDocumentId) AS AcknowledgementCount,
        p.CreatedDateUtc, p.ModifiedDateUtc";

    public async Task<PolicyDocumentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT " + SelectColumns + @"
FROM Compliance.PolicyDocument p
LEFT JOIN IAM.[User] u ON u.UserId = p.OwnedByUserId
WHERE p.PolicyDocumentId = @Id AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        return await cn.QuerySingleOrDefaultAsync<PolicyDocumentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<PolicyDocumentDto>> SearchAsync(Guid? tenantId, string? searchTerm, string? typeCode, string? statusCode, bool? isActive, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT " + SelectColumns + @"
    FROM Compliance.PolicyDocument p
    LEFT JOIN IAM.[User] u ON u.UserId = p.OwnedByUserId
    WHERE p.IsDeleted = 0
      AND (@TenantId IS NULL OR p.TenantId = @TenantId)
      AND (@TypeCode IS NULL OR @TypeCode = '' OR p.PolicyTypeCode = @TypeCode)
      AND (@StatusCode IS NULL OR @StatusCode = '' OR p.StatusCode = @StatusCode)
      AND (@IsActive IS NULL OR p.IsActive = @IsActive)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR p.PolicyCode LIKE '%' + @SearchTerm + '%'
           OR p.PolicyTitle LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY PolicyCode
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Compliance.PolicyDocument p
WHERE p.IsDeleted = 0
  AND (@TenantId IS NULL OR p.TenantId = @TenantId)
  AND (@TypeCode IS NULL OR @TypeCode = '' OR p.PolicyTypeCode = @TypeCode)
  AND (@StatusCode IS NULL OR @StatusCode = '' OR p.StatusCode = @StatusCode)
  AND (@IsActive IS NULL OR p.IsActive = @IsActive)
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR p.PolicyCode LIKE '%' + @SearchTerm + '%'
       OR p.PolicyTitle LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId   = tenantId,
            SearchTerm = searchTerm,
            TypeCode   = typeCode,
            StatusCode = statusCode,
            IsActive   = isActive,
            Offset     = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize   = pageSize
        }, cancellationToken: ct));
        var items = (await multi.ReadAsync<PolicyDocumentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PolicyDocumentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreatePolicyDocumentRequest request, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Compliance.PolicyDocument
    (PolicyDocumentId, TenantId, PolicyCode, PolicyTitle, PolicyTypeCode, Version,
     EffectiveDateUtc, IsActive, StatusCode, Description,
     OwnedByUserId, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES
    (@PolicyDocumentId, @TenantId, @PolicyCode, @PolicyTitle, @PolicyTypeCode, @Version,
     @EffectiveDateUtc, 1, 'Draft', @Description,
     @OwnedByUserId, @CreatedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyDocumentId  = id,
            request.TenantId,
            request.PolicyCode,
            request.PolicyTitle,
            request.PolicyTypeCode,
            request.Version,
            request.EffectiveDateUtc,
            request.Description,
            request.OwnedByUserId,
            request.CreatedByUserId
        }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdatePolicyDocumentRequest request, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Compliance.PolicyDocument
SET PolicyCode       = @PolicyCode,
    PolicyTitle      = @PolicyTitle,
    PolicyTypeCode   = @PolicyTypeCode,
    Version          = @Version,
    EffectiveDateUtc = @EffectiveDateUtc,
    Description      = @Description,
    Content          = @Content,
    OwnedByUserId    = @OwnedByUserId,
    ModifiedDateUtc  = GETUTCDATE()
WHERE PolicyDocumentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.PolicyCode,
            request.PolicyTitle,
            request.PolicyTypeCode,
            request.Version,
            request.EffectiveDateUtc,
            request.Description,
            request.Content,
            request.OwnedByUserId
        }, cancellationToken: ct));
    }

    public async Task<Guid> CreateVersionAsync(Guid id, VersionPolicyDocumentRequest request, CancellationToken ct = default)
    {
        var newId = Guid.NewGuid();
        const string sql = @"
INSERT INTO Compliance.PolicyDocument
    (PolicyDocumentId, TenantId, PolicyCode, PolicyTitle, PolicyTypeCode, Version,
     EffectiveDateUtc, IsActive, StatusCode, Description,
     OwnedByUserId, ParentPolicyDocumentId, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT
    @NewId, TenantId, PolicyCode, PolicyTitle, PolicyTypeCode, @NewVersion,
    @EffectiveDateUtc, 1, 'Draft', COALESCE(@Description, Description),
    OwnedByUserId, @ParentId, @CreatedByUserId, GETUTCDATE(), 0
FROM Compliance.PolicyDocument
WHERE PolicyDocumentId = @ParentId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            NewId            = newId,
            ParentId         = id,
            NewVersion       = request.NewVersion,
            EffectiveDateUtc = request.EffectiveDateUtc,
            Description      = request.Description,
            CreatedByUserId  = request.CreatedByUserId
        }, cancellationToken: ct));
        return newId;
    }

    public async Task PublishAsync(Guid id, Guid? publishedByUserId, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Compliance.PolicyDocument
SET StatusCode        = 'Published',
    IsActive          = 1,
    PublishedByUserId = @PublishedByUserId,
    PublishedDateUtc  = GETUTCDATE(),
    ModifiedDateUtc   = GETUTCDATE()
WHERE PolicyDocumentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, PublishedByUserId = publishedByUserId }, cancellationToken: ct));
    }

    public async Task RetireAsync(Guid id, Guid? retiredByUserId, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Compliance.PolicyDocument
SET StatusCode      = 'Retired',
    IsActive        = 0,
    RetiredByUserId = @RetiredByUserId,
    RetiredDateUtc  = GETUTCDATE(),
    ModifiedDateUtc = GETUTCDATE()
WHERE PolicyDocumentId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, RetiredByUserId = retiredByUserId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PolicyAcknowledgementDto>> GetAcknowledgementsAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT a.AcknowledgementId, a.PolicyDocumentId, a.UserId,
       u.FullName AS UserFullName, u.Email AS UserEmail,
       a.AcknowledgedDateUtc, a.Channel, a.IpAddress
FROM Compliance.PolicyAcknowledgement a
JOIN IAM.[User] u ON u.UserId = a.UserId
WHERE a.PolicyDocumentId = @Id
ORDER BY a.AcknowledgedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<PolicyAcknowledgementDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task<IReadOnlyList<PolicyDocumentDto>> GetVersionHistoryAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT " + SelectColumns + @"
FROM Compliance.PolicyDocument p
LEFT JOIN IAM.[User] u ON u.UserId = p.OwnedByUserId
WHERE p.IsDeleted = 0
  AND p.TenantId  = (SELECT TenantId  FROM Compliance.PolicyDocument WHERE PolicyDocumentId = @Id AND IsDeleted = 0)
  AND p.PolicyCode = (SELECT PolicyCode FROM Compliance.PolicyDocument WHERE PolicyDocumentId = @Id AND IsDeleted = 0)
ORDER BY p.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<PolicyDocumentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task<IReadOnlyList<PolicyAudienceDto>> GetAudienceAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = @"
SELECT au.AudienceId, au.PolicyDocumentId, au.TargetTypeCode, au.TargetId, au.TargetName,
       au.IsRequired, u.FullName AS AddedByFullName, au.AddedDateUtc
FROM Compliance.PolicyAudience au
LEFT JOIN IAM.[User] u ON u.UserId = au.AddedByUserId
WHERE au.PolicyDocumentId = @Id AND au.IsDeleted = 0
ORDER BY au.TargetTypeCode, au.TargetName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = await cn.QueryAsync<PolicyAudienceDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return items.AsList();
    }

    public async Task<Guid> AddAudienceMemberAsync(Guid id, AddAudienceMemberRequest request, CancellationToken ct = default)
    {
        var audienceId = Guid.NewGuid();
        const string sql = @"
INSERT INTO Compliance.PolicyAudience
    (AudienceId, PolicyDocumentId, TargetTypeCode, TargetId, TargetName,
     IsRequired, AddedByUserId, AddedDateUtc, IsDeleted)
VALUES
    (@AudienceId, @PolicyDocumentId, @TargetTypeCode, @TargetId, @TargetName,
     @IsRequired, @AddedByUserId, GETUTCDATE(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            AudienceId       = audienceId,
            PolicyDocumentId = id,
            request.TargetTypeCode,
            request.TargetId,
            request.TargetName,
            request.IsRequired,
            request.AddedByUserId
        }, cancellationToken: ct));
        return audienceId;
    }

    public async Task RemoveAudienceMemberAsync(Guid audienceId, CancellationToken ct = default)
    {
        const string sql = @"
UPDATE Compliance.PolicyAudience
SET IsDeleted = 1
WHERE AudienceId = @AudienceId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { AudienceId = audienceId }, cancellationToken: ct));
    }
}
