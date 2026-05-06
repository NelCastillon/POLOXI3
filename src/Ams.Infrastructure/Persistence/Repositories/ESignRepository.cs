using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ESignRepository : IESignRepository
{
    private const string SelectColumns = @"
        e.ESignRequestId, e.TenantId, e.DocumentId,
        COALESCE(d.FileName, 'Document unavailable') AS Document, CAST(NULL AS NVARCHAR(100)) AS PolicyNumber,
        e.SignerName, e.SignerEmail, e.Priority, e.Status,
        CASE WHEN e.Status IN ('Sent','Viewed') AND e.DueDate < GETUTCDATE() THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS IsOverdue,
        e.SentDate, e.DueDate, e.CompletedDate, e.Message, e.VoidReason";

    private readonly ISqlConnectionFactory _connectionFactory;
    public ESignRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<ESignRequestDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SelectColumns}
FROM DMS.ESignRequest e
LEFT JOIN DMS.Document d ON d.DocumentId = e.DocumentId
WHERE e.TenantId = @TenantId AND e.IsDeleted = 0
ORDER BY e.SentDate DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QueryAsync<ESignRequestDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<ESignRequestDto?> GetByIdAsync(Guid eSignRequestId, CancellationToken cancellationToken = default)
    {
        var sql = $@"
SELECT {SelectColumns}
FROM DMS.ESignRequest e
LEFT JOIN DMS.Document d ON d.DocumentId = e.DocumentId
WHERE e.ESignRequestId = @Id AND e.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ESignRequestDto>(new CommandDefinition(sql, new { Id = eSignRequestId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> SendAsync(SendESignRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var sql = @"
INSERT INTO DMS.ESignRequest
    (ESignRequestId, TenantId, DocumentId, SignerName, SignerEmail, Priority, Status, SentDate, DueDate, Message, IsDeleted, CreatedDateUtc)
VALUES
    (@ESignRequestId, @TenantId, @DocumentId, @SignerName, @SignerEmail, @Priority, 'Sent', GETUTCDATE(), @DueDate, @Message, 0, GETUTCDATE());";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ESignRequestId = id,
            request.TenantId,
            request.DocumentId,
            request.SignerName,
            request.SignerEmail,
            request.Priority,
            request.DueDate,
            request.Message
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task VoidAsync(VoidESignRequest request, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE DMS.ESignRequest
SET Status = 'Voided', VoidReason = @VoidReason, ModifiedDateUtc = GETUTCDATE()
WHERE ESignRequestId = @ESignRequestId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { request.ESignRequestId, request.VoidReason }, cancellationToken: cancellationToken));
    }

    public async Task RemindAsync(Guid eSignRequestId, CancellationToken cancellationToken = default)
    {
        var sql = @"
UPDATE DMS.ESignRequest
SET LastReminderSentDateUtc = GETUTCDATE(), ModifiedDateUtc = GETUTCDATE()
WHERE ESignRequestId = @ESignRequestId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ESignRequestId = eSignRequestId }, cancellationToken: cancellationToken));
    }
}
