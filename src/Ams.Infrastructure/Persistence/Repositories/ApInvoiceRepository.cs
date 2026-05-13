using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Finance;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ApInvoiceRepository : IApInvoiceRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ApInvoiceRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ApInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT 
    ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, 
    Amount, AmountPaid, TaxAmount, StatusCode, GLAccountId, AgreementId, 
    Description, Notes, CreatedDateUtc 
FROM Finance.ApInvoice 
WHERE ApInvoiceId = @Id AND IsDeleted = 0";
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ApInvoiceDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ApInvoiceDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var selectColumns = "ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, Amount, AmountPaid, TaxAmount, StatusCode, GLAccountId, AgreementId, Description, Notes, CreatedDateUtc";
        var searchPredicate = "InvoiceNumber LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%'";

        var sql = RepositorySql.BuildPagedSearchSql("Finance.ApInvoice", selectColumns, searchPredicate, "InvoiceDate DESC");
        
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ApInvoiceDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ApInvoiceDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<Guid> CreateAsync(CreateApInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Finance.ApInvoice (ApInvoiceId, TenantId, VendorId, InvoiceNumber, InvoiceDate, DueDate, Amount, AmountPaid, TaxAmount, StatusCode, GLAccountId, AgreementId, Description, Notes, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES (@Id, @TenantId, @VendorId, @InvoiceNumber, @InvoiceDate, @DueDate, @Amount, @AmountPaid, @TaxAmount, @StatusCode, @GLAccountId, @AgreementId, @Description, @Notes, SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.VendorId, request.InvoiceNumber, request.InvoiceDate, request.DueDate, request.Amount, request.AmountPaid, request.TaxAmount, request.StatusCode, request.GLAccountId, request.AgreementId, request.Description, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateApInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Finance.ApInvoice
SET VendorId = @VendorId,
    InvoiceNumber = @InvoiceNumber,
    InvoiceDate = @InvoiceDate,
    DueDate = @DueDate,
    Amount = @Amount,
    AmountPaid = @AmountPaid,
    TaxAmount = @TaxAmount,
    StatusCode = @StatusCode,
    GLAccountId = @GLAccountId,
    AgreementId = @AgreementId,
    Description = @Description,
    Notes = @Notes,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE ApInvoiceId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.VendorId, request.InvoiceNumber, request.InvoiceDate, request.DueDate, request.Amount, request.AmountPaid, request.TaxAmount, request.StatusCode, request.GLAccountId, request.AgreementId, request.Description, request.Notes, request.ModifiedByUserId }, cancellationToken: cancellationToken));
    }
}
