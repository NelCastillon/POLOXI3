using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Agency;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class BranchRepository : IBranchRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public BranchRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = "BranchId, TenantId, BranchCode, BranchName, City, StateProvince, CountryCode, IsActive, CreatedDateUtc";

    public async Task<BranchDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM Core.Branch WHERE BranchId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<BranchDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BranchDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql("Core.Branch", SelectColumns, "BranchName LIKE '%' + @SearchTerm + '%' OR BranchCode LIKE '%' + @SearchTerm + '%'", "CreatedDateUtc DESC");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<BranchDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BranchDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @CompanyId UNIQUEIDENTIFIER = NULL;
DECLARE @InsertColumns NVARCHAR(MAX) = N'BranchId, TenantId, BranchCode, BranchName, City, StateProvince, CountryCode, IsActive, CreatedDateUtc, IsDeleted';
DECLARE @InsertValues  NVARCHAR(MAX) = N'@BranchId, @TenantId, @BranchCode, @BranchName, @City, @StateProvince, @CountryCode, 1, SYSUTCDATETIME(), 0';

IF COL_LENGTH(N'Core.Branch', N'TimeZoneId') IS NOT NULL
BEGIN
    SET @InsertColumns += N', TimeZoneId';
    SET @InsertValues  += N', @TimeZoneId';
END

IF COL_LENGTH(N'Core.Branch', N'CompanyId') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'Core.Company') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'Core.Company', N'TenantId') IS NOT NULL
            EXEC sp_executesql
                N'SELECT TOP (1) @CompanyIdOut = CompanyId FROM Core.Company WHERE TenantId = @TenantId ORDER BY CompanyId;',
                N'@TenantId UNIQUEIDENTIFIER, @CompanyIdOut UNIQUEIDENTIFIER OUTPUT',
                @TenantId,
                @CompanyId OUTPUT;

        IF @CompanyId IS NULL
            SELECT TOP (1) @CompanyId = CompanyId FROM Core.Company ORDER BY CompanyId;
    END

    IF @CompanyId IS NULL
        THROW 50001, 'Cannot create branch because Core.Branch.CompanyId is required but no Core.Company record was found.', 1;

    SET @InsertColumns += N', CompanyId';
    SET @InsertValues  += N', @CompanyId';
END

DECLARE @InsertSql NVARCHAR(MAX) = N'INSERT INTO Core.Branch (' + @InsertColumns + N') VALUES (' + @InsertValues + N');';
EXEC sp_executesql @InsertSql,
    N'@BranchId UNIQUEIDENTIFIER, @TenantId UNIQUEIDENTIFIER, @BranchCode NVARCHAR(50), @BranchName NVARCHAR(200), @City NVARCHAR(100), @StateProvince NVARCHAR(100), @CountryCode NVARCHAR(10), @TimeZoneId NVARCHAR(100), @CompanyId UNIQUEIDENTIFIER',
    @BranchId, @TenantId, @BranchCode, @BranchName, @City, @StateProvince, @CountryCode, @TimeZoneId, @CompanyId;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            BranchId      = id,
            request.TenantId,
            request.BranchCode,
            request.BranchName,
            request.City,
            request.StateProvince,
            CountryCode = request.CountryCode ?? string.Empty,
            TimeZoneId = "UTC",
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Branch
SET    BranchCode      = @BranchCode,
       BranchName      = @BranchName,
       City            = @City,
       StateProvince   = @StateProvince,
       CountryCode     = @CountryCode,
       IsActive        = @IsActive,
       ModifiedDateUtc = GETUTCDATE()
WHERE  BranchId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.BranchCode,
            request.BranchName,
            request.City,
            request.StateProvince,
            CountryCode = request.CountryCode ?? string.Empty,
            request.IsActive,
        }, cancellationToken: cancellationToken));
    }
}
