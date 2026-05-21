using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DocumentExceptionRepository : IDocumentExceptionRepository
{
    private const string SelectColumns = @"
        DocumentExceptionId, TenantId, DocumentId, FileName, ContentType, FileSizeBytes,
        ExceptionType, ExceptionReason, Status, AiSuggestion, AiConfidence, AssignedToName,
        CategoryCode, DocumentTypeCode, LinkedEntity, Tags, Notes, ReceivedDateUtc,
        ResolvedDateUtc, CreatedDateUtc";

    private readonly ISqlConnectionFactory _connectionFactory;

    public DocumentExceptionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<DocumentExceptionDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {SelectColumns}
FROM DMS.DocumentException
WHERE TenantId = @TenantId AND IsDeleted = 0
ORDER BY CASE WHEN Status = N'Resolved' THEN 1 ELSE 0 END, ReceivedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QueryAsync<DocumentExceptionDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return result.AsList();
    }

    public async Task<DocumentExceptionDto?> GetByIdAsync(Guid documentExceptionId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {SelectColumns}
FROM DMS.DocumentException
WHERE DocumentExceptionId = @DocumentExceptionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<DocumentExceptionDto>(new CommandDefinition(sql, new { DocumentExceptionId = documentExceptionId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateDocumentExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO DMS.DocumentException
    (DocumentExceptionId, TenantId, DocumentId, FileName, ContentType, FileSizeBytes, ExceptionType, ExceptionReason, Status, AiSuggestion, AiConfidence, AssignedToName, ReceivedDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@DocumentExceptionId, @TenantId, @DocumentId, @FileName, @ContentType, @FileSizeBytes, @ExceptionType, @ExceptionReason, @Status, @AiSuggestion, @AiConfidence, @AssignedToName, SYSUTCDATETIME(), SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            DocumentExceptionId = id,
            request.TenantId,
            request.DocumentId,
            request.FileName,
            request.ContentType,
            request.FileSizeBytes,
            request.ExceptionType,
            request.ExceptionReason,
            request.Status,
            request.AiSuggestion,
            request.AiConfidence,
            request.AssignedToName,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task ClassifyAsync(ClassifyDocumentExceptionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentException
SET CategoryCode = @CategoryCode,
    DocumentTypeCode = @DocumentTypeCode,
    LinkedEntity = @LinkedEntity,
    Tags = @Tags,
    Notes = @Notes,
    Status = N'Resolved',
    ResolvedDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE DocumentExceptionId = @DocumentExceptionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(UpdateDocumentExceptionStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentException
SET Status = @Status,
    Notes = COALESCE(@Notes, Notes),
    ResolvedDateUtc = CASE WHEN @Status = N'Resolved' THEN COALESCE(ResolvedDateUtc, SYSUTCDATETIME()) ELSE NULL END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE DocumentExceptionId = @DocumentExceptionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }
}
