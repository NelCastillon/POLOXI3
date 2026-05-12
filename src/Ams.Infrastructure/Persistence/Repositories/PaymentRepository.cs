using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Payments;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public PaymentRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<Guid> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.AccountId == Guid.Empty) throw new InvalidOperationException("Account is required.");
        if (request.Amount <= 0) throw new InvalidOperationException("Payment amount must be greater than zero.");

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var hasInvoiceId = await HasColumnAsync(cn, "InvoiceId", cancellationToken);
        var hasNotes = await HasColumnAsync(cn, "Notes", cancellationToken);
        var columns = "PaymentId, TenantId, AccountId, PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted";
        var values = "@PaymentId, @TenantId, @AccountId, @PaymentDate, @Amount, @PaymentMethodCode, @ReferenceNumber, @StatusCode, SYSUTCDATETIME(), @CreatedByUserId, 0";

        if (hasInvoiceId)
        {
            columns = columns.Replace("AccountId,", "AccountId, InvoiceId,");
            values = values.Replace("@AccountId,", "@AccountId, @InvoiceId,");
        }

        if (hasNotes)
        {
            columns = columns.Replace("StatusCode,", "StatusCode, Notes,");
            values = values.Replace("@StatusCode,", "@StatusCode, @Notes,");
        }

        var sql = $"INSERT INTO Billing.Payment ({columns}) VALUES ({values});";
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PaymentId = id,
            request.TenantId,
            request.AccountId,
            request.InvoiceId,
            PaymentDate = request.PaymentDate.Date,
            request.Amount,
            PaymentMethodCode = request.PaymentMethodCode.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
            request.StatusCode,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));

        return id;
    }

    public async Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var select = await BuildSelectColumnsAsync(cn, cancellationToken);
        var sql = $"SELECT {select} FROM Billing.Payment WHERE PaymentId = @Id AND IsDeleted = 0;";
        return await cn.QuerySingleOrDefaultAsync<PaymentDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<PaymentDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var select = await BuildSelectColumnsAsync(cn, cancellationToken);
        var searchPredicate = await HasColumnAsync(cn, "Notes", cancellationToken)
            ? "ReferenceNumber LIKE '%' + @SearchTerm + '%' OR Notes LIKE '%' + @SearchTerm + '%'"
            : "ReferenceNumber LIKE '%' + @SearchTerm + '%'";
        var sql = RepositorySql.BuildPagedSearchSql("Billing.Payment", select, searchPredicate, "PaymentDate DESC");
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<PaymentDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PaymentDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    private static async Task<string> BuildSelectColumnsAsync(System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        var hasInvoiceId = await HasColumnAsync(connection, "InvoiceId", cancellationToken);
        var hasNotes = await HasColumnAsync(connection, "Notes", cancellationToken);
        return $"PaymentId, TenantId, AccountId, {(hasInvoiceId ? "InvoiceId" : "CAST(NULL AS UNIQUEIDENTIFIER) AS InvoiceId")}, CAST(PaymentDate AS DATETIME2) AS PaymentDate, Amount, PaymentMethodCode, ReferenceNumber, StatusCode, {(hasNotes ? "Notes" : "CAST(NULL AS NVARCHAR(500)) AS Notes")}, CreatedDateUtc";
    }

    private static async Task<bool> HasColumnAsync(System.Data.IDbConnection connection, string columnName, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(1) FROM sys.columns WHERE object_id = OBJECT_ID(N'Billing.Payment') AND name = @ColumnName;";
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { ColumnName = columnName }, cancellationToken: cancellationToken)) > 0;
    }
}
