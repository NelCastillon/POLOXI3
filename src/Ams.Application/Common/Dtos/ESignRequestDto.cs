namespace Ams.Application.Common.Dtos;

public sealed class ESignRequestDto
{
    public Guid ESignRequestId { get; init; }
    public Guid TenantId { get; init; }
    public Guid DocumentId { get; init; }
    public Guid? PolicyId { get; init; }
    public string Document { get; init; } = string.Empty;
    public string? PolicyNumber { get; init; }
    public string SignerName { get; init; } = string.Empty;
    public string SignerEmail { get; init; } = string.Empty;
    public string Priority { get; init; } = "Normal";
    public string Status { get; init; } = "Sent";
    public string ProviderCode { get; init; } = "DocuSign";
    public string? ProviderEnvelopeId { get; init; }
    public string? ProviderStatus { get; init; }
    public string? IdempotencyKey { get; init; }
    public bool IsOverdue { get; init; }
    public DateTime SentDate { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public string? Message { get; init; }
    public string? VoidReason { get; init; }
    public IReadOnlyList<ESignSignerDto> Signers { get; init; } = [];
    public IReadOnlyList<ESignEnvelopeEventDto> Events { get; init; } = [];
}

public sealed record ESignSignerDto(Guid ESignSignerId, Guid TenantId, Guid ESignRequestId, int RoutingOrder, string SignerName, string SignerEmail, string StatusCode, DateTime? ViewedDateUtc, DateTime? SignedDateUtc, DateTime? DeclinedDateUtc, string? DeclineReason);
public sealed record ESignEnvelopeEventDto(Guid ESignEnvelopeEventId, Guid TenantId, Guid ESignRequestId, string? ProviderEventId, string EventTypeCode, string? ProviderStatus, bool IsSignatureVerified, DateTime OccurredDateUtc, DateTime ReceivedDateUtc);
