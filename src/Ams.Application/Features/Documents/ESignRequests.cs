namespace Ams.Application.Features.Documents;

public sealed record SendESignRequest(
    Guid TenantId,
    Guid DocumentId,
    string SignerName,
    string SignerEmail,
    DateTime DueDate,
    string Priority,
    string? Message);

public sealed record VoidESignRequest(
    Guid ESignRequestId,
    string? VoidReason);
