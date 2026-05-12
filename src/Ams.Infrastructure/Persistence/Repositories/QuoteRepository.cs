using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Quotes;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class QuoteRepository : IQuoteRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public QuoteRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreateAsync(CreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        if (request.AccountId == Guid.Empty)
        {
            throw new InvalidOperationException("Account is required.");
        }

        if (string.IsNullOrWhiteSpace(request.QuoteNumber))
        {
            request.QuoteNumber = $"QT-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        const string sql = @"
INSERT INTO CRM.Quote
(
    QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId,
    ValidUntilDate, TotalAmount, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted
)
VALUES
(
    @QuoteId, @TenantId, @QuoteNumber, @OpportunityId, @AccountId,
    @ValidUntilDate, @TotalAmount, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, 0
);";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            QuoteId = id,
            request.TenantId,
            QuoteNumber = request.QuoteNumber.Trim(),
            request.OpportunityId,
            request.AccountId,
            request.ValidUntilDate,
            request.TotalAmount,
            StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Draft" : request.StatusCode.Trim(),
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<QuoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT q.QuoteId, q.TenantId, q.QuoteNumber, q.OpportunityId,
       o.OpportunityName, q.AccountId,
       a.AccountName, q.TotalAmount, q.ValidUntilDate,
       q.StatusCode, q.CreatedDateUtc, q.ModifiedDateUtc
FROM CRM.Quote q
LEFT JOIN Client.Account a ON a.AccountId = q.AccountId
LEFT JOIN CRM.Opportunity o ON o.OpportunityId = q.OpportunityId
WHERE q.QuoteId = @Id AND q.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<QuoteDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<QuoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Paged AS (
    SELECT q.QuoteId, q.TenantId, q.QuoteNumber, q.OpportunityId,
           o.OpportunityName, q.AccountId,
            a.AccountName, q.TotalAmount, q.ValidUntilDate,
            q.StatusCode, q.CreatedDateUtc, q.ModifiedDateUtc
    FROM CRM.Quote q
    LEFT JOIN Client.Account a ON a.AccountId = q.AccountId
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = q.OpportunityId
    WHERE q.TenantId = @TenantId AND q.IsDeleted = 0
      AND (
           @SearchTerm IS NULL OR @SearchTerm = ''
           OR q.QuoteNumber LIKE '%' + @SearchTerm + '%'
           OR a.AccountName LIKE '%' + @SearchTerm + '%'
           OR q.StatusCode LIKE '%' + @SearchTerm + '%'
      )
)
SELECT * FROM Paged ORDER BY CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(*) FROM CRM.Quote WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR QuoteNumber LIKE '%' + @SearchTerm + '%');";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<QuoteDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<QuoteDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task UpdateAsync(UpdateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        if (request.QuoteId == Guid.Empty)
        {
            throw new InvalidOperationException("Quote is required.");
        }

        if (request.AccountId == Guid.Empty)
        {
            throw new InvalidOperationException("Account is required.");
        }

        if (string.IsNullOrWhiteSpace(request.QuoteNumber))
        {
            throw new InvalidOperationException("Quote number is required.");
        }

        const string sql = @"
UPDATE CRM.Quote
SET QuoteNumber = @QuoteNumber,
    OpportunityId = @OpportunityId,
    AccountId = @AccountId,
    ValidUntilDate = @ValidUntilDate,
    TotalAmount = @TotalAmount,
    StatusCode = @StatusCode,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE QuoteId = @QuoteId AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.QuoteId,
            QuoteNumber = request.QuoteNumber.Trim(),
            request.OpportunityId,
            request.AccountId,
            request.ValidUntilDate,
            request.TotalAmount,
            StatusCode = string.IsNullOrWhiteSpace(request.StatusCode) ? "Draft" : request.StatusCode.Trim(),
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE CRM.Quote
SET IsDeleted = 1,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE QuoteId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<QuoteLineDto>> GetLinesByQuoteIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT QuoteLineId, TenantId, QuoteId, LineOrder, ItemCode, Description,
       Quantity, UnitPrice, DiscountPercent, TaxPercent, LineTotal, CreatedDateUtc
FROM CRM.QuoteLine
WHERE QuoteId = @QuoteId AND IsDeleted = 0
ORDER BY LineOrder;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await cn.QueryAsync<QuoteLineDto>(
            new CommandDefinition(sql, new { QuoteId = quoteId }, cancellationToken: cancellationToken));
        return items.AsList();
    }
}
