using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Documents;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DocumentPacketRepository : IDocumentPacketRepository
{
    private const string PacketSelectColumns = @"
        p.DocumentPacketId, p.TenantId, p.PacketName, p.PacketType, p.PolicyNumber, p.Status,
        p.AiAssisted, p.Description, p.RecipientEmail, p.DeliveryMethod, p.SentDateUtc,
        p.MergedDateUtc, p.CreatedDateUtc,
        COUNT(pd.PacketDocumentId) AS DocumentCount,
        SUM(CASE WHEN pd.Status = N'Ready' AND pd.IsDeleted = 0 THEN 1 ELSE 0 END) AS ReadyCount,
        SUM(CASE WHEN pd.Status = N'Missing' AND pd.IsDeleted = 0 THEN 1 ELSE 0 END) AS MissingCount";

    private readonly ISqlConnectionFactory _connectionFactory;

    public DocumentPacketRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<DocumentPacketDto>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {PacketSelectColumns}
FROM DMS.DocumentPacket p
LEFT JOIN DMS.DocumentPacketDocument pd ON pd.DocumentPacketId = p.DocumentPacketId AND pd.IsDeleted = 0
WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
GROUP BY p.DocumentPacketId, p.TenantId, p.PacketName, p.PacketType, p.PolicyNumber, p.Status, p.AiAssisted, p.Description, p.RecipientEmail, p.DeliveryMethod, p.SentDateUtc, p.MergedDateUtc, p.CreatedDateUtc
ORDER BY p.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var packets = (await cn.QueryAsync<DocumentPacketDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
        if (packets.Count == 0) return packets;

        var documents = await GetDocumentsAsync(packets.Select(p => p.DocumentPacketId).ToArray(), cancellationToken);
        foreach (var packet in packets)
        {
            packet.Documents = documents.Where(d => d.DocumentPacketId == packet.DocumentPacketId).OrderBy(d => d.SortOrder).ToList();
        }

        return packets;
    }

    public async Task<DocumentPacketDto?> GetByIdAsync(Guid documentPacketId, CancellationToken cancellationToken = default)
    {
        const string sql = $@"
SELECT {PacketSelectColumns}
FROM DMS.DocumentPacket p
LEFT JOIN DMS.DocumentPacketDocument pd ON pd.DocumentPacketId = p.DocumentPacketId AND pd.IsDeleted = 0
WHERE p.DocumentPacketId = @DocumentPacketId AND p.IsDeleted = 0
GROUP BY p.DocumentPacketId, p.TenantId, p.PacketName, p.PacketType, p.PolicyNumber, p.Status, p.AiAssisted, p.Description, p.RecipientEmail, p.DeliveryMethod, p.SentDateUtc, p.MergedDateUtc, p.CreatedDateUtc;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var packet = await cn.QuerySingleOrDefaultAsync<DocumentPacketDto>(new CommandDefinition(sql, new { DocumentPacketId = documentPacketId }, cancellationToken: cancellationToken));
        if (packet is null) return null;
        packet.Documents = await GetDocumentsAsync([documentPacketId], cancellationToken);
        return packet;
    }

    public async Task<Guid> CreateAsync(CreateDocumentPacketRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO DMS.DocumentPacket
    (DocumentPacketId, TenantId, PacketName, PacketType, PolicyNumber, Status, AiAssisted, Description, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@DocumentPacketId, @TenantId, @PacketName, @PacketType, @PolicyNumber, N'Draft', @AiAssisted, @Description, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            DocumentPacketId = id,
            request.TenantId,
            request.PacketName,
            request.PacketType,
            request.PolicyNumber,
            request.AiAssisted,
            request.Description,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task<Guid> AddDocumentAsync(AddDocumentPacketDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        const string sql = @"
DECLARE @SortOrder INT = COALESCE((SELECT MAX(SortOrder) + 1 FROM DMS.DocumentPacketDocument WHERE DocumentPacketId = @DocumentPacketId AND IsDeleted = 0), 1);
INSERT INTO DMS.DocumentPacketDocument
    (PacketDocumentId, DocumentPacketId, DocumentId, DocumentName, DocumentType, IsRequired, Status, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@PacketDocumentId, @DocumentPacketId, @DocumentId, @DocumentName, @DocumentType, @IsRequired, @Status, @SortOrder, SYSUTCDATETIME(), @CreatedByUserId, 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PacketDocumentId = id,
            request.DocumentPacketId,
            request.DocumentId,
            request.DocumentName,
            request.DocumentType,
            request.IsRequired,
            request.Status,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task RemoveDocumentAsync(Guid packetDocumentId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentPacketDocument
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE PacketDocumentId = @PacketDocumentId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { PacketDocumentId = packetDocumentId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task ReorderDocumentsAsync(ReorderDocumentPacketDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentPacketDocument
SET SortOrder = @SortOrder, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE PacketDocumentId = @PacketDocumentId AND DocumentPacketId = @DocumentPacketId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        for (var i = 0; i < request.PacketDocumentIds.Count; i++)
        {
            await cn.ExecuteAsync(new CommandDefinition(sql, new { request.DocumentPacketId, PacketDocumentId = request.PacketDocumentIds[i], SortOrder = i + 1, request.ModifiedByUserId }, cancellationToken: cancellationToken));
        }
    }

    public async Task SendAsync(SendDocumentPacketRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentPacket
SET Status = N'Sent', RecipientEmail = @RecipientEmail, DeliveryMethod = @DeliveryMethod, SentMessage = @Message,
    SentDateUtc = SYSUTCDATETIME(), ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE DocumentPacketId = @DocumentPacketId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task UpdateStatusAsync(UpdateDocumentPacketStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentPacket
SET Status = @Status,
    Notes = COALESCE(@Notes, Notes),
    MergedDateUtc = CASE WHEN @Status = N'Merged' THEN COALESCE(MergedDateUtc, SYSUTCDATETIME()) ELSE MergedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE DocumentPacketId = @DocumentPacketId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid documentPacketId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE DMS.DocumentPacket
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE DocumentPacketId = @DocumentPacketId AND IsDeleted = 0;
UPDATE DMS.DocumentPacketDocument
SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId
WHERE DocumentPacketId = @DocumentPacketId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { DocumentPacketId = documentPacketId, ModifiedByUserId = modifiedByUserId }, cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<DocumentPacketDocumentDto>> GetDocumentsAsync(Guid[] packetIds, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT PacketDocumentId, DocumentPacketId, DocumentId, DocumentName, DocumentType, IsRequired, Status, SortOrder, CreatedDateUtc
FROM DMS.DocumentPacketDocument
WHERE DocumentPacketId IN @PacketIds AND IsDeleted = 0
ORDER BY DocumentPacketId, SortOrder;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var result = await cn.QueryAsync<DocumentPacketDocumentDto>(new CommandDefinition(sql, new { PacketIds = packetIds }, cancellationToken: cancellationToken));
        return result.AsList();
    }
}
