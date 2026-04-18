using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SupportedLocaleRepository : ISupportedLocaleRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SupportedLocaleRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<SupportedLocaleDto?> GetByIdAsync(Guid localeId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT LocaleId, LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol,
                   DateFormat, TimeFormat, NumberFormat, IsRtl, IsActive, CreatedDateUtc
            FROM Core.SupportedLocale
            WHERE LocaleId = @LocaleId AND IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SupportedLocaleDto>(
            new CommandDefinition(sql, new { LocaleId = localeId }, cancellationToken: cancellationToken));
    }

    public async Task<SupportedLocaleDto?> GetByCodeAsync(string localeCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT LocaleId, LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol,
                   DateFormat, TimeFormat, NumberFormat, IsRtl, IsActive, CreatedDateUtc
            FROM Core.SupportedLocale
            WHERE LocaleCode = @LocaleCode AND IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SupportedLocaleDto>(
            new CommandDefinition(sql, new { LocaleCode = localeCode }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<SupportedLocaleDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT LocaleId, LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol,
                       DateFormat, TimeFormat, NumberFormat, IsRtl, IsActive, CreatedDateUtc
                FROM Core.SupportedLocale
                WHERE IsDeleted = 0
                  AND (@SearchTerm IS NULL OR LocaleName  LIKE '%' + @SearchTerm + '%'
                                          OR LocaleCode  LIKE '%' + @SearchTerm + '%'
                                          OR CurrencyCode = @SearchTerm)
            )
            SELECT * FROM Cte ORDER BY LocaleName ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.SupportedLocale
            WHERE IsDeleted = 0
              AND (@SearchTerm IS NULL OR LocaleName  LIKE '%' + @SearchTerm + '%'
                                      OR LocaleCode  LIKE '%' + @SearchTerm + '%'
                                      OR CurrencyCode = @SearchTerm);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SupportedLocaleDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SupportedLocaleDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
