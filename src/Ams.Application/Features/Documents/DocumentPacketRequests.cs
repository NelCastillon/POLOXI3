using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Documents;

public sealed record CreateDocumentPacketRequest(
    [property: Required] Guid TenantId,
    [property: Required, StringLength(200)] string PacketName,
    [property: Required, StringLength(80)] string PacketType,
    [property: StringLength(100)] string? PolicyNumber,
    bool AiAssisted,
    [property: StringLength(1000)] string? Description,
    Guid? CreatedByUserId);

public sealed record AddDocumentPacketDocumentRequest(
    [property: Required] Guid DocumentPacketId,
    Guid? DocumentId,
    [property: Required, StringLength(260)] string DocumentName,
    [property: Required, StringLength(100)] string DocumentType,
    bool IsRequired,
    [property: Required, StringLength(40)] string Status,
    Guid? CreatedByUserId);

public sealed record ReorderDocumentPacketDocumentsRequest(
    [property: Required] Guid DocumentPacketId,
    IReadOnlyList<Guid> PacketDocumentIds,
    Guid? ModifiedByUserId);

public sealed record SendDocumentPacketRequest(
    [property: Required] Guid DocumentPacketId,
    [property: Required, EmailAddress, StringLength(256)] string RecipientEmail,
    [property: Required, StringLength(80)] string DeliveryMethod,
    [property: StringLength(1000)] string? Message,
    Guid? ModifiedByUserId);

public sealed record UpdateDocumentPacketStatusRequest(
    [property: Required] Guid DocumentPacketId,
    [property: Required, StringLength(40)] string Status,
    [property: StringLength(1000)] string? Notes,
    Guid? ModifiedByUserId);
