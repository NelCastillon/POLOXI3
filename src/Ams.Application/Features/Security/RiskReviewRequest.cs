namespace Ams.Application.Features.Security;

public sealed class RiskReviewRequest
{
    public Guid TrustedDeviceId { get; set; }
    public string? RiskNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
}
