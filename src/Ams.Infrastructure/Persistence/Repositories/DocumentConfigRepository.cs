using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentConfig;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DocumentConfigRepository : IDocumentConfigRepository
{
    private readonly ISqlConnectionFactory _cf;
    public DocumentConfigRepository(ISqlConnectionFactory cf) => _cf = cf;

    private const string Cols = @"g.DocumentGroupId AS DocumentConfigItemId,
        g.TenantId,
        k.KindCode AS Kind,
        g.GroupCode AS Code,
        g.GroupName AS Name,
        c.CategoryName AS Category,
        g.Description,
        g.ConfigurationJson,
        CASE WHEN c.IsActive = 1 AND g.IsActive = 1 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsActive,
        g.SortOrder,
        g.CreatedDateUtc";

    public async Task<DocumentConfigItemDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var category = await cn.QuerySingleOrDefaultAsync<DocumentConfigItemDto>(new CommandDefinition(@"
SELECT dc.DocumentCategoryId AS DocumentConfigItemId,
       dc.TenantId,
       k.KindCode AS Kind,
       dc.CategoryCode AS Code,
       dc.CategoryName AS Name,
       CAST(NULL AS NVARCHAR(200)) AS Category,
       dc.Description,
       dc.ConfigurationJson,
       dc.IsActive,
       dc.SortOrder,
       dc.CreatedDateUtc
FROM DMS.DocumentCategory dc
INNER JOIN DMS.DocumentKind k ON k.DocumentKindId = dc.DocumentKindId AND k.IsDeleted = 0
WHERE dc.DocumentCategoryId=@Id AND dc.IsDeleted=0;", new { Id = id }, cancellationToken: ct));

        if (category is not null)
        {
            return category;
        }

        return await cn.QuerySingleOrDefaultAsync<DocumentConfigItemDto>(new CommandDefinition($@"
SELECT {Cols}
FROM DMS.DocumentGroup g
INNER JOIN DMS.DocumentKind k ON k.DocumentKindId = g.DocumentKindId AND k.IsDeleted = 0
LEFT JOIN DMS.DocumentCategory c ON c.DocumentCategoryId = g.CategoryId AND c.IsDeleted = 0
WHERE g.DocumentGroupId=@Id AND g.IsDeleted=0;", new { Id = id }, cancellationToken: ct));
    }

    public async Task<PagedResult<DocumentConfigItemDto>> SearchAsync(Guid tenantId, string? kind, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        if (string.Equals(kind, "DocumentCategory", StringComparison.OrdinalIgnoreCase))
        {
            const string categorySql = @"
SELECT dc.DocumentCategoryId AS DocumentConfigItemId,
       dc.TenantId,
       k.KindCode AS Kind,
       dc.CategoryCode AS Code,
       dc.CategoryName AS Name,
       CAST(NULL AS NVARCHAR(200)) AS Category,
       dc.Description,
       dc.ConfigurationJson,
       dc.IsActive,
       dc.SortOrder,
       dc.CreatedDateUtc
FROM DMS.DocumentCategory dc
INNER JOIN DMS.DocumentKind k ON k.DocumentKindId = dc.DocumentKindId AND k.IsDeleted = 0
WHERE dc.TenantId=@TenantId AND k.KindCode=@Kind AND dc.IsDeleted=0
  AND (@SearchTerm='' OR dc.CategoryName LIKE '%'+@SearchTerm+'%' OR dc.CategoryCode LIKE '%'+@SearchTerm+'%')
ORDER BY dc.SortOrder ASC, dc.CategoryName ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM DMS.DocumentCategory dc
INNER JOIN DMS.DocumentKind k ON k.DocumentKindId = dc.DocumentKindId AND k.IsDeleted = 0
WHERE dc.TenantId=@TenantId AND k.KindCode=@Kind AND dc.IsDeleted=0
  AND (@SearchTerm='' OR dc.CategoryName LIKE '%'+@SearchTerm+'%' OR dc.CategoryCode LIKE '%'+@SearchTerm+'%');";
            using var categoryCn = await _cf.CreateOpenConnectionAsync(ct);
            using var categoryMulti = await categoryCn.QueryMultipleAsync(new CommandDefinition(categorySql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm ?? string.Empty, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
            return new() { Items = (await categoryMulti.ReadAsync<DocumentConfigItemDto>()).AsList(), TotalCount = await categoryMulti.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
        }

        const string sql = @"
SELECT g.DocumentGroupId AS DocumentConfigItemId,
       g.TenantId,
       k.KindCode AS Kind,
       g.GroupCode AS Code,
       g.GroupName AS Name,
       c.CategoryName AS Category,
       g.Description,
       g.ConfigurationJson,
       CASE WHEN c.IsActive = 1 AND g.IsActive = 1 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsActive,
       g.SortOrder,
       g.CreatedDateUtc
FROM DMS.DocumentGroup g
INNER JOIN DMS.DocumentKind k ON k.DocumentKindId = g.DocumentKindId AND k.IsDeleted = 0
LEFT JOIN DMS.DocumentCategory c ON c.DocumentCategoryId = g.CategoryId AND c.IsDeleted = 0
WHERE g.TenantId=@TenantId AND (@Kind='' OR k.KindCode=@Kind) AND g.IsDeleted=0
  AND (@SearchTerm='' OR g.GroupName LIKE '%'+@SearchTerm+'%' OR g.GroupCode LIKE '%'+@SearchTerm+'%' OR c.CategoryName LIKE '%'+@SearchTerm+'%')
ORDER BY k.KindCode ASC, g.SortOrder ASC, g.GroupName ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM DMS.DocumentGroup g
INNER JOIN DMS.DocumentKind k ON k.DocumentKindId = g.DocumentKindId AND k.IsDeleted = 0
LEFT JOIN DMS.DocumentCategory c ON c.DocumentCategoryId = g.CategoryId AND c.IsDeleted = 0
WHERE g.TenantId=@TenantId AND (@Kind='' OR k.KindCode=@Kind) AND g.IsDeleted=0
  AND (@SearchTerm='' OR g.GroupName LIKE '%'+@SearchTerm+'%' OR g.GroupCode LIKE '%'+@SearchTerm+'%' OR c.CategoryName LIKE '%'+@SearchTerm+'%');";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind ?? string.Empty, SearchTerm = searchTerm ?? string.Empty, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        return new() { Items = (await multi.ReadAsync<DocumentConfigItemDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateDocumentConfigItemRequest r, CancellationToken ct = default)
    {
        if (string.Equals(r.Kind, "DocumentCategory", StringComparison.OrdinalIgnoreCase))
        {
            const string categorySql = @"
DECLARE @KindId UNIQUEIDENTIFIER;

SELECT @KindId = DocumentKindId
FROM DMS.DocumentKind
WHERE TenantId = @TenantId AND KindCode = @Kind AND IsDeleted = 0;

IF @KindId IS NULL
BEGIN
    SET @KindId = NEWID();
    INSERT INTO DMS.DocumentKind (DocumentKindId, TenantId, KindCode, KindName, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@KindId, @TenantId, @Kind, @Kind, 1, 0, GETUTCDATE(), 0);
END

INSERT INTO DMS.DocumentCategory (DocumentCategoryId, TenantId, DocumentKindId, CategoryCode, CategoryName, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @KindId, @Code, @Name, @Description, @ConfigurationJson, 1, @SortOrder, GETUTCDATE(), 0);";
            var categoryId = Guid.NewGuid();
            using var categoryCn = await _cf.CreateOpenConnectionAsync(ct);
            await categoryCn.ExecuteAsync(new CommandDefinition(categorySql, new { Id = categoryId, r.TenantId, r.Kind, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.SortOrder }, cancellationToken: ct));
            return categoryId;
        }

        const string sql = @"
DECLARE @KindId UNIQUEIDENTIFIER;
DECLARE @CategoryId UNIQUEIDENTIFIER;
DECLARE @CategoryName NVARCHAR(200) = COALESCE(NULLIF(LTRIM(RTRIM(@Category)), N''), N'Unassigned');
DECLARE @CategoryCode NVARCHAR(80) = UPPER(REPLACE(REPLACE(@CategoryName, N' ', N'_'), N'-', N'_'));

SELECT @KindId = DocumentKindId
FROM DMS.DocumentKind
WHERE TenantId = @TenantId AND KindCode = @Kind AND IsDeleted = 0;

IF @KindId IS NULL
BEGIN
    SET @KindId = NEWID();
    INSERT INTO DMS.DocumentKind (DocumentKindId, TenantId, KindCode, KindName, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@KindId, @TenantId, @Kind, @Kind, 1, 0, GETUTCDATE(), 0);
END

SELECT @CategoryId = DocumentCategoryId
FROM DMS.DocumentCategory
WHERE TenantId = @TenantId AND DocumentKindId = @KindId AND CategoryCode = @CategoryCode AND IsDeleted = 0;

IF @CategoryId IS NULL
BEGIN
    SET @CategoryId = NEWID();
    INSERT INTO DMS.DocumentCategory (DocumentCategoryId, TenantId, DocumentKindId, CategoryCode, CategoryName, SortOrder, IsActive, IsDeleted, CreatedDateUtc)
    VALUES (@CategoryId, @TenantId, @KindId, @CategoryCode, @CategoryName, @SortOrder, 1, 0, GETUTCDATE());
END

INSERT INTO DMS.DocumentGroup (DocumentGroupId, TenantId, DocumentKindId, CategoryId, GroupCode, GroupName, Description, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @KindId, @CategoryId, @Code, @Name, @Description, 1, @SortOrder, GETUTCDATE(), 0);

UPDATE DMS.DocumentGroup
SET ConfigurationJson = @ConfigurationJson
WHERE DocumentGroupId = @Id;

UPDATE DMS.DocumentCategory
SET ConfigurationJson = COALESCE(ConfigurationJson, @ConfigurationJson),
    ModifiedDateUtc = GETUTCDATE()
WHERE DocumentCategoryId = @CategoryId;";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.Kind, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateDocumentConfigItemRequest r, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var isDocumentCategory = await cn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM DMS.DocumentCategory WHERE DocumentCategoryId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct)) > 0;
        if (isDocumentCategory)
        {
            const string categorySql = @"
UPDATE DMS.DocumentCategory
SET CategoryCode = @Code,
    CategoryName = @Name,
    Description = @Description,
    ConfigurationJson = @ConfigurationJson,
    IsActive = @IsActive,
    SortOrder = @SortOrder,
    ModifiedDateUtc = GETUTCDATE()
WHERE DocumentCategoryId = @Id AND IsDeleted = 0;";
            await cn.ExecuteAsync(new CommandDefinition(categorySql, new { Id = id, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.IsActive, r.SortOrder }, cancellationToken: ct));
            return;
        }

        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER;
DECLARE @KindId UNIQUEIDENTIFIER;
DECLARE @CategoryId UNIQUEIDENTIFIER;
DECLARE @CategoryName NVARCHAR(200) = COALESCE(NULLIF(LTRIM(RTRIM(@Category)), N''), N'Unassigned');
DECLARE @CategoryCode NVARCHAR(80) = UPPER(REPLACE(REPLACE(@CategoryName, N' ', N'_'), N'-', N'_'));

SELECT @TenantId = TenantId, @KindId = DocumentKindId
FROM DMS.DocumentGroup
WHERE DocumentGroupId = @Id AND IsDeleted = 0;

IF @TenantId IS NOT NULL
BEGIN
    SELECT @CategoryId = DocumentCategoryId
    FROM DMS.DocumentCategory
    WHERE TenantId = @TenantId AND DocumentKindId = @KindId AND CategoryCode = @CategoryCode AND IsDeleted = 0;

    IF @CategoryId IS NULL
    BEGIN
        SET @CategoryId = NEWID();
        INSERT INTO DMS.DocumentCategory (DocumentCategoryId, TenantId, DocumentKindId, CategoryCode, CategoryName, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
        VALUES (@CategoryId, @TenantId, @KindId, @CategoryCode, @CategoryName, @IsActive, @SortOrder, GETUTCDATE(), 0);
    END
END

UPDATE DMS.DocumentGroup
SET CategoryId = @CategoryId,
    GroupCode = @Code,
    GroupName = @Name,
    Description = @Description,
    ConfigurationJson = @ConfigurationJson,
    IsActive = @IsActive,
    SortOrder = @SortOrder,
    ModifiedDateUtc = GETUTCDATE()
WHERE DocumentGroupId = @Id AND IsDeleted = 0;

UPDATE DMS.DocumentCategory
SET CategoryName = @CategoryName,
    ConfigurationJson = @ConfigurationJson,
    IsActive = @IsActive,
    ModifiedDateUtc = GETUTCDATE()
WHERE DocumentCategoryId = @CategoryId AND IsDeleted = 0;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task<PagedResult<DocumentConfigItemDto>> SearchGroupsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
    {
        const string sql = @"
SELECT g.DocumentGroupId AS DocumentConfigItemId,
       g.TenantId,
       k.KindCode AS Kind,
       g.GroupCode AS Code,
       g.GroupName AS Name,
       c.CategoryName AS Category,
       g.Description,
       g.ConfigurationJson,
       g.IsActive,
       g.SortOrder,
       g.CreatedDateUtc
FROM DMS.DocumentGroup g
INNER JOIN DMS.DocumentKind k ON k.DocumentKindId = g.DocumentKindId AND k.IsDeleted = 0
LEFT JOIN DMS.DocumentCategory c ON c.DocumentCategoryId = g.CategoryId AND c.IsDeleted = 0
WHERE g.TenantId=@TenantId AND k.KindCode=N'DocumentCategory' AND g.IsDeleted=0
  AND (@SearchTerm='' OR g.GroupName LIKE '%'+@SearchTerm+'%' OR g.GroupCode LIKE '%'+@SearchTerm+'%' OR c.CategoryName LIKE '%'+@SearchTerm+'%')
ORDER BY COALESCE(c.SortOrder, 9999), c.CategoryName, g.SortOrder, g.GroupName
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM DMS.DocumentGroup g
INNER JOIN DMS.DocumentKind k ON k.DocumentKindId = g.DocumentKindId AND k.IsDeleted = 0
LEFT JOIN DMS.DocumentCategory c ON c.DocumentCategoryId = g.CategoryId AND c.IsDeleted = 0
WHERE g.TenantId=@TenantId AND k.KindCode=N'DocumentCategory' AND g.IsDeleted=0
  AND (@SearchTerm='' OR g.GroupName LIKE '%'+@SearchTerm+'%' OR g.GroupCode LIKE '%'+@SearchTerm+'%' OR c.CategoryName LIKE '%'+@SearchTerm+'%');";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm ?? string.Empty, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: ct));
        return new() { Items = (await multi.ReadAsync<DocumentConfigItemDto>()).AsList(), TotalCount = await multi.ReadSingleAsync<int>(), PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateGroupAsync(CreateDocumentGroupRequest r, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @KindId UNIQUEIDENTIFIER;
DECLARE @CategoryId UNIQUEIDENTIFIER;
DECLARE @CategoryName NVARCHAR(200) = COALESCE(NULLIF(LTRIM(RTRIM(@Category)), N''), N'General');
DECLARE @CategoryCode NVARCHAR(80) = UPPER(REPLACE(REPLACE(@CategoryName, N' ', N'_'), N'-', N'_'));

SELECT @KindId = DocumentKindId
FROM DMS.DocumentKind
WHERE TenantId = @TenantId AND KindCode = N'DocumentCategory' AND IsDeleted = 0;

IF @KindId IS NULL
BEGIN
    SET @KindId = NEWID();
    INSERT INTO DMS.DocumentKind (DocumentKindId, TenantId, KindCode, KindName, Description, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@KindId, @TenantId, N'DocumentCategory', N'Document Categories', N'Enterprise document category and group taxonomy.', 1, 10, GETUTCDATE(), 0);
END

SELECT @CategoryId = DocumentCategoryId
FROM DMS.DocumentCategory
WHERE TenantId = @TenantId AND DocumentKindId = @KindId AND (CategoryName = @CategoryName OR CategoryCode = @CategoryCode) AND IsDeleted = 0;

IF @CategoryId IS NULL
BEGIN
    SET @CategoryId = NEWID();
    INSERT INTO DMS.DocumentCategory (DocumentCategoryId, TenantId, DocumentKindId, CategoryCode, CategoryName, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@CategoryId, @TenantId, @CategoryCode, @CategoryName, 1, @SortOrder, GETUTCDATE(), 0);
END

INSERT INTO DMS.DocumentGroup (DocumentGroupId, TenantId, DocumentKindId, CategoryId, GroupCode, GroupName, Description, ConfigurationJson, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @KindId, @CategoryId, @Code, @Name, @Description, @ConfigurationJson, 1, @SortOrder, GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.TenantId, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.SortOrder }, cancellationToken: ct));
        return id;
    }

    public async Task UpdateGroupAsync(Guid id, UpdateDocumentGroupRequest r, CancellationToken ct = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER;
DECLARE @KindId UNIQUEIDENTIFIER;
DECLARE @CategoryId UNIQUEIDENTIFIER;
DECLARE @CategoryName NVARCHAR(200) = COALESCE(NULLIF(LTRIM(RTRIM(@Category)), N''), N'General');
DECLARE @CategoryCode NVARCHAR(80) = UPPER(REPLACE(REPLACE(@CategoryName, N' ', N'_'), N'-', N'_'));

SELECT @TenantId = TenantId, @KindId = DocumentKindId
FROM DMS.DocumentGroup
WHERE DocumentGroupId = @Id AND IsDeleted = 0;

IF @TenantId IS NOT NULL
BEGIN
    SELECT @CategoryId = DocumentCategoryId
    FROM DMS.DocumentCategory
    WHERE TenantId = @TenantId AND DocumentKindId = @KindId AND (CategoryName = @CategoryName OR CategoryCode = @CategoryCode) AND IsDeleted = 0;

    IF @CategoryId IS NULL
    BEGIN
        SET @CategoryId = NEWID();
        INSERT INTO DMS.DocumentCategory (DocumentCategoryId, TenantId, DocumentKindId, CategoryCode, CategoryName, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
        VALUES (@CategoryId, @TenantId, @KindId, @CategoryCode, @CategoryName, 1, @SortOrder, GETUTCDATE(), 0);
    END
END

UPDATE DMS.DocumentGroup
SET CategoryId = @CategoryId,
    GroupCode = @Code,
    GroupName = @Name,
    Description = @Description,
    ConfigurationJson = @ConfigurationJson,
    IsActive = @IsActive,
    SortOrder = @SortOrder,
    ModifiedDateUtc = GETUTCDATE()
WHERE DocumentGroupId = @Id AND IsDeleted = 0;";
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, r.Code, r.Name, r.Category, r.Description, r.ConfigurationJson, r.IsActive, r.SortOrder }, cancellationToken: ct));
    }

    public async Task DeleteGroupAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition("UPDATE DMS.DocumentGroup SET IsDeleted=1, ModifiedDateUtc=GETUTCDATE() WHERE DocumentGroupId=@Id;", new { Id = id }, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var cn = await _cf.CreateOpenConnectionAsync(ct);
        var isDocumentCategory = await cn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(1) FROM DMS.DocumentCategory WHERE DocumentCategoryId=@Id AND IsDeleted=0;", new { Id = id }, cancellationToken: ct)) > 0;
        if (isDocumentCategory)
        {
            await cn.ExecuteAsync(new CommandDefinition("UPDATE DMS.DocumentCategory SET IsDeleted=1, ModifiedDateUtc=GETUTCDATE() WHERE DocumentCategoryId=@Id;", new { Id = id }, cancellationToken: ct));
            return;
        }

        await cn.ExecuteAsync(new CommandDefinition("UPDATE DMS.DocumentGroup SET IsDeleted=1, ModifiedDateUtc=GETUTCDATE() WHERE DocumentGroupId=@Id;", new { Id = id }, cancellationToken: ct));
    }
}
