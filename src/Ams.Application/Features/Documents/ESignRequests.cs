namespace Ams.Application.Features.Documents;

public sealed record SendESignRequest(
    Guid TenantId,
    Guid DocumentId,
    Guid? PolicyId,
    string SignerName,
    string SignerEmail,
    DateTime DueDate,
    string Priority,
    string? Message,
    string IdempotencyKey,
    Guid? RequestedByUserId = null);

public sealed record VoidESignRequest(
    Guid TenantId,
    Guid ESignRequestId,
    string? VoidReason,
    Guid? ModifiedByUserId = null);

public sealed record ProcessDocuSignCallbackRequest(Guid TenantId, string Payload, string Signature);
