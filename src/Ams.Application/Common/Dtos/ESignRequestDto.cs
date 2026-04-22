namespace Ams.Application.Common.Dtos;

public sealed class ESignRequestDto
{
    public Guid ESignRequestId { get; init; }
    public Guid TenantId { get; init; }
    public Guid DocumentId { get; init; }
    public string Document { get; init; } = string.Empty;
    public string? PolicyNumber { get; init; }
    public string SignerName { get; init; } = string.Empty;
    public string SignerEmail { get; init; } = string.Empty;
    public string Priority { get; init; } = "Normal";
    public string Status { get; init; } = "Sent";
    public bool IsOverdue { get; init; }
    public DateTime SentDate { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? CompletedDate { get; init; }
    public string? Message { get; init; }
    public string? VoidReason { get; init; }
}
