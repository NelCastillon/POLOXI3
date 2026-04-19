using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Tenants;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TenantDomainRepository : ITenantDomainRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public TenantDomainRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns = """
        TenantDomainId, TenantId, DomainName, IsPrimary,
        SslStatusCode, VerificationStatusCode, VerificationToken,
        VerifiedDateUtc, RedirectTarget, SslExpiresDateUtc,
        IsActive, CreatedDateUtc, ModifiedDateUtc, CreatedByUserId, Notes
        """;

    public async Task<TenantDomainDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM Core.TenantDomain WHERE TenantDomainId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TenantDomainDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TenantDomainDto>> SearchByTenantAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = $"""
            ;WITH Cte AS (
                SELECT {SelectColumns}
                FROM Core.TenantDomain
                WHERE TenantId = @TenantId AND IsDeleted = 0
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR DomainName LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY IsPrimary DESC, CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.TenantDomain
            WHERE TenantId = @TenantId AND IsDeleted = 0
              AND (@SearchTerm IS NULL OR @SearchTerm = ''
                   OR DomainName LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TenantDomainDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TenantDomainDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<TenantDomainDto>> SearchAllAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = """
            ;WITH Cte AS (
                SELECT d.TenantDomainId, d.TenantId, d.DomainName, d.IsPrimary,
                       d.SslStatusCode, d.VerificationStatusCode, d.VerificationToken,
                       d.VerifiedDateUtc, d.RedirectTarget, d.SslExpiresDateUtc,
                       d.IsActive, d.CreatedDateUtc, d.ModifiedDateUtc, d.CreatedByUserId, d.Notes,
                       t.TenantName
                FROM Core.TenantDomain d
                INNER JOIN Core.Tenant t ON t.TenantId = d.TenantId
                WHERE d.IsDeleted = 0
                  AND (@SearchTerm IS NULL OR @SearchTerm = ''
                       OR d.DomainName LIKE '%' + @SearchTerm + '%'
                       OR t.TenantName LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.TenantDomain d
            INNER JOIN Core.Tenant t ON t.TenantId = d.TenantId
            WHERE d.IsDeleted = 0
              AND (@SearchTerm IS NULL OR @SearchTerm = ''
                   OR d.DomainName LIKE '%' + @SearchTerm + '%'
                   OR t.TenantName LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize },
            cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TenantDomainDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TenantDomainDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateTenantDomainRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO Core.TenantDomain
                (TenantDomainId, TenantId, DomainName, IsPrimary,
                 SslStatusCode, VerificationStatusCode, VerificationToken,
                 RedirectTarget, Notes, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
            VALUES
                (@TenantDomainId, @TenantId, @DomainName, @IsPrimary,
                 'None', 'Pending', @VerificationToken,
                 @RedirectTarget, @Notes, 1, SYSUTCDATETIME(), @CreatedByUserId, 0);
            """;
        var id = Guid.NewGuid();
        var token = $"ams-verify={Guid.NewGuid():N}";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantDomainId    = id,
            request.TenantId,
            request.DomainName,
            request.IsPrimary,
            VerificationToken = token,
            request.RedirectTarget,
            request.Notes,
            request.CreatedByUserId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateRedirectAsync(Guid id, string? redirectTarget, string? notes = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Core.TenantDomain SET
                RedirectTarget  = @RedirectTarget,
                Notes           = @Notes,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE TenantDomainId = @Id AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, RedirectTarget = redirectTarget, Notes = notes }, cancellationToken: cancellationToken));
    }

    public async Task SetPrimaryAsync(Guid tenantId, Guid domainId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Core.TenantDomain SET IsPrimary = 0, ModifiedDateUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND IsDeleted = 0;
            UPDATE Core.TenantDomain SET IsPrimary = 1, ModifiedDateUtc = SYSUTCDATETIME()
            WHERE TenantDomainId = @DomainId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, DomainId = domainId }, cancellationToken: cancellationToken));
    }

    public async Task VerifyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Core.TenantDomain SET
                VerificationStatusCode = 'Verified',
                SslStatusCode          = 'Provisioning',
                VerifiedDateUtc        = SYSUTCDATETIME(),
                ModifiedDateUtc        = SYSUTCDATETIME()
            WHERE TenantDomainId = @Id AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Core.TenantDomain SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME()
            WHERE TenantDomainId = @Id AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
