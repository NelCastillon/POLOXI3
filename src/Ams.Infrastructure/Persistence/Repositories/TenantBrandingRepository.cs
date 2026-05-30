using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Tenants;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class TenantBrandingRepository : ITenantBrandingRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public TenantBrandingRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<TenantBrandingDto?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT BrandingId, TenantId, WhiteLabelName, LogoUrl, FaviconUrl,
                   PrimaryColor, SecondaryColor, AccentColor, CustomDomain, CustomCssUrl,
                   SupportEmail, SupportPhone, FooterText, IsActive, CreatedDateUtc, ModifiedDateUtc
            FROM Core.TenantBranding
            WHERE TenantId = @TenantId AND IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TenantBrandingDto>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<TenantBrandingDto?> GetByIdAsync(Guid brandingId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT BrandingId, TenantId, WhiteLabelName, LogoUrl, FaviconUrl,
                   PrimaryColor, SecondaryColor, AccentColor, CustomDomain, CustomCssUrl,
                   SupportEmail, SupportPhone, FooterText, IsActive, CreatedDateUtc, ModifiedDateUtc
            FROM Core.TenantBranding
            WHERE BrandingId = @BrandingId AND IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<TenantBrandingDto>(
            new CommandDefinition(sql, new { BrandingId = brandingId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<TenantBrandingDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT BrandingId, TenantId, WhiteLabelName, LogoUrl, FaviconUrl,
                       PrimaryColor, SecondaryColor, AccentColor, CustomDomain,
                       SupportEmail, SupportPhone, FooterText, IsActive, CreatedDateUtc, ModifiedDateUtc
                FROM Core.TenantBranding
                WHERE IsDeleted = 0
                  AND (@SearchTerm IS NULL OR WhiteLabelName LIKE '%' + @SearchTerm + '%'
                                          OR CustomDomain   LIKE '%' + @SearchTerm + '%')
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.TenantBranding
            WHERE IsDeleted = 0
              AND (@SearchTerm IS NULL OR WhiteLabelName LIKE '%' + @SearchTerm + '%'
                                      OR CustomDomain   LIKE '%' + @SearchTerm + '%');
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<TenantBrandingDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<TenantBrandingDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task UpdateAsync(Guid tenantId, UpdateTenantBrandingRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM Core.TenantBranding WHERE TenantId = @TenantId AND IsDeleted = 0)
                UPDATE Core.TenantBranding SET
                    WhiteLabelName  = @WhiteLabelName,
                    LogoUrl         = @LogoUrl,
                    FaviconUrl      = @FaviconUrl,
                    PrimaryColor    = @PrimaryColor,
                    SecondaryColor  = @SecondaryColor,
                    AccentColor     = @AccentColor,
                    CustomDomain    = @CustomDomain,
                    CustomCssUrl    = @CustomCssUrl,
                    SupportEmail    = @SupportEmail,
                    SupportPhone    = @SupportPhone,
                    FooterText      = @FooterText,
                    ModifiedDateUtc = SYSUTCDATETIME()
                WHERE TenantId = @TenantId AND IsDeleted = 0;
            ELSE
                INSERT INTO Core.TenantBranding
                    (TenantId, WhiteLabelName, LogoUrl, FaviconUrl, PrimaryColor, SecondaryColor, AccentColor, CustomDomain, CustomCssUrl, SupportEmail, SupportPhone, FooterText, IsActive, CreatedDateUtc, IsDeleted)
                VALUES
                    (@TenantId, @WhiteLabelName, @LogoUrl, @FaviconUrl, @PrimaryColor, @SecondaryColor, @AccentColor, @CustomDomain, @CustomCssUrl, @SupportEmail, @SupportPhone, @FooterText, 1, SYSUTCDATETIME(), 0);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            request.WhiteLabelName,
            request.LogoUrl,
            request.FaviconUrl,
            request.PrimaryColor,
            request.SecondaryColor,
            request.AccentColor,
            request.CustomDomain,
            request.CustomCssUrl,
            request.SupportEmail,
            request.SupportPhone,
            request.FooterText,
        }, cancellationToken: cancellationToken));
    }

    public async Task ResetToDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE Core.TenantBranding SET
                WhiteLabelName  = NULL,
                LogoUrl         = NULL,
                FaviconUrl      = NULL,
                PrimaryColor    = '#0d6efd',
                SecondaryColor  = '#6c757d',
                AccentColor     = '#198754',
                CustomDomain    = NULL,
                CustomCssUrl    = NULL,
                SupportEmail    = NULL,
                SupportPhone    = NULL,
                FooterText      = NULL,
                ModifiedDateUtc = SYSUTCDATETIME()
            WHERE TenantId = @TenantId AND IsDeleted = 0;
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
